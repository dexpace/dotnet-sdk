// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// The header-syntax predicates the model validates with, for transports that re-check at the wire (HTTP-17,
/// HTTP-18, HTTP-19, HTTP-20; design §4.1, §10 <c>construction-bypass</c>).
/// </summary>
/// <remarks>
/// A mandatory wire re-check that every transport restated would drift, so the four rules live here once. The
/// predicates are non-throwing and allocation-free. The throwing validators stay internal and read the same character
/// table: <see cref="Headers"/>, <see cref="Headers.Builder"/> and <see cref="HttpHeaderName.Of(string)"/> throw an
/// <see cref="ArgumentException"/> whose message names the offending character by code point and never echoes the
/// value (HTTP-20); a transport that finds a forged model drops the header and logs <see cref="EscapeName(string)"/>.
/// </remarks>
public static class HttpHeaderSyntax
{
    /// <summary>
    /// True when <paramref name="name"/> is a non-empty RFC 9110 token. No trimming is applied: <c>" X"</c> is invalid.
    /// </summary>
    /// <param name="name">The candidate header name.</param>
    /// <returns><see langword="true"/> when every character is a <c>tchar</c> and the name is not empty.</returns>
    public static bool IsValidName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!IsTokenChar(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when <paramref name="value"/> may be sent: HTAB and printable ASCII (0x20 to 0x7E) only, so CR, LF, NUL,
    /// other controls, DEL and non-ASCII are rejected (HTTP-18).
    /// </summary>
    /// <param name="value">The candidate outbound header value; the empty value is valid.</param>
    /// <returns><see langword="true"/> when every character is allowed on the wire.</returns>
    public static bool IsValidOutboundValue(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (!IsOutboundValueChar(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when <paramref name="value"/> may be accepted from a response: no C0 control other than HTAB and no DEL;
    /// obs-text (0x80 and above) is allowed (HTTP-19).
    /// </summary>
    /// <param name="value">The candidate inbound header value; the empty value is valid.</param>
    /// <returns><see langword="true"/> when no control character is present.</returns>
    public static bool IsValidInboundValue(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Renders <paramref name="name"/> safe for a log line (HTTP-20): every non-token character becomes
    /// <c>\uXXXX</c> (so a space is <c>\u0020</c> and CR is <c>\u000D</c>); a valid token comes back unchanged.
    /// </summary>
    /// <param name="name">The header name, valid or not.</param>
    /// <returns>The escaped name, which never holds a control character, space or colon.</returns>
    public static string EscapeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (IsValidName(name) || name.Length == 0)
        {
            return name;
        }

        var sb = new StringBuilder(name.Length + 8);
        foreach (var c in name)
        {
            if (IsTokenChar(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append(string.Create(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}"));
            }
        }

        return sb.ToString();
    }

    /// <summary>An RFC 9110 <c>tchar</c>.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a token character.</returns>
    internal static bool IsTokenChar(char c) =>
        c is >= 'a' and <= 'z'
        or >= 'A' and <= 'Z'
        or >= '0' and <= '9'
        or '!' or '#' or '$' or '%' or '&' or '\'' or '*'
        or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';

    /// <summary>HTAB or printable ASCII (0x20 to 0x7E).</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> when the character may appear in an outbound value.</returns>
    internal static bool IsOutboundValueChar(char c) => c == '\t' || c is >= ' ' and <= '~';

    /// <summary>A C0 control other than HTAB, or DEL.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> when the character may not appear in any value.</returns>
    internal static bool IsControl(char c) => (c < ' ' && c != '\t') || c == '\u007F';
}
