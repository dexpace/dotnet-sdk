// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// An auth-stage policy that sends nothing preemptively and answers a <c>401</c> challenge through an
/// <see cref="IChallengeHandler"/> (Digest, or Basic on challenge).
/// </summary>
/// <remarks>
/// <para>
/// <c>new ChallengeAuthPolicy(new DigestChallengeHandler(credential))</c> is the shape issue #2 asks for;
/// <c>new CompositeChallengeHandler(digest, basic)</c> is its fallback. The handler is called with
/// <c>proxy: false</c>. The client descriptor is <c>[Digest, Basic]</c>.
/// </para>
/// <para>
/// The lifecycle (HTTPS guard, cross-origin suppression, the single replay and its replayability gate) is
/// <see cref="AuthorizationPolicy"/>'s.
/// </para>
/// </remarks>
public sealed class ChallengeAuthPolicy : AuthorizationPolicy
{
    private readonly IChallengeHandler _handler;

    /// <summary>Initializes the policy.</summary>
    /// <param name="handler">The handler that answers a challenge.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public ChallengeAuthPolicy(IChallengeHandler handler)
        : base(
            new AuthDescriptor(new AuthRequirement(AuthScheme.Digest), new AuthRequirement(AuthScheme.Basic)),
            [AuthScheme.Digest, AuthScheme.Basic])
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<HttpHeaderName> WithheldHeaderNames { get; } = [HttpHeaderName.WellKnown.Authorization];

    /// <inheritdoc/>
    protected override ValueTask<(string HeaderName, string HeaderValue)?> GetCredentialAsync(
        AuthRequirement requirement,
        Request request,
        PipelineContext context) =>
        new((ValueTuple<string, string>?)null);

    /// <inheritdoc/>
    protected override (string HeaderName, string HeaderValue)? GetCredential(
        AuthRequirement requirement,
        Request request,
        PipelineContext context) =>
        null;

    /// <inheritdoc/>
    protected override ValueTask<Request?> OnChallengeAsync(AuthChallengeContext challenge) => new(OnChallenge(challenge));

    /// <inheritdoc/>
    protected override Request? OnChallenge(AuthChallengeContext challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return _handler.Authorize(challenge.Challenges, challenge.Request, proxy: false);
    }
}
