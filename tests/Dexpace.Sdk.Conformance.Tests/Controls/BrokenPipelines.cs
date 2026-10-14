// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>
/// The deliberately broken pipeline policies and subjects the invariant suite's negative controls use (plan 1.4 and after): each
/// breaks exactly the clause its assertion judges, and a pipeline built from one of them replaces the subject's
/// <c>CreatePipeline</c>. They are test code over core's public policy surface, never part of the kit.
/// </summary>
internal static class BrokenPipelines
{
    /// <summary>A subject whose pipeline is exactly the given policies over the transport (no standard resilience).</summary>
    /// <param name="name">What is broken.</param>
    /// <param name="policies">Builds the policies from the run's recording clock.</param>
    internal static InvariantSubject Subject(string name, Func<TimeProvider, HttpPipelinePolicy[]> policies) =>
        new()
        {
            Name = "broken: " + name,
            CreatePipeline = (transport, options, time, _) => new PipelineBuilder().AddRange(policies(time)).Build(transport, options),
        };

    /// <summary>The default pipeline of a subject with one more policy added to it (flattened, PIPE-35), so a control breaks one layer and keeps the rest.</summary>
    /// <param name="name">What is broken.</param>
    /// <param name="extra">Builds the policy that breaks the clause.</param>
    internal static InvariantSubject OverDefault(string name, Func<TimeProvider, HttpPipelinePolicy> extra) =>
        new()
        {
            Name = "broken: " + name,
            CreatePipeline = (transport, options, time, logger) =>
                PipelineBuilder.Flatten(new InvariantSubject().CreatePipeline(transport, options, time, logger)).Add(extra(time)).Build(),
        };

