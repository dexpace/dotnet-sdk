// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A per-call pipeline policy that stamps the <c>User-Agent</c> header on each outgoing request,
/// replacing any value already present.
/// </summary>
/// <remarks>
/// Placed at <see cref="PipelineStage.PerCall"/>, this policy runs once above the retry boundary.
/// The value is taken from <see cref="Configuration.DexpaceClientOptions.UserAgent"/> so
/// callers can override the default without subclassing.
/// </remarks>
public sealed class ClientIdentityPolicy : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.PerCall;

    /// <inheritdoc/>
    public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Request = context.Request with
        {
            Headers = context.Request.Headers.Set(
                HttpHeaderName.WellKnown.UserAgent.Original,
                context.Options.UserAgent)
        };

        await continuation.RunAsync(context).ConfigureAwait(false);
    }
}
