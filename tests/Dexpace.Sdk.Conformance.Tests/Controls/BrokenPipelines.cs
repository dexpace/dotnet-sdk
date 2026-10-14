// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
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
}
