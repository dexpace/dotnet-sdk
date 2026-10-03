// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// The WHATWG <c>application/x-www-form-urlencoded</c> serializer (HTTP-38, design P3b-7), hand-written because none of
/// the built-in encoders is it: <c>WebUtility.UrlEncode</c> leaves <c>!()</c> literal and <c>Uri.EscapeDataString</c> is
/// RFC 3986 and writes <c>%20</c> for a space (design fact 6).
/// </summary>
/// <remarks>
/// Each name and value is UTF-8 encoded strictly (a lone surrogate throws instead of becoming U+FFFD). The bytes
/// <c>A-Z a-z 0-9 * - . _</c> are literal, a space is <c>+</c>, and every other byte is <c>%XX</c> in upper-case hex.
/// Pairs are joined with <c>&amp;</c> and a name from its value with <c>=</c>; order and duplicates are kept.
/// </remarks>
internal static class FormUrlEncoder
{
    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static ReadOnlySpan<byte> HexDigits => "0123456789ABCDEF"u8;

    /// <summary>Serializes <paramref name="fields"/> to the wire bytes.</summary>
    /// <param name="fields">The name/value pairs, in order.</param>
    /// <returns>The encoded bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> is null, or a name or value is null.</exception>
    /// <exception cref="ArgumentException">A name or value holds a lone surrogate; the message names the field index only.</exception>
    internal static byte[] Encode(IEnumerable<KeyValuePair<string, string>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var output = new ArrayBufferWriter<byte>();
        var index = 0;
        foreach (var (name, value) in fields)
        {
            if (index > 0)
            {
                output.Write("&"u8);
            }

            AppendField(output, name, index, "name");
            output.Write("="u8);
            AppendField(output, value, index, "value");
            index++;
        }

        return output.WrittenSpan.ToArray();
    }

    private static void AppendField(ArrayBufferWriter<byte> output, string? text, int index, string part)
    {
        if (text is null)
        {
            throw new ArgumentNullException(paramName: null, $"The {part} of field {index} is null.");
        }

        byte[] bytes;
        try
        {
            bytes = s_strictUtf8.GetBytes(text);
        }
        catch (EncoderFallbackException)
        {
            throw new ArgumentException(
                $"The {part} of field {index} is not valid Unicode text (it holds a lone surrogate).");
        }

        foreach (var b in bytes)
        {
            AppendByte(output, b);
        }
    }

    private static void AppendByte(ArrayBufferWriter<byte> output, byte b)
    {
        if (b is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9')
            or (byte)'*' or (byte)'-' or (byte)'.' or (byte)'_')
        {
            output.Write([b]);
        }
        else if (b == (byte)' ')
        {
            output.Write("+"u8);
        }
        else
        {
            output.Write([(byte)'%', HexDigits[b >> 4], HexDigits[b & 0xF]]);
        }
    }
}
