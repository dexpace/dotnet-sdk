// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// The total <c>Location</c> resolver (REDIR-12, REDIR-13, REDIR-14, REDIR-18, REDIR-19; P6b-10): whatever the server sent,
/// it yields a dispatchable, userinfo-free http(s) <see cref="Uri"/>, or says why not. It never throws.
/// </summary>
internal static class RedirectLocation
{
    /// <summary>Resolves the <c>Location</c> of a response against the current hop's URL.</summary>
    /// <param name="current">The URL of the hop that produced the response (never the seed: REDIR-14).</param>
    /// <param name="headers">The response headers.</param>
    /// <param name="target">The resolved target when the result is <see cref="LocationOutcome.Resolved"/>.</param>
    /// <param name="malformedRaw">The raw value(s) when the result is <see cref="LocationOutcome.Malformed"/>.</param>
    /// <returns>The outcome.</returns>
    internal static LocationOutcome TryResolve(Uri current, Headers headers, out Uri? target, out string? malformedRaw)
    {
        target = null;
        malformedRaw = null;
        var values = headers.GetAll(HttpHeaderName.WellKnown.Location);
        if (values.Count == 0 || (values.Count == 1 && string.IsNullOrWhiteSpace(values[0])))
        {
            return LocationOutcome.Absent;
        }

        if (values.Count > 1)
        {
            // Location is a singleton field (RFC 9110 section 10.2.2): two values are an ambiguous instruction.
            malformedRaw = string.Join(", ", values);
            return LocationOutcome.Malformed;
        }

        var raw = values[0].Trim();
        malformedRaw = raw;
        if (!Uri.TryCreate(current, raw, out var created) || !Screen(created))
        {
            return LocationOutcome.Malformed;
        }

        if (!TryStripUserInfo(created, out var stripped))
        {
            return LocationOutcome.Malformed;
        }

        target = stripped;
        malformedRaw = null;
        return LocationOutcome.Resolved;
    }

    private static bool Screen(Uri created) =>
        created.IsAbsoluteUri
        && (created.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || created.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        && created.IdnHost.Length > 0;

    private static bool TryStripUserInfo(Uri created, out Uri stripped)
    {
        if (created.UserInfo.Length == 0)
        {
            stripped = created;
            return true;
        }

        var text = created.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped);
        if (Uri.TryCreate(text, UriKind.Absolute, out var parsed) && Screen(parsed))
        {
            stripped = parsed;
            return true;
        }

        stripped = created;
        return false;
    }
}