    /// <summary>A retry loop that treats a cancelled send as a failure to retry (XCUT-1).</summary>
    internal sealed class RetriesCancellation : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Retry;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return await continuation.RunAsync(request, context);
                }
                catch (OperationCanceledException) when (attempt < 3)
                {
                }
            }
        }
    }

    /// <summary>A layer that reports a cancellation as a retryable transport failure (XCUT-1).</summary>
    internal sealed class WrapsCancellation : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Retry;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            try
            {
                return await continuation.RunAsync(request, context);
            }
            catch (OperationCanceledException cancelled)
            {
                throw new ServiceRequestException("interrupted", cancelled);
            }
        }
    }

    /// <summary>An attempt deadline that surfaces as a cancellation and ends the call (XCUT-2).</summary>
    internal sealed class DeadlineAsCancellation : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Retry;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            using var deadline = new CancellationTokenSource(context.Options.AttemptTimeout ?? TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
            return await continuation.RunAsync(request, context.WithCancellationToken(linked.Token));
        }
    }

    /// <summary>A retry wait that does not honour the caller's token (XCUT-3).</summary>
    internal sealed class IgnoresTokenWait(TimeProvider time) : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Retry;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            for (var attempt = 0; ; attempt++)
            {
                var response = await continuation.RunAsync(request, context);
                if (response.Status.Code != 503 || attempt >= 3)
                {
                    return response;
                }

                response.Dispose();
                await time.DelayAsync(context.Options.Retry.FixedDelay ?? TimeSpan.FromSeconds(30), CancellationToken.None);
            }
        }
    }

    /// <summary>A retry wait that is cancellable but never disposes the timer it armed (XCUT-3).</summary>
    internal sealed class LeaksItsTimer(TimeProvider time) : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Retry;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            for (var attempt = 0; ; attempt++)
            {
                var response = await continuation.RunAsync(request, context);
                if (response.Status.Code != 503 || attempt >= 3)
                {
                    return response;
                }

                response.Dispose();
                var woke = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = time.CreateTimer(_ => woke.TrySetResult(), null, context.Options.Retry.FixedDelay ?? TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
                using var registration = context.CancellationToken.Register(() => woke.TrySetCanceled(context.CancellationToken));
                await woke.Task;
            }
        }
    }

    /// <summary>A mapping that turns a refused connection into an error that is not retryable (XCUT-4).</summary>
    internal sealed class RefusalNotRetryable : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.PerCall;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            try
            {
                return await continuation.RunAsync(request, context);
            }
            catch (ServiceRequestException refused)
            {
                throw new SdkException("refused: " + refused.Message);
            }
        }
    }

    /// <summary>A mapping that surfaces an error status as a bare SDK error carrying no response (XCUT-4).</summary>
    internal sealed class StatusAsBareError : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.PerCall;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            try
            {
                return await continuation.RunAsync(request, context);
            }
            catch (HttpResponseException failed)
            {
                throw new SdkException("the server answered " + failed.Status.Code);
            }
        }
    }

    /// <summary>A retry loop that retries only the concrete transport types it knows (XCUT-6).</summary>
    internal sealed class RetriesConcreteTypes : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Retry;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return await continuation.RunAsync(request, context);
                }
                catch (Exception ex) when (ex is ServiceRequestException or IOException && attempt < context.Options.Retry.MaxRetryAttempts)
                {
                }
            }
        }
    }

    /// <summary>A retry loop that decides from the error's baked retryable flag instead of the configured status set (XCUT-7).</summary>
    internal sealed class RetriesOnBakedFlag : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Retry;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            for (var attempt = 0; ; attempt++)
            {
                var response = await continuation.RunAsync(request, context);
                if (!response.Status.IsError || attempt >= context.Options.Retry.MaxRetryAttempts || !new HttpResponseException(response).IsRetryable)
                {
                    return response;
                }

                response.Dispose();
            }
        }
    }

    /// <summary>A mapping step that raises for every status from 300 up, so a non-error response is not returned as it is (XCUT-8).</summary>
    internal sealed class ThrowsForRedirects : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.PerCall;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            var response = await continuation.RunAsync(request, context);
            return response.Status.Code >= 300 ? throw new HttpResponseException(response) : response;
        }
    }

    /// <summary>An exception whose retryable flag is whatever the factory baked in (XCUT-5).</summary>
    internal sealed class BakedException(bool retryable) : SdkException("baked")
    {
        public override bool IsRetryable => retryable;
    }

    /// <summary>
    /// A retry loop with the safety gate applied to one kind of failure only, or to neither (XCUT-10). A request is safe to re-send
    /// when it has no body and an idempotent method, or a replayable body.
    /// </summary>
    internal sealed class RetryLoop(bool gateTransportFailures, bool gateProtocolFailures) : HttpPipelinePolicy
    {
        private static readonly HashSet<int> s_retryableStatuses = [408, 429, 500, 502, 503, 504];

        public override PipelineStage Stage => PipelineStage.Retry;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            var retries = context.Options.Retry.MaxRetryAttempts;
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    var response = await continuation.RunAsync(request, context);
                    if (attempt < retries && s_retryableStatuses.Contains(response.Status.Code) && (!gateProtocolFailures || Safe(request)))
                    {
                        response.Dispose();
                        continue;
                    }

                    return response;
                }
                catch (ServiceRequestException) when (attempt < retries && (!gateTransportFailures || Safe(request)))
                {
                }
            }
        }

        private static bool Safe(Request request) =>
            request.Body is null
                ? request.Method == Method.Get || request.Method == Method.Head || request.Method == Method.Put || request.Method == Method.Delete || request.Method == Method.Options
                : request.Body.IsReplayable;
    }

    /// <summary>A step that remembers the request in a field shared by every call and forwards what the field holds (XCUT-11). Every call waits for all the others first, so the cross-talk is certain.</summary>
    internal sealed class HoldsRequestInAField : HttpPipelinePolicy
    {
        private readonly TaskCompletionSource _everyoneArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Request? _last;
        private int _arrived;

        public override PipelineStage Stage => PipelineStage.PerAttempt;

        public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            _last = request;
            if (Interlocked.Increment(ref _arrived) == InvariantOptions.Concurrency)
            {
                _everyoneArrived.TrySetResult();
            }

            await _everyoneArrived.Task;
            return await continuation.RunAsync(_last, context);
        }
    }

    /// <summary>A cause walk with no visited set, lazily and without end over a cycle (XCUT-9).</summary>
    /// <param name="exception">The exception to walk.</param>
    internal static IEnumerable<Exception> WalkWithoutVisitedSet(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    /// <summary>A cause walk that tracks visited causes with the default equality comparer (XCUT-9).</summary>
    /// <param name="exception">The exception to walk.</param>
    internal static IEnumerable<Exception> WalkByEquals(Exception exception)
    {
        var seen = new HashSet<Exception>();
        for (var current = exception; current is not null && seen.Add(current); current = current.InnerException)
        {
            yield return current;
        }
    }
}
