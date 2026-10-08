// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// The origin comparison the authorization step uses for both the cross-origin withholding rule (AUTH-29) and the check
/// on a challenge replacement.
/// </summary>
internal static class AuthOrigin
{
    // Derives a canonical origin string: "<lower-scheme>://<lower-host>:<port>". Port is always included. For a URL
    // written without a port, Uri.Port returns the scheme's default (443 for https, 80 for http), not -1, so
    // "https://a/" and "https://a:443/" yield the same origin whether or not the caller supplied the port explicitly.
    internal static string Of(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}:{uri.Port}";
    }

    // 6b's HttpOrigin is the one comparison (it folds an IDN host and its punycode spelling into one origin).
    internal static bool Same(Uri a, Uri b) => HttpOrigin.From(a) == HttpOrigin.From(b);
}
