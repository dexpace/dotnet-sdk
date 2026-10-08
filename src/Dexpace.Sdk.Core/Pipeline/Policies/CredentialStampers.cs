// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

// The per-scheme logic shared by the single-scheme policies and MultiSchemeAuthPolicy (P6c-36), so none of it is written
// twice. Each stamper is immutable after construction; the bearer stamper's cache is the only state.

/// <summary>Stamps an API key in its own header (AUTH-26).</summary>
internal sealed class ApiKeyStamper(ApiKeyCredential credential)
{
    internal HttpHeaderName HeaderName => credential.HeaderName;

    internal (string HeaderName, string HeaderValue) Stamp() => (credential.HeaderName.Original, credential.HeaderValue);
}

/// <summary>Stamps preemptive Basic (AUTH-14).</summary>
internal sealed class BasicStamper(BasicCredential credential)
{
    internal (string HeaderName, string HeaderValue) Stamp() => (HttpHeaderName.WellKnown.Authorization.Original, credential.HeaderValue);
}

/// <summary>Answers a Digest challenge reactively; nothing is stamped on the first pass.</summary>
internal sealed class DigestStamper(IChallengeHandler handler)
{
    internal Request? OnChallenge(AuthChallengeContext challenge) =>
        handler.Authorize(challenge.Challenges, challenge.Request, proxy: false);
}

/// <summary>Stamps a bearer token from the cache and re-fetches once on a Bearer challenge (AUTH-34 to AUTH-37).</summary>
internal sealed class BearerStamper(AccessTokenCache cache, string[] fallbackScopes)
{
    // The resolved requirement's scopes win (a per-call tier may ask for others); the policy's own are the fallback.
    internal TokenRequestContext ContextFor(AuthRequirement requirement) =>
        new(requirement.Scopes.Count > 0 ? requirement.Scopes : fallbackScopes);

    internal async ValueTask<(string HeaderName, string HeaderValue)?> StampAsync(AuthRequirement requirement, PipelineContext context)
    {
        var token = await cache
            .GetAsync(ContextFor(requirement), context.State.Logger, context.CancellationToken)
            .ConfigureAwait(false);
        return (HttpHeaderName.WellKnown.Authorization.Original, Bearer(token));
    }

    internal (string HeaderName, string HeaderValue)? Stamp(AuthRequirement requirement, PipelineContext context)
    {
        var token = cache.Get(ContextFor(requirement), context.State.Logger, context.CancellationToken);
        return (HttpHeaderName.WellKnown.Authorization.Original, Bearer(token));
    }

    internal async ValueTask<Request?> OnChallengeAsync(AuthChallengeContext challenge)
    {
        if (RejectedHeader(challenge) is not { } rejected)
        {
            return null;
        }

        var token = await cache
            .GetAfterRejectionAsync(ContextFor(challenge.Requirement), rejected, challenge.Context.CancellationToken)
            .ConfigureAwait(false);
        return Replacement(challenge.Request, rejected, token);
    }

    internal Request? OnChallenge(AuthChallengeContext challenge)
    {
        if (RejectedHeader(challenge) is not { } rejected)
        {
            return null;
        }

        var token = cache.GetAfterRejection(ContextFor(challenge.Requirement), rejected, challenge.Context.CancellationToken);
        return Replacement(challenge.Request, rejected, token);
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
}
