// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// Factory methods that build <c>nextRequest</c> delegates for use with
/// <see cref="Pageable.Create{TPage,T}"/>.
/// </summary>
/// <remarks>
/// Each factory returns a <see cref="Func{TPage,Response,Request,Request}"/> compatible with the
/// <c>nextRequest</c> parameter of <see cref="Pageable.Create{TPage,T}"/>. The returned delegate
/// receives the deserialized page, the raw (not-yet-disposed) response, and the current request,
/// and must return the next <see cref="Request"/> to send or <see langword="null"/> to stop
/// iteration.
/// </remarks>
public static class PaginationStrategies
{
    // ── the IPageStrategy factories (PAGE-4, PAGE-5, PAGE-16 to PAGE-20; design P7c-2) ────────────────

    /// <summary>
    /// Adapts two delegates, the <c>selectItems</c> and <c>nextRequest</c> shape of design section 7.1, to an
    /// <see cref="IPageStrategy{TPage,T}"/> (PAGE-4, PAGE-5).
    /// </summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">Extracts the ordered item list from a deserialized envelope.</param>
    /// <param name="nextRequest">
    /// Given the envelope, the response (its body already read; read status, headers and <c>response.Request.Url</c>
    /// only) and the walk's first request, returns the next request, or <see langword="null"/> to end the stream.
    /// Build the next request from the first request, not from <c>response.Request</c>, which carries whatever the
    /// authorization policies stamped (P7c-8).
    /// </param>
    /// <returns>A strategy holding only the two delegates.</returns>
    /// <remarks>
    /// This is the migration path for the old two-delegate <c>Pageable.Create</c> and for a <c>hasMore</c>
    /// page-number predicate: <c>nextRequest</c> may inspect the envelope and stop early.
    /// </remarks>
    public static IPageStrategy<TPage, T> Create<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items,
        Func<TPage, Response, Request, Request?> nextRequest)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(nextRequest);
        return new DelegateStrategy<TPage, T>(items, nextRequest);
    }

    /// <summary>Builds the cursor strategy (PAGE-16): the envelope carries the cursor for the next page.</summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">Extracts the ordered item list from a deserialized envelope.</param>
    /// <param name="nextCursor">
    /// Extracts the continuation cursor from the envelope; <see langword="null"/> or empty ends the stream.
    /// </param>
    /// <param name="queryParameter">
    /// The query parameter set to the cursor on the next request (replacing an existing one in place); <c>cursor</c> by default.
    /// </param>
    /// <returns>A strategy holding only its configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="queryParameter"/> is empty or holds a lone surrogate.</exception>
    public static IPageStrategy<TPage, T> Cursor<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items,
        Func<TPage, string?> nextCursor,
        string queryParameter = "cursor")
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(nextCursor);
        RequireQueryParameter(queryParameter);
        return new CursorStrategy<TPage, T>(items, nextCursor, queryParameter);
    }

    /// <summary>Builds the page-number strategy (PAGE-17).</summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">Extracts the ordered item list from a deserialized envelope; an empty list ends the stream.</param>
    /// <param name="queryParameter">The query parameter that carries the page number; <c>page</c> by default.</param>
    /// <param name="startPage">
    /// The page number assumed when the executed request's URL carries none or a value that is not ASCII digits;
    /// <c>1</c> by default, <c>0</c> for a zero-based server.
    /// </param>
    /// <returns>A strategy holding only its configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="queryParameter"/> is empty or holds a lone surrogate.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="startPage"/> is negative.</exception>
    /// <remarks>
    /// <para>
    /// The current page is read from the <em>executed</em> request, parsed with the invariant culture and no sign
    /// or whitespace (a Unicode digit is not a digit), and the next request carries the current number plus one; a
    /// page that is already <see cref="int.MaxValue"/> ends the stream (P7c-11).
    /// </para>
    /// <para>
    /// <b>Breaking:</b> the old factory took a <c>hasMore</c> predicate and stopped on it. This one ends only on an empty
    /// page, so a non-empty last page now costs one more (empty) exchange. Use <see cref="Create{TPage,T}"/> for a
    /// predicate.
    /// </para>
    /// </remarks>
    public static IPageStrategy<TPage, T> PageNumber<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items,
        string queryParameter = "page",
        int startPage = 1)
    {
        ArgumentNullException.ThrowIfNull(items);
        RequireQueryParameter(queryParameter);
        ArgumentOutOfRangeException.ThrowIfNegative(startPage);
        return new PageNumberStrategy<TPage, T>(items, queryParameter, startPage);
    }

    /// <summary>
    /// Builds the <c>Link</c>-header strategy (PAGE-18 to PAGE-20), temporarily named so it can sit beside the old
    /// overload until that is removed.
    /// </summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">Extracts the ordered item list from a deserialized envelope.</param>
    /// <param name="headerName">The header to read; <c>Link</c> by default. Every instance of it is read.</param>
    /// <param name="allowCrossOrigin">
    /// Whether a <c>next</c> target on another origin than the first request may be followed. <see langword="false"/>
    /// by default: the walk ends there. Setting it is a security decision, because each page is a fresh pipeline call and
    /// the authorization policies stamp the credential for any origin the call's request is same-origin with.
    /// </param>
    /// <returns>A strategy holding only its configuration.</returns>
    /// <exception cref="ArgumentException"><paramref name="headerName"/> is empty or holds a lone surrogate.</exception>
    public static IPageStrategy<TPage, T> LinkHeaderNext<TPage, T>(
        Func<TPage, IReadOnlyList<T>> items,
        string headerName = "Link",
        bool allowCrossOrigin = false)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentException.ThrowIfNullOrEmpty(headerName);
        QuerySplice.RejectLoneSurrogate(headerName, nameof(headerName), "The header name");
        return new LinkHeaderStrategy<TPage, T>(items, headerName, allowCrossOrigin);
    }

    // Fails at construction, not on the first server cursor (P7c-10).
    private static void RequireQueryParameter(string queryParameter)
    {
        ArgumentException.ThrowIfNullOrEmpty(queryParameter);
        QuerySplice.RejectLoneSurrogate(queryParameter, nameof(queryParameter), "The query parameter name");
    }

    // ── Cursor ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a next-request delegate that drives cursor-based pagination.
    /// </summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <param name="nextCursor">
    /// Extracts the continuation cursor from a deserialized page. Return
    /// <see langword="null"/> or empty string to end iteration.
    /// </param>
    /// <param name="queryParameter">
    /// The URL query-string key to set to the cursor value on the next request.
    /// </param>
    /// <returns>
    /// A delegate that returns the next <see cref="Request"/> with <paramref name="queryParameter"/>
    /// set to the cursor, or <see langword="null"/> when the cursor is absent.
    /// </returns>
    public static Func<TPage, Response, Request, Request?> Cursor<TPage>(
        Func<TPage, string?> nextCursor,
        string queryParameter)
    {
        ArgumentNullException.ThrowIfNull(nextCursor);
        ArgumentNullException.ThrowIfNull(queryParameter);

        return (page, _, current) =>
        {
            var cursor = nextCursor(page);
            if (string.IsNullOrEmpty(cursor))
            {
                return null;
            }

            return current.WithUrl(SetQueryParameter(current.Url, queryParameter, cursor));
        };
    }

    // ── PageNumber ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a next-request delegate that drives page-number pagination.
    /// </summary>
    /// <typeparam name="TPage">The deserialized page-envelope type.</typeparam>
    /// <param name="queryParameter">
    /// The URL query-string key that carries the page number. If absent on the current request URL
    /// the current page number is treated as 1.
    /// </param>
    /// <param name="hasMore">
    /// Returns <see langword="true"/> when the response contains more pages to fetch.
    /// </param>
    /// <returns>
    /// A delegate that increments the page-number query parameter and returns the next
    /// <see cref="Request"/>, or <see langword="null"/> when <paramref name="hasMore"/> returns
    /// <see langword="false"/>.
    /// </returns>
    public static Func<TPage, Response, Request, Request?> PageNumber<TPage>(
        string queryParameter,
        Func<TPage, bool> hasMore)
    {
        ArgumentNullException.ThrowIfNull(queryParameter);
        ArgumentNullException.ThrowIfNull(hasMore);

        return (page, _, current) =>
        {
            if (!hasMore(page))
            {
                return null;
            }

            var raw = GetQueryParameter(current.Url, queryParameter);
            var currentPage = raw is not null && int.TryParse(raw, out var n) ? n : 1;
            var nextPage = currentPage + 1;
            return current.WithUrl(SetQueryParameter(current.Url, queryParameter, nextPage.ToString(CultureInfo.InvariantCulture)));
        };
    }

    // ── LinkHeader ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a next-request delegate that drives link-header pagination (RFC 8288).
    /// </summary>
    /// <param name="rel">
    /// The <c>rel</c> type to look for in the <c>Link</c> response header.
    /// Defaults to <c>"next"</c>.
    /// </param>
    /// <returns>
    /// A delegate that reads the <c>Link</c> response header, resolves the URL for the
    /// given <paramref name="rel"/>, and returns the next <see cref="Request"/>; or
    /// <see langword="null"/> if no matching entry is found or the URL cannot be resolved.
    /// </returns>
    public static Func<TPage, Response, Request, Request?> LinkHeader<TPage>(string rel = "next")
    {
        ArgumentNullException.ThrowIfNull(rel);

        return (_, response, current) =>
        {
            var linkHeaderValue = response.Headers.Get("Link");
            if (string.IsNullOrEmpty(linkHeaderValue))
            {
                return null;
            }

            var linkUrl = ParseLinkHeader(linkHeaderValue, rel);
            if (linkUrl is null)
            {
                return null;
            }

            // Resolve the link URL against the current request URL.
            if (!Uri.TryCreate(current.Url, linkUrl, out var resolved))
            {
                return null;
            }

            // Scheme guard: only allow http/https so an untrusted Link header (mailto:, javascript:, ftp:, etc.)
            // ends pagination (null) instead of reaching WithUrl, which would throw on such a URL.
            if (!resolved.IsAbsoluteUri
                || (!resolved.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    && !resolved.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return current.WithUrl(resolved);
        };
    }

    // ── private helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the raw (URI-decoded) value of the first occurrence of
    /// <paramref name="key"/> in the query string of <paramref name="uri"/>, or
    /// <see langword="null"/> when the key is absent.
    /// </summary>
    private static string? GetQueryParameter(Uri uri, string key)
    {
        var query = uri.Query;
        if (string.IsNullOrEmpty(query))
        {
            return null;
        }

        // Strip leading '?'.
        var span = query.AsSpan().TrimStart('?');
        var encodedKey = Uri.EscapeDataString(key);

        foreach (var pair in new QueryPairEnumerator(span))
        {
            var eqIndex = pair.IndexOf('=');
            var pairKey = eqIndex < 0 ? pair : pair[..eqIndex];
            if (pairKey.Equals(encodedKey.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                if (eqIndex < 0)
                {
                    return string.Empty;
                }

                return Uri.UnescapeDataString(new string(pair[(eqIndex + 1)..]));
            }
        }

        return null;
    }

    /// <summary>
    /// Returns a new <see cref="Uri"/> identical to <paramref name="uri"/> except that
    /// <paramref name="key"/> is set to <paramref name="value"/> (replacing any existing
    /// occurrence, or appending if absent).
    /// </summary>
    private static Uri SetQueryParameter(Uri uri, string key, string value)
    {
        var encodedKey = Uri.EscapeDataString(key);
        var encodedValue = Uri.EscapeDataString(value);
        var pair = $"{encodedKey}={encodedValue}";

        var existing = uri.Query.TrimStart('?');
        string newQuery;

        if (string.IsNullOrEmpty(existing))
        {
            newQuery = pair;
        }
        else
        {
            // Replace the existing key=value if present, otherwise append.
            var span = existing.AsSpan();
            var sb = new System.Text.StringBuilder();
            var replaced = false;

            foreach (var segment in new QueryPairEnumerator(span))
            {
                var eqIndex = segment.IndexOf('=');
                var segKey = eqIndex < 0 ? segment : segment[..eqIndex];

                if (!replaced && segKey.Equals(encodedKey.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    if (sb.Length > 0) { sb.Append('&'); }
                    sb.Append(pair);
                    replaced = true;
                }
                else
                {
                    if (sb.Length > 0) { sb.Append('&'); }
                    sb.Append(new string(segment));
                }
            }

            if (!replaced)
            {
                if (sb.Length > 0) { sb.Append('&'); }
                sb.Append(pair);
            }

            newQuery = sb.ToString();
        }

        var builder = new UriBuilder(uri) { Query = newQuery };
        return builder.Uri;
    }

    /// <summary>
    /// Parses the value of a <c>Link</c> header and returns the URL string for the entry
    /// whose <c>rel</c> matches <paramref name="rel"/> (case-insensitive), or
    /// <see langword="null"/> if not found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles the comma-separated format:
    /// <c>&lt;https://api.example.com/items?page=2&gt;; rel="next", &lt;...&gt;; rel="prev"</c>
    /// </para>
    /// <para>
    /// Quoted parameter values (e.g. <c>title="Page 3, final"</c>) are handled correctly:
    /// commas and semicolons inside a double-quoted run are not treated as delimiters.
    /// </para>
    /// <para>
    /// Per RFC 8288, <c>rel</c> is a space-separated token list; any token that matches
    /// <paramref name="rel"/> is accepted.
    /// </para>
    /// </remarks>
    private static string? ParseLinkHeader(string headerValue, string rel)
    {
        // Split on commas that are not inside angle brackets or double-quoted strings.
        var entries = SplitLinkEntries(headerValue);

        foreach (var entry in entries)
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0) { continue; }

            // Each entry: <URL>; param=value; param=value…
            // Use quote-aware split on ';'.
            var parts = SplitLinkParams(trimmed);
            if (parts.Count < 2) { continue; }

            // First part must be <URL>.
            var urlPart = parts[0].Trim();
            if (!urlPart.StartsWith('<') || !urlPart.EndsWith('>')) { continue; }

            var linkUrl = urlPart[1..^1].Trim();

            // Scan the remaining parts for rel= (RFC 8288: space-separated token list).
            var hasMatchingRel = false;
            for (var i = 1; i < parts.Count; i++)
            {
                var param = parts[i].Trim();

                if (param.StartsWith("rel=", StringComparison.OrdinalIgnoreCase))
                {
                    // Strip optional surrounding quotes, then split the token list on whitespace.
                    var relValue = param["rel=".Length..].Trim().Trim('"');
                    foreach (var token in relValue.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (string.Equals(token, rel, StringComparison.OrdinalIgnoreCase))
                        {
                            hasMatchingRel = true;
                            break;
                        }
                    }

                    if (hasMatchingRel) { break; }
                }
            }

            if (hasMatchingRel)
            {
                return linkUrl;
            }
        }

        return null;
    }

    /// <summary>
    /// Splits a <c>Link</c> header value on top-level commas — those that are outside
    /// both angle brackets (<c>&lt;…&gt;</c>) and double-quoted strings (<c>"…"</c>).
    /// </summary>
    private static IEnumerable<string> SplitLinkEntries(string headerValue)
    {
        var depth = 0;        // angle-bracket nesting
        var inQuotes = false; // inside "…"
        var start = 0;

        for (var i = 0; i < headerValue.Length; i++)
        {
            var ch = headerValue[i];

            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes)
            {
                switch (ch)
                {
                    case '<': depth++; break;
                    case '>': if (depth > 0) { depth--; } break;
                    case ',' when depth == 0:
                        yield return headerValue[start..i];
                        start = i + 1;
                        break;
                }
            }
        }

        if (start < headerValue.Length)
        {
            yield return headerValue[start..];
        }
    }

    /// <summary>
    /// Splits one <c>Link</c> entry on semicolons that are outside double-quoted strings.
    /// The first element is always the <c>&lt;URL&gt;</c> target; the remainder are parameters.
    /// </summary>
    private static List<string> SplitLinkParams(string entry)
    {
        var parts = new List<string>();
        var inQuotes = false;
        var start = 0;

        for (var i = 0; i < entry.Length; i++)
        {
            var ch = entry[i];

            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && ch == ';')
            {
                parts.Add(entry[start..i]);
                start = i + 1;
            }
        }

        if (start <= entry.Length)
        {
            parts.Add(entry[start..]);
        }

        return parts;
    }

    // ── query-pair ref-struct enumerator ──────────────────────────────────────────────────────

    /// <summary>
    /// Zero-allocation enumerator over <c>key=value</c> pairs in a query string span
    /// (without the leading <c>?</c>). Each iteration returns a <see cref="ReadOnlySpan{T}"/>
    /// slice covering one <c>key=value</c> segment.
    /// </summary>
    private ref struct QueryPairEnumerator
    {
        private ReadOnlySpan<char> _remaining;
        private ReadOnlySpan<char> _current;

        internal QueryPairEnumerator(ReadOnlySpan<char> query) => _remaining = query;

        /// <summary>Returns <c>this</c> so the type works in <c>foreach</c>.</summary>
        public readonly QueryPairEnumerator GetEnumerator() => this;

        /// <summary>The current segment.</summary>
        public readonly ReadOnlySpan<char> Current => _current;

        /// <summary>Advances to the next segment.</summary>
        public bool MoveNext()
        {
            while (!_remaining.IsEmpty)
            {
                var idx = _remaining.IndexOf('&');
                if (idx < 0)
                {
                    _current = _remaining;
                    _remaining = [];
                }
                else
                {
                    _current = _remaining[..idx];
                    _remaining = _remaining[(idx + 1)..];
                }

                // Skip empty segments (e.g. leading/trailing '&').
                if (!_current.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
