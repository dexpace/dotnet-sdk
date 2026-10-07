// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// Renders a header set into log-state pairs: default-deny by name, URL-valued headers redacted as URLs, every value
/// bounded (OBS-7, OBS-16, OBS-17, OBS-18, XCUT-19(c)). Stateless once built, so one instance serves every call and
/// thread, and the sync and async paths alike (OBS-17).
/// </summary>
internal sealed class HeaderLogRenderer
{
    private readonly FrozenSet<string> _allowed;
    private readonly FrozenSet<string> _urlValued;
    private readonly UrlRedactor _redactor;
    private readonly bool _omitDisallowed;

    internal HeaderLogRenderer(HttpLoggingOptions options, UrlRedactor redactor)
    {
        _allowed = options.AllowedHeaderNames.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        _urlValued = options.UrlValuedHeaderNames.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        _redactor = redactor;
        _omitDisallowed = options.OmitDisallowedHeaders;
    }

    /// <summary>Appends one pair per header, in insertion order, keyed <paramref name="prefix"/> plus the lower-cased name.</summary>
    /// <param name="headers">The headers.</param>
    /// <param name="prefix">The key prefix, such as <see cref="DexpaceLogKeys.HttpRequestHeaderPrefix"/>.</param>
    /// <param name="into">The list the pairs are appended to.</param>
    internal void Render(Headers headers, string prefix, List<KeyValuePair<string, object?>> into)
    {
        foreach (var (name, values) in headers)
        {
            var key = prefix + name.ToLowerInvariant();
            if (!_allowed.Contains(name))
            {
                if (!_omitDisallowed)
                {
                    into.Add(new KeyValuePair<string, object?>(key, DexpaceLogKeys.RedactedHeaderValue));
                }

                continue;
            }

            into.Add(new KeyValuePair<string, object?>(key, LogText.Truncate(Join(name, values))));
        }
    }

    private string Join(string name, IReadOnlyList<string> values)
    {
        if (!_urlValued.Contains(name))
        {
            return values.Count == 1 ? values[0] : string.Join(", ", values);
        }

        var redacted = new string[values.Count];
        for (var i = 0; i < redacted.Length; i++)
        {
            redacted[i] = _redactor.RedactHeaderValue(values[i]);
        }

        return string.Join(", ", redacted);
    }
}
