// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// An auth-stage pipeline policy that obtains an OAuth 2.0 bearer token via an
/// <see cref="AccessTokenCache"/> and stamps <c>Authorization: Bearer &lt;token&gt;</c> on every
/// outgoing request, replacing any prior <c>Authorization</c> header value.
/// </summary>
/// <remarks>
/// <para>
/// The policy creates exactly one <see cref="AccessTokenCache"/> for the lifetime of the policy
/// instance, wrapping the supplied <see cref="TokenCredential"/>, or uses the one a caller hands it. All pipeline
/// invocations share that cache, so concurrent or sequential requests reuse a valid cached token, a token near expiry is
/// refreshed in the background, and a burst at expiry costs one fetch (AUTH-34, AUTH-37).
/// </para>
/// <para>
/// Both paths are real: the asynchronous path awaits <see cref="AccessTokenCache.GetAsync(TokenRequestContext,CancellationToken)"/>
/// and the synchronous path calls <see cref="AccessTokenCache.Get(TokenRequestContext,CancellationToken)"/>, so no thread is blocked on a task. The scopes of
/// the resolved requirement are used (a per-call OAuth2 requirement may ask for others), falling back to the
/// policy's.
/// </para>
/// <para>
/// <b>401 re-acquisition:</b> a <c>401</c> whose <c>WWW-Authenticate</c> carries a <c>Bearer</c> challenge evicts the
/// rejected token (only if it is still the cached one), fetches a fresh one and retries the call once, whatever the
/// method; the base's replayability gate protects a body that cannot be written again (AUTH-31, AUTH-36). If the provider
/// hands back the very token that was rejected, the <c>401</c> is returned. A second <c>401</c> is returned as it is
/// (AUTH-30).
/// </para>
/// <para>
/// Credentials are withheld when the request has been redirected to a different origin; see
/// <see cref="AuthorizationPolicy"/> for the cross-origin withholding contract.
/// </para>
/// <para>
/// <b>Breaking:</b> a <c>401</c> with a <c>Bearer</c> challenge is now retried once with a fresh token (was: returned
/// to the caller).
/// </para>
/// </remarks>
public sealed class BearerTokenAuthPolicy : AuthorizationPolicy
{
    private readonly AccessTokenCache _cache;
    private readonly string[] _scopes;

    /// <summary>
    /// Initializes a <see cref="BearerTokenAuthPolicy"/> with the given credential and scopes.
    /// </summary>
    /// <param name="credential">
    /// The token credential used to obtain bearer tokens. A single
    /// <see cref="AccessTokenCache"/> is created over this credential and shared across all
    /// requests.
    /// </param>
    /// <param name="scopes">
    /// The OAuth 2.0 scopes to request. Passed to
    /// <see cref="TokenCredential.GetTokenAsync"/> via <see cref="TokenRequestContext"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="credential"/> or <paramref name="scopes"/> is <see langword="null"/>.
    /// </exception>
    public BearerTokenAuthPolicy(TokenCredential credential, params string[] scopes)
        : this(CacheOver(credential), scopes)
    {
    }

    /// <summary>
    /// Initializes a <see cref="BearerTokenAuthPolicy"/> over an existing cache, so a caller can set the refresh margin and
    /// the <see cref="TimeProvider"/>, or share one cache between policies.
    /// </summary>
    /// <param name="cache">The token cache.</param>
    /// <param name="scopes">The OAuth 2.0 scopes to request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cache"/> or <paramref name="scopes"/> is <see langword="null"/>.</exception>
    public BearerTokenAuthPolicy(AccessTokenCache cache, params string[] scopes)
        : base(ClientDescriptor(scopes), [AuthScheme.OAuth2])
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
        _scopes = [.. scopes];
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<HttpHeaderName> WithheldHeaderNames { get; } = [HttpHeaderName.WellKnown.Authorization];

    /// <inheritdoc/>
    protected override async ValueTask<(string HeaderName, string HeaderValue)?> GetCredentialAsync(
        AuthRequirement requirement,
        Request request,
        PipelineContext context)
    {
        var token = await _cache
            .GetAsync(TokenContextFor(requirement), context.State.Logger, context.CancellationToken)
            .ConfigureAwait(false);

        return (HttpHeaderName.WellKnown.Authorization.Original, Bearer(token));
    }

    /// <inheritdoc/>
    protected override (string HeaderName, string HeaderValue)? GetCredential(
        AuthRequirement requirement,
        Request request,
        PipelineContext context)
    {
        var token = _cache.Get(TokenContextFor(requirement), context.State.Logger, context.CancellationToken);
        return (HttpHeaderName.WellKnown.Authorization.Original, Bearer(token));
    }

    /// <inheritdoc/>
    protected override async ValueTask<Request?> OnChallengeAsync(AuthChallengeContext challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        if (RejectedHeader(challenge) is not { } rejected)
        {
            return null;
        }

        var token = await _cache
            .GetAfterRejectionAsync(TokenContextFor(challenge.Requirement), rejected, challenge.Context.CancellationToken)
            .ConfigureAwait(false);
        return Replacement(challenge.Request, rejected, token);
    }

    /// <inheritdoc/>
    protected override Request? OnChallenge(AuthChallengeContext challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        if (RejectedHeader(challenge) is not { } rejected)
        {
            return null;
        }

        var token = _cache.GetAfterRejection(TokenContextFor(challenge.Requirement), rejected, challenge.Context.CancellationToken);
        return Replacement(challenge.Request, rejected, token);
    }

    private static AccessTokenCache CacheOver(TokenCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        return new AccessTokenCache(credential);
    }

    private static string Bearer(AccessToken token) => "Bearer " + token.Token;

    // Only a Bearer challenge to a request that carried an Authorization header is answered (AUTH-36).
    private static string? RejectedHeader(AuthChallengeContext challenge) =>
        challenge.Challenges.Any(static c => string.Equals(c.Scheme, "bearer", StringComparison.Ordinal))
            ? challenge.Request.Headers.Get(HttpHeaderName.WellKnown.Authorization)
            : null;

    // A provider that hands back the rejected token yields no retry (P6c-27): the 401 surfaces.
    private static Request? Replacement(Request sent, string rejected, AccessToken token)
    {
        var value = Bearer(token);
        return string.Equals(value, rejected, StringComparison.Ordinal)
            ? null
            : sent.WithHeaders(sent.Headers.Set(HttpHeaderName.WellKnown.Authorization, value));
    }

    private static AuthDescriptor ClientDescriptor(string[] scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        return new AuthDescriptor(new AuthRequirement(AuthScheme.OAuth2) { Scopes = scopes });
    }

    // The resolved requirement's scopes win (a per-call tier may ask for others); the policy's own are the fallback.
    private TokenRequestContext TokenContextFor(AuthRequirement requirement) =>
        new(requirement.Scopes.Count > 0 ? requirement.Scopes : _scopes);
}
