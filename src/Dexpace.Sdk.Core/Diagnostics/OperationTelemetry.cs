// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The operation span and the per-attempt and per-hop events phase 6 emits through (OBS-21, OBS-25, OBS-28, OBS-29;
/// design 5c positions A to E, P5c-2 to P5c-7).
/// </summary>
/// <remarks>
/// <para>
/// One <see cref="ActivityKind.Internal"/> span opens when a call enters <c>HttpPipeline</c> and ends exactly once when the
/// call returns its response or throws. Every mutator is a no-op on a <see langword="null"/> span and skips a span that is
/// not recording (<see cref="Activity.IsAllDataRequested"/>), so an untraced call allocates nothing. Nothing here catches
/// a listener callback (OBS-20, OBS-30, design §11 item 37).
/// </para>
/// <para>
/// The events are attached to the operation span because the attempt span has already ended when the retry policy decides
/// (<c>Retry</c> sits outside <c>Diagnostics</c>). The retry policy records a spent budget on the call state and the failure
/// path emits <c>dexpace.retry.exhausted</c> immediately before the <c>exception</c> event, so the pairing OBS-29 asks for
/// holds by construction (P5c-6).
/// </para>
/// </remarks>
internal static class OperationTelemetry
{
    private static readonly RedactionCache s_redaction = new();

    /// <summary>Opens the operation span, or returns <see langword="null"/> when the source has no listener.</summary>
    /// <param name="seed">The call's seed request.</param>
    /// <param name="options">The call's client options (the URL redaction allow-list).</param>
    /// <returns>The started span, or <see langword="null"/>.</returns>
    internal static Activity? Start(Request seed, DexpaceClientOptions options)
    {
        var activity = DexpaceDiagnostics.ActivitySource.StartActivity(
            HttpSemanticConventions.SpanName(seed.Method),
            ActivityKind.Internal);
        if (activity is { IsAllDataRequested: true })
        {
            SetSeedTags(activity, seed, options);
        }

        return activity;
    }

