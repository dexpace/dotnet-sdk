// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Recovery;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A per-call pipeline policy that attaches an <c>Idempotency-Key</c> header to outgoing requests, enabling safe retries
/// on transient failures. It delegates the rule to an <see cref="IdempotencyKeyStep"/> (RECOV-32).
/// </summary>
/// <remarks>
/// <para>
/// By default <c>POST</c>, <c>PUT</c> and <c>PATCH</c> requests receive the header. The key is minted by the step's
/// strategy once per logical call and stashed in the <see cref="PipelineContext"/> property bag under the key
/// <c>"dexpace.idempotency-key"</c>. Redirect hops and retry attempts that re-enter the policy on the same context reuse
/// the same key, so the strategy runs at most once per call.
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
/// </remarks>
public sealed class IdempotencyPolicy : HttpPipelinePolicy
{
    /// <summary>Context property-bag key under which the generated idempotency key is stored.</summary>
    internal const string PropertyKey = "dexpace.idempotency-key";

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
    public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Request = _step.Apply(context.Request, () => KeyFor(context));

        await continuation.RunAsync(context).ConfigureAwait(false);
    }

    // Reuse a key already minted for this call (a retry or redirect hop re-entering here), else mint and stash one.
    private string KeyFor(PipelineContext context)
    {
        var key = context.GetProperty<string>(PropertyKey);
        if (key is not null)
        {
            return key;
        }

        key = _step.KeyStrategy();
        if (!string.IsNullOrWhiteSpace(key))
        {
            context.SetProperty(PropertyKey, key);
        }

        return key;
    }
}
