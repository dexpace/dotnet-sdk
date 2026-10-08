// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// The RFC 6454 origin triple of an absolute URI: lower-case scheme, lower-case IDN-normalised host and effective port.
/// Userinfo, path, query and fragment never participate (REDIR-8, REDIR-11). Offered to the auth policies (P6b-12).
/// </summary>
/// <param name="Scheme">The lower-case scheme.</param>
/// <param name="Host">The lower-case host, in its punycode (<see cref="Uri.IdnHost"/>) spelling.</param>
/// <param name="Port">The effective port: an absent port is the scheme's default.</param>
internal readonly record struct HttpOrigin(string Scheme, string Host, int Port)
{
    /// <summary>Builds the origin of <paramref name="uri"/>.</summary>
    /// <param name="uri">An absolute URI.</param>
    /// <returns>Its origin triple.</returns>
    /// <exception cref="ArgumentException"><paramref name="uri"/> is not absolute.</exception>
    internal static HttpOrigin From(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("An origin needs an absolute URI.", nameof(uri));
        }

        // IdnHost, not Host: an IDN host and its punycode spelling are one origin.
        return new HttpOrigin(uri.Scheme.ToLowerInvariant(), uri.IdnHost.ToLowerInvariant(), uri.Port);
    }
}
