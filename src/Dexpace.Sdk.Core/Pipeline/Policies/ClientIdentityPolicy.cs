// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Recovery;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A per-call pipeline policy that stamps the <c>User-Agent</c> header on each outgoing request. It delegates the
/// composition to a <see cref="ClientIdentityStep"/> (RECOV-33).
/// </summary>
/// <remarks>
/// <para>
/// Placed at <see cref="PipelineStage.PerCall"/>, this policy runs once above the retry boundary. The line is taken
/// from <see cref="Configuration.DexpaceClientOptions.UserAgent"/>, read on every call, so callers can override the
/// default without subclassing. A blank value emits no header.
/// </para>
/// <para>
/// <b>Breaking:</b> a caller-supplied <c>User-Agent</c> used to be replaced. By default the SDK line is now appended
/// after the first existing value (<see cref="ClientIdentityMode.Append"/>); <c>new
/// ClientIdentityPolicy(ClientIdentityMode.Replace)</c> restores the old behaviour. A blank <c>UserAgent</c> used to be
/// stamped as an empty value and now emits no header.
/// </para>
/// </remarks>
public sealed class ClientIdentityPolicy : HttpPipelinePolicy
{
    private readonly ClientIdentityStep _step;

    /// <summary>Initializes a policy in <see cref="ClientIdentityMode.Append"/> mode.</summary>
    public ClientIdentityPolicy()
        : this(ClientIdentityMode.Append)
    {
    }

    /// <summary>Initializes a policy in the given mode.</summary>
    /// <param name="mode">How the line is composed with an existing <c>User-Agent</c>.</param>
    public ClientIdentityPolicy(ClientIdentityMode mode)
    {
        _step = new ClientIdentityStep([]) { Mode = mode };
    }

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.PerCall;

    /// <inheritdoc/>
    public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Request = _step.Compose(context.Request, context.Options.UserAgent);

        await continuation.RunAsync(context).ConfigureAwait(false);
    }
}
