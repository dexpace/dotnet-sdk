// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// Answers a <c>Basic</c> challenge (any realm) with the credential (RFC 7617, AUTH-14).
/// </summary>
/// <remarks>
/// The header value is precomputed at construction and is the same string <c>BasicAuthPolicy</c> stamps preemptively.
/// </remarks>
public sealed class BasicChallengeHandler : IChallengeHandler
{
    private readonly string _headerValue;

    /// <summary>Initializes the handler.</summary>
    /// <param name="credential">The credential to answer with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is <see langword="null"/>.</exception>
    public BasicChallengeHandler(BasicCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _headerValue = credential.HeaderValue;
    }

    /// <inheritdoc/>
    public Request? Authorize(IReadOnlyList<AuthenticationChallenge> challenges, Request request, bool proxy)
    {
        ArgumentNullException.ThrowIfNull(challenges);
        ArgumentNullException.ThrowIfNull(request);
        foreach (var challenge in challenges)
        {
            if (string.Equals(challenge.Scheme, "basic", StringComparison.Ordinal))
            {
                var header = proxy ? HttpHeaderName.WellKnown.ProxyAuthorization : HttpHeaderName.WellKnown.Authorization;
                return request.WithHeaders(request.Headers.Set(header.Original, _headerValue));
            }
        }

        return null;
    }
}
