// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// Resolves the raw target of a <c>Link: &lt;…&gt;; rel=next</c> header into the URL of the next page, or says there
/// is none (PAGE-19; design P7c-12).
/// </summary>
/// <remarks>
/// <para>
/// Total, like the redirect location resolver (REDIR-12): it never throws, and every rejection is the same quiet
/// "no next page" that ends the walk (PAGE-19 asks for an end, not an error). A target is rejected when it holds a
/// space, any control character, <c>&lt;</c>, <c>&gt;</c> or <c>"</c> (<see cref="Uri.TryCreate(Uri, string, out Uri)"/>
/// accepts <c>not a url</c> as a relative path and <c>https://evil/x&#9;y</c> as a URL, design fact 3); when it does
/// not resolve to an absolute <c>http</c> or <c>https</c> URL with a host; and, unless the caller opted in, when its
/// origin is not the template's.
/// </para>
/// <para>
/// The origin compared is the <em>template's</em> (the walk's first request), not the response's: each page is a fresh
/// pipeline call, and the authorization policies stamp a credential when a request is same-origin with their own
/// call's seed, so a hostile or compromised server could otherwise harvest the bearer token with one header
/// (design fact 9). Comparing against the response URL would let a first page that redirected to another origin
/// launder that origin. A scheme downgrade is a different origin and ends too. Userinfo is removed from the target
/// (the REDIR-12 analogue): a server must not be able to plant credentials in the next URL.
/// </para>
/// </remarks>
internal static class LinkTarget
{
    /// <summary>Resolves <paramref name="raw"/> against <paramref name="responseUrl"/>.</summary>
    /// <param name="responseUrl">
    /// The URL the page was fetched from (<c>response.Request.Url</c>, post-redirect): the RFC 3986 base.
    /// </param>
    /// <param name="raw">The raw URI-reference from the <c>Link</c> header; <see langword="null"/> is rejected.</param>
    /// <param name="templateUrl">The walk's first URL, whose origin a target must share.</param>
    /// <param name="allowCrossOrigin">Whether a target on another origin may be followed.</param>
    /// <param name="target">The next URL, without userinfo, when the result is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when there is a next page to follow.</returns>
    internal static bool TryResolve(
        Uri responseUrl,
        string? raw,
        Uri templateUrl,
        bool allowCrossOrigin,
        [NotNullWhen(true)] out Uri? target)
    {
        target = null;
        var text = raw?.Trim(' ', '\t');
        if (string.IsNullOrEmpty(text) || HasForbiddenCharacter(text) || !TryCombine(responseUrl, text, out var created))
        {
            return false;
        }

        if (!IsHttpWithHost(created))
        {
            return false;
        }

        var stripped = created.UserInfo.Length == 0
            ? created
            : new Uri(created.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped), UriKind.Absolute);
        if (!allowCrossOrigin && HttpOrigin.From(stripped) != HttpOrigin.From(templateUrl))
        {
            return false;
        }

        target = stripped;
        return true;
    }

    // Space and every control character (the C0 block and DELETE), plus the three characters that cannot appear in a
    // URI-reference inside `<…>` and `"…"`.
    private static bool HasForbiddenCharacter(string text)
    {
        foreach (var ch in text)
        {
            if (ch <= ' ' || ch == '\u007F' || ch is '<' or '>' or '"')
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCombine(Uri baseUri, string text, [NotNullWhen(true)] out Uri? created)
    {
        try
        {
            return Uri.TryCreate(baseUri, text, out created);
        }
        catch (Exception ex) when (ex is UriFormatException or ArgumentException or InvalidOperationException)
        {
            created = null;
            return false;
        }
    }

    private static bool IsHttpWithHost(Uri created) =>
        created.IsAbsoluteUri
        && (created.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || created.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        && created.IdnHost.Length > 0;
}
