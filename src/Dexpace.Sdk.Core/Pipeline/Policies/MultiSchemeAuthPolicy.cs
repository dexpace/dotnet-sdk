// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// The descriptor-driven, multi-credential auth step a generated SDK installs when its API declares several security schemes
/// (AUTH-4, AUTH-5; P6c-36).
/// </summary>
/// <remarks>
/// <para>
/// For each call the policy resolves the per-call, operation and client tiers against the schemes <see cref="AuthCredentials"/>
/// configures, then stamps <see cref="AuthScheme.OAuth2"/> through the same cache and <c>401</c> logic as
/// <see cref="BearerTokenAuthPolicy"/>, <see cref="AuthScheme.ApiKey"/> and <see cref="AuthScheme.Basic"/> preemptively, and
/// answers <see cref="AuthScheme.Digest"/> reactively through a <see cref="DigestChallengeHandler"/> with the default
/// preference. The single-scheme policies and this one share internal stampers, so no logic is written twice.
/// </para>
/// <para>
/// A per-call or operation <c>RequestOptions.Auth</c> / <c>OperationAuth</c> descriptor overrides the client descriptor; a
/// descriptor naming only schemes with no credential throws <see cref="Errors.AuthResolutionException"/> before anything is
/// sent. The OpenAPI mapping is in <c>docs/sdk-documentation/auth.md</c>.
/// </para>
/// <para>
/// The constructor rejects a client descriptor that can never be served: one that does not allow anonymous calls and names no
/// scheme <see cref="AuthCredentials"/> configures (a pinned reading, flagged for review).
/// </para>
/// </remarks>
public sealed class MultiSchemeAuthPolicy : AuthorizationPolicy
{
    private readonly ApiKeyStamper? _apiKey;
    private readonly BasicStamper? _basic;
    private readonly BearerStamper? _bearer;
    private readonly DigestStamper? _digest;

    /// <summary>Initializes the policy.</summary>
    /// <param name="clientDescriptor">The client tier: the preference order when a call names no tier of its own.</param>
    /// <param name="credentials">The credentials the policy can serve; each member is optional.</param>
    /// <param name="timeProvider">The clock of the token cache; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clientDescriptor"/> or <paramref name="credentials"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The client descriptor can never be served by <paramref name="credentials"/>.</exception>
    public MultiSchemeAuthPolicy(AuthDescriptor clientDescriptor, AuthCredentials credentials, TimeProvider? timeProvider = null)
        : base(RequireServable(clientDescriptor, credentials), AvailableOf(credentials))
    {
        _bearer = credentials.Token is { } token
            ? new BearerStamper(new AccessTokenCache(token, timeProvider) { RefreshMargin = credentials.TokenRefreshMargin }, [])
            : null;
        _apiKey = credentials.ApiKey is { } apiKey ? new ApiKeyStamper(apiKey) : null;
        _basic = credentials.Basic is { } basic ? new BasicStamper(basic) : null;
        _digest = credentials.Digest is { } digest ? new DigestStamper(new DigestChallengeHandler(digest)) : null;
        WithheldHeaderNames = Withheld(credentials);
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<HttpHeaderName> WithheldHeaderNames { get; }

    /// <inheritdoc/>
    protected override async ValueTask<(string HeaderName, string HeaderValue)?> GetCredentialAsync(
        AuthRequirement requirement,
        Request request,
        PipelineContext context)
    {
        if (requirement.Scheme == AuthScheme.OAuth2 && _bearer is not null)
        {
            return await _bearer.StampAsync(requirement, context).ConfigureAwait(false);
        }

        return GetCredential(requirement, request, context);
    }

    /// <inheritdoc/>
    protected override (string HeaderName, string HeaderValue)? GetCredential(
        AuthRequirement requirement,
        Request request,
        PipelineContext context) =>
        requirement.Scheme switch
        {
            AuthScheme.OAuth2 => _bearer?.Stamp(requirement, context),
            AuthScheme.ApiKey => _apiKey?.Stamp(),
            AuthScheme.Basic => _basic?.Stamp(),
            _ => null, // Digest is reactive: nothing is attached until the server challenges.
        };

    /// <inheritdoc/>
    protected override async ValueTask<Request?> OnChallengeAsync(AuthChallengeContext challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return challenge.Requirement.Scheme == AuthScheme.OAuth2 && _bearer is not null
            ? await _bearer.OnChallengeAsync(challenge).ConfigureAwait(false)
            : OnChallenge(challenge);
    }

    /// <inheritdoc/>
    protected override Request? OnChallenge(AuthChallengeContext challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        return challenge.Requirement.Scheme switch
        {
            AuthScheme.OAuth2 => _bearer?.OnChallenge(challenge),
            AuthScheme.Digest => _digest?.OnChallenge(challenge),
            _ => null,
        };
    }

    private static AuthDescriptor RequireServable(AuthDescriptor clientDescriptor, AuthCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(clientDescriptor);
        ArgumentNullException.ThrowIfNull(credentials);
        var available = credentials.Available;
        if (!clientDescriptor.AllowsAnonymous && !clientDescriptor.Requirements.Any(r => available.Contains(r.Scheme)))
        {
            throw new ArgumentException(
                "The client descriptor names no authentication scheme that the credentials configure.",
                nameof(credentials));
        }

        return clientDescriptor;
    }

    private static IReadOnlyList<AuthScheme> AvailableOf(AuthCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return credentials.Available;
    }

    // Authorization (bearer, Basic, Digest) plus the API key's own header, when it differs.
    private static HttpHeaderName[] Withheld(AuthCredentials credentials)
    {
        var names = new List<HttpHeaderName> { HttpHeaderName.WellKnown.Authorization };
        if (credentials.ApiKey is { } apiKey && !names.Contains(apiKey.HeaderName))
        {
            names.Add(apiKey.HeaderName);
        }

        return [.. names];
    }
}
