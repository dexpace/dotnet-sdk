// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Text;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// A media type, as defined by RFC 7231 §3.1.1.1.
/// </summary>
/// <remarks>
/// Instances are immutable and constructed exclusively through <see cref="Of"/> and
/// <see cref="Parse"/>; both factories normalise the type, subtype, and parameter keys to
/// lower case so equality is case-insensitive in practice. Parameter values are case-preserved —
/// boundaries, base64 tokens, and other values must not be folded. A wildcard primary type is
/// only valid when the subtype is also a wildcard.
/// </remarks>
public sealed record MediaType
{
    private MediaType(string type, string subtype, ImmutableSortedDictionary<string, string> parameters)
    {
        Type = type;
        Subtype = subtype;
        Parameters = parameters;
    }

    /// <summary>The primary type (e.g. <c>application</c>, <c>text</c>), lower-cased.</summary>
    public string Type { get; }

    /// <summary>The subtype (e.g. <c>json</c>, <c>plain</c>), lower-cased.</summary>
    public string Subtype { get; }

    /// <summary>The media-type parameters keyed by lower-cased name (values case-preserved).</summary>
    public ImmutableSortedDictionary<string, string> Parameters { get; }

    /// <summary>The bare <c>type/subtype</c> form, without any parameters.</summary>
    public string FullType => $"{Type}/{Subtype}";

    /// <summary>
    /// The <c>charset</c> parameter resolved through <see cref="Encoding.GetEncoding(string)"/>, or
    /// <see langword="null"/> if absent or unknown. Unknown-charset failures are swallowed so callers
    /// can fall back to a default rather than wrapping every access in a try/catch.
    /// </summary>
    public Encoding? Charset
    {
        get
        {
            if (!Parameters.TryGetValue("charset", out var value))
            {
                return null;
            }

            try
            {
                return Encoding.GetEncoding(value);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Constructs a media type from its components. Type and subtype are validated as RFC 7230
    /// tokens and lower-cased; parameter keys are lower-cased, values preserved.
    /// </summary>
    /// <param name="type">The primary type.</param>
    /// <param name="subtype">The subtype.</param>
    /// <param name="parameters">Optional parameters.</param>
    /// <returns>The constructed <see cref="MediaType"/>.</returns>
    /// <exception cref="ArgumentException">A component is empty, not a valid token, or a half-wildcard.</exception>
    public static MediaType Of(
        string type,
        string subtype,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(subtype);

        var t = RequireToken(type, nameof(type)).ToLowerInvariant();
        var s = RequireToken(subtype, nameof(subtype)).ToLowerInvariant();
        if (t == "*" && s != "*")
        {
            throw new ArgumentException("A wildcard type requires a wildcard subtype.", nameof(type));
        }

        var builder = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                builder[RequireToken(key, nameof(parameters)).ToLowerInvariant()] = value;
            }
        }

        return new MediaType(t, s, builder.ToImmutable());
    }

    /// <summary>
    /// Parses a media type in <c>type/subtype;key=value</c> form. Quoted-string parameter values
    /// are unescaped. This is the inverse of <see cref="ToString"/> for every constructible value.
    /// </summary>
    /// <param name="value">The header value to parse.</param>
    /// <returns>The parsed <see cref="MediaType"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a well-formed media type.</exception>
    public static MediaType Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var segments = SplitRespectingQuotes(value);
        var typeParts = segments[0].Trim().Split('/');
        if (typeParts.Length != 2)
        {
            throw new ArgumentException($"Malformed media type: '{value}'.", nameof(value));
        }

        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < segments.Count; i++)
        {
            var segment = segments[i].Trim();
            if (segment.Length == 0)
            {
                continue;
            }

            var eq = segment.IndexOf('=');
            if (eq < 0)
            {
                throw new ArgumentException($"Malformed media-type parameter: '{segment}'.", nameof(value));
            }

            var key = segment[..eq].Trim();
            var raw = segment[(eq + 1)..].Trim();
            parameters[key] = Unquote(raw);
        }

        return Of(typeParts[0], typeParts[1], parameters);
    }

    /// <summary>
    /// True when this media type includes <paramref name="other"/>, treating wildcards in either
    /// position as matching anything. Parameters are not considered.
    /// </summary>
    /// <param name="other">The concrete media type to test.</param>
    /// <returns><see langword="true"/> if this pattern matches <paramref name="other"/>.</returns>
    public bool Includes(MediaType other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var typeMatches = Type == "*" || string.Equals(Type, other.Type, StringComparison.OrdinalIgnoreCase);
        var subtypeMatches = Subtype == "*" || string.Equals(Subtype, other.Subtype, StringComparison.OrdinalIgnoreCase);
        return typeMatches && subtypeMatches;
    }

    /// <summary>
    /// Returns the wire form: <c>type/subtype</c> followed by <c>;key=value</c> for each parameter.
    /// Values that are not RFC 7230 tokens are emitted as backslash-escaped quoted-strings, so
    /// <c>Parse(x.ToString())</c> round-trips for every constructible value.
    /// </summary>
    /// <returns>The serialised media type.</returns>
    public override string ToString()
    {
        if (Parameters.Count == 0)
        {
            return FullType;
        }

        var sb = new StringBuilder(FullType);
        foreach (var (key, value) in Parameters)
        {
            sb.Append(';').Append(key).Append('=').Append(FormatParameterValue(value));
        }

        return sb.ToString();
    }

    /// <summary>Value equality consistent with the case-folding factories.</summary>
    public bool Equals(MediaType? other) =>
        other is not null
        && Type == other.Type
        && Subtype == other.Subtype
        && Parameters.Count == other.Parameters.Count
        && Parameters.All(kv => other.Parameters.TryGetValue(kv.Key, out var v) && v == kv.Value);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Subtype);
        foreach (var (key, value) in Parameters)
        {
            hash.Add(key);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    private static string RequireToken(string candidate, string paramName)
    {
        if (candidate.Length == 0)
        {
            throw new ArgumentException("Media-type component must not be empty.", paramName);
        }

        foreach (var c in candidate)
        {
            if (!IsTokenChar(c) && c != '*')
            {
                throw new ArgumentException($"Invalid media-type token character '{c}'.", paramName);
            }
        }

        return candidate;
    }

    private static string FormatParameterValue(string value)
    {
        if (value.Length > 0 && value.All(IsTokenChar))
        {
            return value;
        }

        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            if (c is '"' or '\\')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        sb.Append('"');
        return sb.ToString();
    }

    private static List<string> SplitRespectingQuotes(string value)
    {
        var segments = new List<string>();
        var start = 0;
        var inQuotes = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case '\\' when inQuotes && i + 1 < value.Length:
                    i++; // skip the escaped character
                    break;
                case ';' when !inQuotes:
                    segments.Add(value[start..i]);
                    start = i + 1;
                    break;
            }
        }

        segments.Add(value[start..]);
        return segments;
    }

    private static string Unquote(string raw)
    {
        if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"')
        {
            return raw;
        }

        var sb = new StringBuilder(raw.Length - 2);
        for (var i = 1; i < raw.Length - 1; i++)
        {
            var c = raw[i];
            if (c == '\\' && i + 1 < raw.Length - 1)
            {
                c = raw[++i];
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    // RFC 7230 §3.2.6 token characters.
    private static bool IsTokenChar(char c) =>
        c is >= 'a' and <= 'z'
        or >= 'A' and <= 'Z'
        or >= '0' and <= '9'
        or '!' or '#' or '$' or '%' or '&' or '\'' or '*'
        or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
}
