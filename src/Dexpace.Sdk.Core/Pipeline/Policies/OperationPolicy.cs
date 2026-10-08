// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// The outermost pipeline policy. Applies the overall-operation timeout configured in
/// <see cref="Configuration.DexpaceClientOptions.OverallTimeout"/> by linking a
/// <see cref="CancellationTokenSource"/> to the caller's token before forwarding the call.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="Configuration.DexpaceClientOptions.OverallTimeout"/> is set, a new <see cref="CancellationTokenSource"/>
/// is armed on the policy's <see cref="TimeProvider"/> (so a fake clock drives it), and a copy of the context carrying the
/// linked token (<see cref="PipelineContext.WithCancellationToken"/>) is passed downstream so that all policies further
/// down the chain, including the transport, observe the deadline. The source is disposed after the call completes.
/// </para>
/// <para>
/// <b>A deadline is a timeout, not a cancellation.</b> If the deadline fires while the caller's token is still unsignalled,
/// the policy throws <see cref="OperationTimeoutException"/> (not retryable) carrying the cancellation as its inner
/// exception and the failed attempts' trail as suppressed exceptions. A call the caller cancelled still surfaces
/// <see cref="OperationCanceledException"/> (XCUT-1).
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> an expired deadline surfaces <see cref="OperationTimeoutException"/> (was
/// <see cref="OperationCanceledException"/> or <c>TaskCanceledException</c>); and a non-positive
/// <see cref="Configuration.DexpaceClientOptions.OverallTimeout"/>, which used to mean "none", is rejected where it is set.
/// </para>
/// <para>
/// <b>Breaking (binary):</b> the parameterless constructor became the constructor with an optional
/// <see cref="TimeProvider"/>; source that calls <c>new OperationPolicy()</c> still compiles.
/// </para>
/// <para>
/// <b>The operation span is not opened here.</b> <c>HttpPipeline</c> opens it at call entry, around this policy, so it
/// exists for every pipeline shape and a replaced <c>Operation</c> pillar cannot lose it (OBS-29, phase 5c, P5c-2). A
/// deadline that fires is a failure of that span.
/// </para>
/// </remarks>
public sealed class OperationPolicy : HttpPipelinePolicy
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new <see cref="OperationPolicy"/>.
    /// </summary>
    /// <param name="timeProvider">The clock that arms the deadline, or <see langword="null"/> for the system clock.</param>
    public OperationPolicy(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.Operation;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(OperationPolicy));

    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Options.OverallTimeout is not { } timeout)
        {
            return async
                ? await continuation.RunAsync(request, context).ConfigureAwait(false)
                : continuation.Run(request, context);
        }

        // The deadline-linked token travels downstream on a copy of the context, never back up (PIPE-16).
        using var deadline = new CancellationTokenSource(timeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, deadline.Token);
        var downstream = context.WithCancellationToken(linked.Token);
        try
        {
            return async
                ? await continuation.RunAsync(request, downstream).ConfigureAwait(false)
                : continuation.Run(request, downstream);
        }
        catch (OperationCanceledException ex) when (deadline.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
        {
            var timedOut = new OperationTimeoutException($"The operation exceeded its overall timeout of {timeout}.", ex);
            foreach (var attempt in ExceptionTrail.GetSuppressed(ex))
            {
                ExceptionTrail.AddSuppressed(timedOut, attempt);
            }

            throw timedOut;
        }
    }
}
