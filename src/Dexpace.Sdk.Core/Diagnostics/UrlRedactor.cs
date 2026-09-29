// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// Produces a log-safe string form of a URL. Redaction is <strong>default-deny</strong>: every query-parameter value
/// is replaced with <c>***</c> unless its name is on the allow-list.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Redaction boundary</strong> (OBS-11–OBS-15, XCUT-19):
/// <list type="bullet">
///   <item><description>
///     <strong>Userinfo</strong> (the <c>user:password@</c> segment of an authority) is always replaced with
///     <c>***:***@</c>, whatever the allow-list says.
///   </description></item>
///   <item><description>
///     <strong>Query-parameter values</strong> become <c>***</c> unless the parameter name, percent-decoded and
///     compared case-insensitively, is allow-listed. The default allow-list is exactly <c>{api-version}</c>; an empty
///     allow-list redacts every value. Names, the <c>=</c> separator, value-less parameters (<c>?flag</c>) and a
///     present-but-empty query (a trailing <c>?</c>) are kept; a trailing <c>&amp;</c> is dropped.
///   </description></item>
///   <item><description>
///     <strong>Fragment</strong> <c>key=value</c> tokens are redacted under the same allow-list; a fragment with no
///     <c>=</c> is kept verbatim.
///   </description></item>
///   <item><description>
///     <strong>Scheme, host, port and path are preserved verbatim</strong>, and nothing kept is re-encoded. Callers
///     must not embed secrets inside the URL path; this class does not inspect or redact path components.
///   </description></item>
///   <item><description>
///     Redaction never throws on its input: a URL that cannot be parsed yields the fixed sentinel
///     <c>[malformed url]</c>.
///   </description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class UrlRedactor
{
    /// <summary>
    /// The default query-parameter allow-list: exactly <c>api-version</c> (OBS-12).
    /// </summary>
    public static readonly IReadOnlyCollection<string> DefaultQueryAllowList = ["api-version"];

    private const string RedactedValue = "***";
    private const string RedactedUserInfo = "***:***@";
    private const string MalformedUrl = "[malformed url]";

    private readonly HashSet<string> _allowList;

    /// <summary>
    /// Initializes a <see cref="UrlRedactor"/> using <see cref="DefaultQueryAllowList"/>.
    /// </summary>
    public UrlRedactor()
        : this(DefaultQueryAllowList)
    {
    }

    /// <summary>
    /// Initializes a <see cref="UrlRedactor"/> with a caller-supplied query-parameter allow-list (case-insensitive).
    /// </summary>
    /// <param name="queryAllowList">
    /// The query-parameter names whose values are logged verbatim. Every other value is replaced with <c>***</c>;
    /// an empty collection redacts every value.
    /// </param>
    public UrlRedactor(IEnumerable<string> queryAllowList)
    {
        ArgumentNullException.ThrowIfNull(queryAllowList);
        _allowList = new HashSet<string>(queryAllowList, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns a log-safe representation of <paramref name="uri"/>, built from its original text. Relative and
    /// absolute URIs are both accepted.
    /// </summary>
    /// <param name="uri">The URI to redact. May be relative or absolute.</param>
    /// <returns>The redacted URL, or <c>[malformed url]</c> if it cannot be redacted.</returns>
    public string Redact(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        // OBS-15: total. Nothing below is expected to throw; this is the backstop that keeps a logging call from
        // ever breaking the request it describes.
#pragma warning disable CA1031 // A redaction failure must yield the sentinel, never an exception (OBS-15, XCUT-20).
        try
        {
            return RedactParsed(uri);
        }
        catch (Exception)
        {
            return MalformedUrl;
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Returns a log-safe representation of the URL text <paramref name="url"/>, relative or absolute.
    /// </summary>
    /// <param name="url">The URL text to redact.</param>
    /// <returns>
    /// The redacted URL, or <c>[malformed url]</c> when <paramref name="url"/> is not a well-formed URI reference
    /// (for example, it contains whitespace or control characters, or its authority does not parse).
    /// </returns>
    public string Redact(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!IsVerbatimSafe(url) || !Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var parsed))
        {
            return MalformedUrl;
        }

        return Redact(parsed);
    }

    private string RedactParsed(Uri uri)
    {
        var original = uri.OriginalString;
        if (IsVerbatimSafe(original))
        {
            var redacted = RedactText(original, out var maskedUserInfo);

            // Defence in depth: should System.Uri ever report authority userinfo that the text scan did not find, the
            // parsed form below decides. No input reaching this branch is known to do so.
            if (maskedUserInfo || !HasAuthorityUserInfo(uri))
            {
                return redacted;
            }
        }

        // System.Uri parsed something other than the original text (it trims surrounding whitespace, drops tabs and
        // turns '\' into '/'), so the text cannot be trusted to locate the userinfo or the query. Its canonical form
        // can; for a relative reference there is none.
        if (!uri.IsAbsoluteUri)
        {
            return MalformedUrl;
        }

        // Fail closed when the text has a query but the canonical form does not: for schemes System.Uri gives no
        // query (ftp, news, nntp, gopher, telnet, uuid) it escapes '?' to "%3F", and the value would pass unredacted.
        var absolute = uri.AbsoluteUri;
        if (HasQueryDelimiter(original) && !HasQueryDelimiter(absolute))
        {
            return MalformedUrl;
        }

        var canonical = RedactText(absolute, out var masked);
        return masked || !HasAuthorityUserInfo(uri) ? canonical : MalformedUrl;
    }

    // A '?' before the first '#': the query delimiter. One inside the fragment is not (OBS-14).
    private static bool HasQueryDelimiter(string text)
    {
        var hash = text.IndexOf('#', StringComparison.Ordinal);
        var question = text.IndexOf('?', StringComparison.Ordinal);
        return question >= 0 && (hash < 0 || question < hash);
    }

    // Userinfo inside a "//" authority. Uri also reports a mailto: local part as UserInfo, which is an address, not a
    // credential, and is kept as the opaque part it is.
    private static bool HasAuthorityUserInfo(Uri uri) =>
        uri.IsAbsoluteUri && uri.UserInfo.Length > 0 && AuthorityStart(uri.AbsoluteUri) >= 0;

    // RFC 3986 admits no whitespace, control character or backslash in a URI reference; System.Uri silently rewrites
    // them, so text that holds one is not the text it parsed.
    private static bool IsVerbatimSafe(string text)
    {
        foreach (var c in text)
        {
            if (c <= ' ' || c == '\u007F' || c == '\\')
            {
                return false;
            }
        }

        return true;
    }

    // Splits at the first '#', then at the first '?' before it, and rebuilds from the untouched pieces.
    private string RedactText(string text, out bool maskedUserInfo)
    {
        var hash = text.IndexOf('#', StringComparison.Ordinal);
        var head = hash < 0 ? text : text[..hash];
        var fragment = hash < 0 ? null : text[(hash + 1)..];

        var question = head.IndexOf('?', StringComparison.Ordinal);
        var beforeQuery = question < 0 ? head : head[..question];
        var query = question < 0 ? null : head[(question + 1)..];

        var builder = new StringBuilder(text.Length + 16);
        maskedUserInfo = AppendMaskingUserInfo(builder, beforeQuery);

        if (query is not null)
        {
            builder.Append('?');
            AppendRedactedPairs(builder, query);
        }

        if (fragment is not null)
        {
            builder.Append('#');
            if (fragment.Contains('=', StringComparison.Ordinal))
            {
                AppendRedactedPairs(builder, fragment);
            }
            else
            {
                builder.Append(fragment);
            }
        }

        return builder.ToString();
    }

    // Appends scheme, authority and path verbatim, except an authority's userinfo, which becomes "***:***@".
    private static bool AppendMaskingUserInfo(StringBuilder builder, string beforeQuery)
    {
        var authorityStart = AuthorityStart(beforeQuery);
        if (authorityStart >= 0)
        {
            var authorityEnd = beforeQuery.IndexOf('/', authorityStart);
            if (authorityEnd < 0)
            {
                authorityEnd = beforeQuery.Length;
            }

            // The last '@' of the authority: a stray '@' inside the userinfo hides nothing.
            var at = beforeQuery.AsSpan(authorityStart, authorityEnd - authorityStart).LastIndexOf('@');
            if (at >= 0)
            {
                builder.Append(beforeQuery, 0, authorityStart).Append(RedactedUserInfo);
                builder.Append(beforeQuery, authorityStart + at + 1, beforeQuery.Length - authorityStart - at - 1);
                return true;
            }
        }

        builder.Append(beforeQuery);
        return false;
    }

    // The index just past the "//" that opens an authority ("scheme://" or a network-path "//"), or -1.
    private static int AuthorityStart(string text)
    {
        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            return 2;
        }

        if (text.Length == 0 || !char.IsAsciiLetter(text[0]))
        {
            return -1;
        }

        var i = 1;
        while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] is '+' or '-' or '.'))
        {
            i++;
        }

        return string.CompareOrdinal(text, i, "://", 0, 3) == 0 ? i + 3 : -1;
    }

    // Rewrites each "name=value" token as "name=***" unless the name is allow-listed; bare tokens are kept. A trailing
    // empty token (a final '&') is dropped (OBS-14).
    private void AppendRedactedPairs(StringBuilder builder, string pairs)
    {
        var tokens = pairs.Split('&');
        var count = tokens.Length > 1 && tokens[^1].Length == 0 ? tokens.Length - 1 : tokens.Length;

        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append('&');
            }

            var token = tokens[i];
            var eq = token.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0 || _allowList.Contains(Uri.UnescapeDataString(token[..eq])))
            {
                builder.Append(token);
            }
            else
            {
                builder.Append(token, 0, eq + 1).Append(RedactedValue);
            }
        }
    }
}