    /// <summary>Records the final response of a successful operation. The status stays <c>Unset</c> (P5c-5).</summary>
    /// <param name="activity">The operation span, or <see langword="null"/>.</param>
    /// <param name="response">The response the call returns.</param>
    internal static void Complete(Activity? activity, Response response)
    {
        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag(HttpSemanticConventions.ResponseStatusCode, HttpSemanticConventions.BoxedStatusCode(response.Status.Code));
        }
    }

    /// <summary>
    /// Records a failed operation: <c>dexpace.retry.exhausted</c> when the retry policy recorded a spent budget, the
    /// <c>exception</c> event, <c>error.type</c> and the error status, in that order (OBS-29).
    /// </summary>
    /// <param name="activity">The operation span, or <see langword="null"/>.</param>
    /// <param name="exception">The exception the call throws.</param>
    /// <param name="state">The call state, or <see langword="null"/> when the call failed before one existed.</param>
    internal static void Fail(Activity? activity, Exception exception, CallState? state)
    {
        if (activity is not { IsAllDataRequested: true })
        {
            return;
        }

        var errorType = HttpSemanticConventions.ErrorType(exception);
        if (state is not null && state.TryGetExhaustion(out var attempts))
        {
            activity.AddEvent(new ActivityEvent(
                HttpSemanticConventions.RetryExhaustedEvent,
                tags: new ActivityTagsCollection
                {
                    { HttpSemanticConventions.RetryAttempts, attempts },
                    { HttpSemanticConventions.ErrorTypeKey, errorType },
                }));
        }

        // The stack trace is the exception's own, never ToString(): an SdkException renders its suppressed trail there
        // (design 5c P5c-5, pre-flight V3).
        var tags = new ActivityTagsCollection
        {
            { HttpSemanticConventions.ExceptionType, errorType },
            { HttpSemanticConventions.ExceptionMessage, exception.Message },
        };
        if (exception.StackTrace is { } stackTrace)
        {
            tags.Add(HttpSemanticConventions.ExceptionStackTrace, stackTrace);
        }

        activity.AddEvent(new ActivityEvent(HttpSemanticConventions.ExceptionEvent, tags: tags));
        activity.SetTag(HttpSemanticConventions.ErrorTypeKey, errorType);
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    /// <summary>
    /// Ends the operation span, once. When neither <see cref="Complete"/> nor <see cref="Fail"/> ran (a fatal exception
    /// passed through) the status is <c>Error</c> with no description and no exception event.
    /// </summary>
    /// <param name="activity">The operation span, or <see langword="null"/>.</param>
    /// <param name="settled">Whether <see cref="Complete"/> or <see cref="Fail"/> ran.</param>
    internal static void Stop(Activity? activity, bool settled)
    {
        if (activity is null || activity.IsStopped)
        {
            return;
        }

        if (!settled && activity.IsAllDataRequested)
        {
            activity.SetStatus(ActivityStatusCode.Error);
        }

        activity.Dispose();
    }

    /// <summary>Starts a retry sequence: clears an earlier sequence's exhaustion record (state, not telemetry).</summary>
    /// <param name="context">The call's context.</param>
    internal static void RetrySequenceStarted(PipelineContext context) => context.State.ClearExhaustion();

    /// <summary>Records on the call state that a retry sequence ended with its budget spent (emitted only if the call fails).</summary>
    /// <param name="context">The call's context.</param>
    /// <param name="attempts">The transmissions in the exhausted sequence.</param>
    internal static void RetriesExhausted(PipelineContext context, int attempts) => context.State.RecordExhaustion(attempts);

    /// <summary>Emits <c>dexpace.attempt.failed</c>: an attempt failed and another follows after <paramref name="nextDelay"/>.</summary>
    /// <param name="context">The call's context.</param>
    /// <param name="response">The failed attempt's response, if it had one.</param>
    /// <param name="failure">The failed attempt's exception, if it had one.</param>
    /// <param name="nextDelay">The wait before the next attempt.</param>
    internal static void AttemptFailed(PipelineContext context, Response? response, Exception? failure, TimeSpan nextDelay)
    {
        if (context.Instrumentation.ActiveSpan is not { IsAllDataRequested: true } operation)
        {
            return;
        }

        var tags = new ActivityTagsCollection
        {
            { HttpSemanticConventions.RequestResendCount, Math.Max(0, context.State.Transmissions - 1) },
        };
        if (response is not null)
        {
            var code = response.Status.Code;
            tags.Add(HttpSemanticConventions.ErrorTypeKey, HttpSemanticConventions.ErrorType(code));
            tags.Add(HttpSemanticConventions.ResponseStatusCode, HttpSemanticConventions.BoxedStatusCode(code));
        }
        else if (failure is not null)
        {
            tags.Add(HttpSemanticConventions.ErrorTypeKey, HttpSemanticConventions.ErrorType(failure));
        }

        tags.Add(HttpSemanticConventions.RetryDelay, nextDelay.TotalSeconds);
        operation.AddEvent(new ActivityEvent(HttpSemanticConventions.AttemptFailedEvent, tags: tags));
    }

    /// <summary>Emits <c>dexpace.redirect.hop</c>: a redirect is being followed. Called by the redirect policy (6b).</summary>
    /// <param name="context">The call's context.</param>
    /// <param name="hop">The 1-based number of the new hop.</param>
    /// <param name="statusCode">The redirect status that triggered it.</param>
    /// <param name="target">The redirect target; it is redacted here.</param>
    /// <param name="crossOrigin">Whether the target is another origin.</param>
    internal static void RedirectHop(PipelineContext context, int hop, int statusCode, Uri target, bool crossOrigin)
    {
        if (context.Instrumentation.ActiveSpan is not { IsAllDataRequested: true } operation)
        {
            return;
        }

        operation.AddEvent(new ActivityEvent(
            HttpSemanticConventions.RedirectHopEvent,
            tags: new ActivityTagsCollection
            {
                { HttpSemanticConventions.RedirectHop, hop },
                { HttpSemanticConventions.ResponseStatusCode, HttpSemanticConventions.BoxedStatusCode(statusCode) },
                { HttpSemanticConventions.UrlFull, s_redaction.Get(context.Options.Logging).Redactor.Redact(target) },
                { HttpSemanticConventions.RedirectCrossOrigin, crossOrigin },
            }));
    }

    private static void SetSeedTags(Activity activity, Request seed, DexpaceClientOptions options)
    {
        var url = seed.Url;
        activity.SetTag(HttpSemanticConventions.RequestMethod, HttpSemanticConventions.MethodValue(seed.Method));
        if (HttpSemanticConventions.IsOther(seed.Method))
        {
            activity.SetTag(HttpSemanticConventions.RequestMethodOriginal, seed.Method.Name);
        }

        activity.SetTag(HttpSemanticConventions.ServerAddress, url.Host);
        activity.SetTag(HttpSemanticConventions.ServerPort, url.Port);
        activity.SetTag(HttpSemanticConventions.UrlFull, s_redaction.Get(options.Logging).Redactor.Redact(url));
    }
}
