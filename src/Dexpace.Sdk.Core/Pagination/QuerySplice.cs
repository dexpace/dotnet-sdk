// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// Reads and rewrites one query parameter of an absolute URI by splicing the raw query, not by re-rendering it
/// (PAGE-21 to PAGE-24; design P7c-9, P7c-10).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Query"/> is not used: it re-renders the whole query from a parsed model, which PAGE-21 forbids for the
/// segments a splice does not target. <see cref="UriBuilder"/> is not used either: it writes an explicit default port
/// (<c>https://h:443/p</c>), which PAGE-24 forbids, so the URL is rebuilt from <see cref="Uri.GetComponents"/>
/// (design fact 1). <see cref="Uri"/> drops a scoped IPv6 host's zone id from every component, so it is restored from
/// <see cref="Uri.IdnHost"/> (PAGE-24).
/// </para>
/// <para>
/// A segment is the text between two <c>&amp;</c>; empty segments are skipped (the HTTP-31 leniency). Its name is the
/// text before the first <c>=</c>, decoded with <c>Rfc3986.DecodeComponent</c> and compared ordinally to the wanted
/// name, so <c>?Page=1</c> is not <c>page</c> and <c>?my%20key=1</c> is <c>my key</c> (P7c-9). Untargeted segments are
/// copied verbatim. The residual of "verbatim" is <see cref="Uri"/>'s own canonical form of the query, which decodes
/// percent-escaped unreserved characters before this type sees them (design section 10 entry 18).
/// </para>
/// </remarks>
internal static class QuerySplice
{
    /// <summary>Reads the first parameter called <paramref name="name"/> (PAGE-22).</summary>
    /// <param name="url">The absolute URI whose query is read.</param>
    /// <param name="name">The decoded parameter name.</param>
    /// <returns>
    /// The decoded value (<c>+</c> stays <c>+</c>); <see cref="string.Empty"/> for a value-less flag; <see langword="null"/>
    /// when the name is absent.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or holds a lone surrogate.</exception>
    internal static string? Get(Uri url, string name)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrEmpty(name);
        RejectLoneSurrogate(name, nameof(name), "The query parameter name");

        foreach (var segment in Segments(url))
        {
            var (rawName, rawValue) = SplitSegment(segment);
            if (string.Equals(Rfc3986.DecodeComponent(rawName), name, StringComparison.Ordinal))
            {
                return rawValue is null ? string.Empty : Rfc3986.DecodeComponent(rawValue);
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <paramref name="url"/> with the parameter <paramref name="name"/> set to <paramref name="value"/>:
    /// the first occurrence is replaced in place, later duplicates are dropped, an absent name is appended, and a
    /// <see langword="null"/> value removes the parameter (PAGE-21 to PAGE-24).
    /// </summary>
    /// <param name="url">The absolute URI to rewrite; it is not mutated.</param>
    /// <param name="name">The decoded parameter name.</param>
    /// <param name="value">The decoded value, or <see langword="null"/> to remove the parameter.</param>
    /// <returns>The rewritten URI. Scheme, userinfo, host, port, path and fragment are carried over.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty, or <paramref name="name"/> or <paramref name="value"/> holds a lone
    /// surrogate. The message names the parameter and never echoes the value, because a cursor can carry a session
    /// token (P7c-10).
    /// </exception>
    internal static Uri Set(Uri url, string name, string? value)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrEmpty(name);
        RejectLoneSurrogate(name, nameof(name), "The query parameter name");
        if (value is not null)
        {
            RejectLoneSurrogate(value, nameof(value), $"The value of query parameter \"{name}\"");
        }

        var kept = new List<string>();
        var matched = false;
        foreach (var segment in Segments(url))
        {
            var rawName = SplitSegment(segment).RawName;
            if (!string.Equals(Rfc3986.DecodeComponent(rawName), name, StringComparison.Ordinal))
            {
                kept.Add(segment);
                continue;
            }

            // PAGE-23: later duplicates are dropped, and on removal every match is.
            if (matched || value is null)
            {
                continue;
            }

            matched = true;
            kept.Add(Render(name, value));
        }

        if (!matched && value is not null)
        {
            kept.Add(Render(name, value));
        }

        return Rebuild(url, string.Join('&', kept));
    }

    // The `&`-separated segments of the raw query, empty ones skipped. Only the one delimiting '?' is dropped: a parameter
    // name may itself start with '?' (`p??a=1` has a parameter called "?a"), and stripping every leading one would rewrite it.
    private static string[] Segments(Uri url) =>
        (url.Query.Length > 0 ? url.Query[1..] : string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries);

    // Text before and after the first '='; a segment with no '=' is a value-less flag (null value).
    private static (string RawName, string? RawValue) SplitSegment(string segment)
    {
        var eq = segment.IndexOf('=', StringComparison.Ordinal);
        return eq < 0 ? (segment, null) : (segment[..eq], segment[(eq + 1)..]);
    }

    private static string Render(string name, string value) =>
        $"{Rfc3986.EncodeComponent(name)}={Rfc3986.EncodeComponent(value)}";

    // Scheme, userinfo, host, port and path from the components (never UriBuilder: PAGE-24), then the new query,
    // then the fragment when the source had one.
    private static Uri Rebuild(Uri url, string query)
    {
        var text = RestoreZoneId(
            url,
            url.GetComponents(
                UriComponents.SchemeAndServer | UriComponents.UserInfo | UriComponents.Path,
                UriFormat.UriEscaped));
        if (query.Length > 0)
        {
            text += "?" + query;
        }

        if (url.Fragment.Length > 0)
        {
            text += "#" + url.GetComponents(UriComponents.Fragment, UriFormat.UriEscaped);
        }

        return new Uri(text, UriKind.Absolute);
    }

    // Every Uri.GetComponents form drops the zone id of a scoped IPv6 literal (`[fe80::1%25eth0]` comes back as `[fe80::1]`),
    // and so does Host and AbsoluteUri; only IdnHost keeps it. Without this a Cursor or PageNumber walk would aim every next
    // request at the same address on no interface, which PAGE-24 ("host preserved exactly") forbids. The host in the rebuilt
    // text is the first bracketed literal: neither the scheme nor an unescaped userinfo can hold a '['.
    private static string RestoreZoneId(Uri url, string text)
    {
        if (url.HostNameType != UriHostNameType.IPv6 || !url.IdnHost.Contains('%', StringComparison.Ordinal))
        {
            return text;
        }

        var bare = url.Host;
        var at = text.IndexOf(bare, StringComparison.Ordinal);
        return at < 0 ? text : $"{text[..at]}[{url.IdnHost}]{text[(at + bare.Length)..]}";
    }

    /// <summary>
    /// Throws when <paramref name="text"/> holds an unpaired surrogate, which <c>Rfc3986.EncodeComponent</c> would turn
    /// into U+FFFD's bytes (design fact 11), silently corrupting a server-supplied cursor; <c>Query.Builder</c> rejects
    /// one with <see cref="ArgumentException"/> for the same reason (P7c-10). The text itself is never echoed.
    /// </summary>
    /// <param name="text">The name or value to check.</param>
    /// <param name="paramName">The parameter the exception names.</param>
    /// <param name="subject">What the text is, as the start of the message (for example <c>The header name</c>).</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> holds a lone surrogate.</exception>
    internal static void RejectLoneSurrogate(string text, string paramName, string subject)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                throw new ArgumentException(
                    $"{subject} contains an unpaired surrogate and cannot be percent-encoded.",
                    paramName);
            }
        }
    }
}
