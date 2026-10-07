// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

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
/// When <see cref="Configuration.DexpaceClientOptions.OverallTimeout"/> is a positive
/// <see cref="TimeSpan"/>, a new <see cref="CancellationTokenSource"/> is created, the timeout
/// is armed, and a copy of the context carrying the linked token
/// (<see cref="PipelineContext.WithCancellationToken"/>) is passed downstream so that all policies further down the chain — including the transport — observe the
/// deadline. The CTS is disposed after the call completes (or faults/cancels).
/// </para>
/// <para>
/// <b>Cancellation is not caught.</b> If the deadline fires, <see cref="OperationCanceledException"/>
/// propagates to the caller unchanged. The policy does not distinguish between caller-initiated
/// cancellation and deadline expiry — both surface as <see cref="OperationCanceledException"/>.
/// </para>
/// <para>
/// When no timeout is configured (or the value is non-positive) the policy is transparent:
/// it simply awaits the continuation without allocating a CTS.
/// </para>
/// </remarks>
public sealed class OperationPolicy : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.Operation;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(OperationPolicy));

    private static async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var timeout = context.Options.OverallTimeout;
        if (timeout is not { } ts || ts <= TimeSpan.Zero)
        {
            return async
                ? await continuation.RunAsync(request, context).ConfigureAwait(false)
                : continuation.Run(request, context);
        }

        // The deadline-linked token travels downstream on a copy of the context, never back up (PIPE-16).
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(ts);
        var downstream = context.WithCancellationToken(cts.Token);
        return async
            ? await continuation.RunAsync(request, downstream).ConfigureAwait(false)
            : continuation.Run(request, downstream);
    }
}
