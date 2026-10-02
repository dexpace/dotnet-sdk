// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// The header-syntax predicates the model validates with (HTTP-17, HTTP-18, HTTP-19, HTTP-20, HTTP-26, XCUT-18;
/// design §4.1): names are trimmed of surrounding SP/HTAB and held to the RFC 9110 token grammar (a superset of
/// HTTP-17's rejections, design §11 item 36); outbound values accept HTAB and printable ASCII only; inbound values
/// additionally admit obs-text (0x80 and above). Every rejection names the offending character by code point and
/// never echoes the value, so a CR/LF or a secret never lands raw in a log line.
/// </summary>
internal static class HeaderSyntax
{
    /// <summary>Trims surrounding SP/HTAB from <paramref name="name"/> and validates it as a token.</summary>
    /// <param name="name">The candidate header name.</param>
    /// <param name="paramName">The caller's parameter name, for the exception.</param>
    /// <returns>The trimmed name.</returns>
    /// <exception cref="ArgumentException">The name is blank or holds a non-token character.</exception>
    internal static string ValidateName(string name, string paramName)
    {
        ArgumentNullException.ThrowIfNull(name, paramName);
        var trimmed = name.Trim(' ', '\t');
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Header name must not be blank.", paramName);
        }

        for (var i = 0; i < trimmed.Length; i++)
        {
            if (!IsTokenChar(trimmed[i]))
            {
                throw new ArgumentException(
                    $"Header name contains the invalid character {CodePoint(trimmed[i])} at index {i}; "
                    + "a header name must be an RFC 9110 token.",
                    paramName);
            }
        }

        return trimmed;
    }

    /// <summary>Validates an outbound (caller-set) header value: HTAB and printable ASCII only (HTTP-18).</summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="name">The already-validated header name, which the message may echo.</param>
    /// <param name="paramName">The caller's parameter name, for the exception.</param>
    /// <exception cref="ArgumentException">The value holds a control or non-ASCII character.</exception>
    internal static void ValidateOutboundValue(string value, string name, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        var index = IndexOfInvalidOutbound(value);
        if (index >= 0)
        {
            throw InvalidValue(name, value[index], index, "outbound header values accept only HTAB and printable ASCII", paramName);
        }
    }

    /// <summary>
    /// Validates an inbound (received) header value leniently: obs-text is permitted, controls other than HTAB and DEL
    /// are not (HTTP-19, XCUT-18).
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <param name="name">The already-validated header name, which the message may echo.</param>
    /// <param name="paramName">The caller's parameter name, for the exception.</param>
    /// <exception cref="ArgumentException">The value holds a control character.</exception>
    internal static void ValidateInboundValue(string value, string name, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        for (var i = 0; i < value.Length; i++)
        {
            if (IsControl(value[i]))
            {
                throw InvalidValue(name, value[i], i, "header values must not contain control characters", paramName);
            }
        }
    }

    /// <summary>The index of the first character outside HTAB and printable ASCII, or <c>-1</c>.</summary>
    /// <param name="value">The value to scan.</param>
    /// <returns>The index, or <c>-1</c> when every character is valid.</returns>
    internal static int IndexOfInvalidOutbound(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (!IsOutboundValueChar(value[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The offending character as <c>U+XXXX</c>, the only form a message may carry (HTTP-20).</summary>
    /// <param name="c">The character.</param>
    /// <returns>The code-point label.</returns>
    internal static string CodePoint(char c) => string.Create(CultureInfo.InvariantCulture, $"U+{(int)c:X4}");

    /// <summary>An RFC 9110 <c>tchar</c> (the one character table lives in <see cref="HttpHeaderSyntax"/>).</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a token character.</returns>
    internal static bool IsTokenChar(char c) => HttpHeaderSyntax.IsTokenChar(c);

    private static bool IsOutboundValueChar(char c) => HttpHeaderSyntax.IsOutboundValueChar(c);

    private static bool IsControl(char c) => HttpHeaderSyntax.IsControl(c);

    private static ArgumentException InvalidValue(string name, char c, int index, string rule, string paramName) =>
        new($"The value of header '{name}' contains the invalid character {CodePoint(c)} at index {index}; {rule}.", paramName);
}
