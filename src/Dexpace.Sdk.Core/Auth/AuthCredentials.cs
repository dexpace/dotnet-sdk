// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// The credentials a multi-scheme auth policy can serve, one optional member per scheme (AUTH-8).
/// </summary>
/// <remarks>
/// <see cref="ToString"/> lists which schemes are configured and never renders a member (P6c-20).
/// </remarks>
public sealed record AuthCredentials
{
    private static readonly TimeSpan s_defaultMargin = TimeSpan.FromSeconds(30);

    private TimeSpan _tokenRefreshMargin = s_defaultMargin;

    /// <summary>The OAuth 2.0 token credential, or <see langword="null"/>.</summary>
    public TokenCredential? Token { get; init; }

    /// <summary>The API-key credential, or <see langword="null"/>.</summary>
    public ApiKeyCredential? ApiKey { get; init; }

    /// <summary>The Basic credential, or <see langword="null"/>.</summary>
    public BasicCredential? Basic { get; init; }

    /// <summary>The Digest credential, or <see langword="null"/>.</summary>
    public DigestCredential? Digest { get; init; }

    /// <summary>How long before expiry a bearer token is refreshed in the background; 30 seconds by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan TokenRefreshMargin
    {
        get => _tokenRefreshMargin;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            _tokenRefreshMargin = value;
        }
    }

    /// <summary>The schemes that have a credential, in enum order.</summary>
    internal IReadOnlyList<AuthScheme> Available
    {
        get
        {
            var schemes = new List<AuthScheme>(4);
            if (Token is not null)
            {
                schemes.Add(AuthScheme.OAuth2);
            }

            if (ApiKey is not null)
            {
                schemes.Add(AuthScheme.ApiKey);
            }

            if (Basic is not null)
            {
                schemes.Add(AuthScheme.Basic);
            }

            if (Digest is not null)
            {
                schemes.Add(AuthScheme.Digest);
            }

            return schemes;
        }
    }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Schemes = [").Append(string.Join(", ", Available)).Append(']');
        return true;
    }
}
