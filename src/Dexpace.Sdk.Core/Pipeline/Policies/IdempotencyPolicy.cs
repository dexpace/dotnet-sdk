// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.Recovery;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A per-call pipeline policy that attaches an <c>Idempotency-Key</c> header to outgoing requests, enabling safe retries
/// on transient failures. It delegates the rule to an <see cref="IdempotencyKeyStep"/> (RECOV-32).
/// </summary>
/// <remarks>
/// <para>
/// By default <c>POST</c>, <c>PUT</c> and <c>PATCH</c> requests receive the header. The key is minted by the step's
/// strategy once per logical call and stashed in the <see cref="PipelineContext"/> property bag under a typed
/// <see cref="PipelinePropertyKey{T}"/>. The policy runs at <see cref="PipelineStage.PerCall"/>, once per call outside
/// the redirect and retry loops, and every hop and attempt carries the stamped request, so the strategy runs at most once
/// per call.
/// </para>
/// <para>
/// If the request already carries an <c>Idempotency-Key</c> header, the policy does not overwrite it (unless the step is
/// configured with <see cref="IdempotencyKeyStep.RespectExisting"/> set to <see langword="false"/>).
/// </para>
/// <para>
/// <b>Breaking:</b> the constructor was <c>IdempotencyPolicy(IEnumerable&lt;Method&gt;? methods = null)</c>; it is now
/// <c>IdempotencyPolicy()</c> and <c>IdempotencyPolicy(IdempotencyKeyStep)</c>, so the method set is configured on the
/// step. <b>Breaking:</b> the default method set was <c>POST</c> only; it is now <c>POST</c>, <c>PUT</c> and
/// <c>PATCH</c> (RECOV-32).
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> the policy runs once per call, outside the redirect loop; it used to run once per hop
/// (<see cref="PipelineStage.PerCall"/> moved from 250 to 150, P4c-23).
/// </para>
/// </remarks>
public sealed class IdempotencyPolicy : HttpPipelinePolicy
{
    // The call-scoped property-bag key under which the generated idempotency key is stored (PIPE-11, P4c-4).
    private static readonly PipelinePropertyKey<string> s_keyProperty = new("dexpace.idempotency-key");

    private readonly IdempotencyKeyStep _step;

    /// <summary>Initializes a policy over a default <see cref="IdempotencyKeyStep"/>.</summary>
    public IdempotencyPolicy()
        : this(new IdempotencyKeyStep())
    {
    }

    /// <summary>Initializes a policy over <paramref name="step"/>.</summary>
    /// <param name="step">The step that decides which requests get a key, and mints it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="step"/> is <see langword="null"/>.</exception>
    public IdempotencyPolicy(IdempotencyKeyStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _step = step;
    }

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.PerCall;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(IdempotencyPolicy));

    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var stamped = _step.Apply(request, () => KeyFor(context));

        return async
            ? await continuation.RunAsync(stamped, context).ConfigureAwait(false)
            : continuation.Run(stamped, context);
    }

    // Reuse a key already minted for this call, else mint and stash one.
    private string KeyFor(PipelineContext context)
    {
        if (context.TryGetProperty(s_keyProperty, out var existing))
        {
            return existing;
        }

        var key = _step.KeyStrategy();
        if (!string.IsNullOrWhiteSpace(key))
        {
            context.SetProperty(s_keyProperty, key);
        }

        return key;
    }
}
