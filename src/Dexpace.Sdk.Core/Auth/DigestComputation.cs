// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// The pure arithmetic and text forms of RFC 7616 Digest (AUTH-17, AUTH-20 to AUTH-22; design §6.3): hashing, the credential
/// encodings, quoting and the RFC 8187 form of a non-ASCII username. Stateless.
/// </summary>
internal static class DigestComputation
{
    // The charsets of AUTH-21. Both throw on a character they cannot represent, so a credential is never hashed with a
    // silent '?' substituted (fact 6, P6c-32).
    private static readonly Encoding s_utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding s_latin1 = Encoding.GetEncoding("iso-8859-1", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

    // Encodes text under the challenge's charset; false when a character has no code in it (AUTH-21).
    internal static bool TryEncode(string text, bool utf8, out byte[] bytes)
    {
        try
        {
            bytes = (utf8 ? s_utf8 : s_latin1).GetBytes(text);
            return true;
        }
        catch (EncoderFallbackException)
        {
            bytes = [];
            return false;
        }
    }

    // The response value (AUTH-17): qop == null is the legacy RFC 2069 form, otherwise qop is "auth".
    internal static string Compute(
        DigestAlgorithm algorithm,
        byte[] username,
        byte[] realm,
        byte[] password,
        string nonce,
        string method,
        string digestUri,
        string? qop,
        string nc,
        string cnonce)
    {
        var md5 = DigestAlgorithmNames.IsMd5(algorithm);
        var ha1 = ComputeHa1(md5, DigestAlgorithmNames.IsSession(algorithm), username, realm, password, nonce, cnonce);
        var ha2 = HashHex(md5, Encoding.UTF8.GetBytes($"{method}:{digestUri}"));
        var input = qop is null ? $"{ha1}:{nonce}:{ha2}" : $"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}";
        return HashHex(md5, Encoding.UTF8.GetBytes(input));
    }

    // H(username:realm:password), keyed with the nonce and cnonce for a -sess algorithm.
    internal static string ComputeHa1(bool md5, bool session, byte[] username, byte[] realm, byte[] password, string nonce, string cnonce)
    {
        var joined = new byte[username.Length + realm.Length + password.Length + 2];
        username.CopyTo(joined, 0);
        joined[username.Length] = (byte)':';
        realm.CopyTo(joined, username.Length + 1);
        joined[username.Length + 1 + realm.Length] = (byte)':';
        password.CopyTo(joined, username.Length + realm.Length + 2);
        var ha1 = HashHex(md5, joined);
        return session ? HashHex(md5, Encoding.UTF8.GetBytes($"{ha1}:{nonce}:{cnonce}")) : ha1;
    }

    // Lower-case hex only through Convert.ToHexStringLower (AUTH-17, P6c-38). MD5 is the protocol's own hash (RFC 2617 and
    // RFC 7616 define the response over it); it is offered only when a server asks for it and the host allows it (AUTH-15).
#pragma warning disable CA5351
    internal static string HashHex(bool md5, byte[] data) =>
        Convert.ToHexStringLower(md5 ? MD5.HashData(data) : SHA256.HashData(data));
#pragma warning restore CA5351

    // The request-target the transport writes (fact 9): the escaped path and query, "/" for an empty one (P6c-34).
    internal static string DigestUri(Uri url)
    {
        var target = url.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);
        return target.Length == 0 ? "/" : target;
    }

    // RFC 7616 section 3.4: quoted-string escaping of a quote and a backslash (AUTH-22).
    internal static string Quote(string value) =>
        value.Contains('"', StringComparison.Ordinal) || value.Contains('\\', StringComparison.Ordinal)
            ? value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
            : value;

    internal static string Nc(uint count) => count.ToString("x8", CultureInfo.InvariantCulture);

    internal static bool IsPrintableAscii(string text)
    {
        foreach (var c in text)
        {
            if (c is < ' ' or > '~')
            {
                return false;
            }
        }

        return true;
    }

    // RFC 8187 section 3.2.1 (R14): percent-encode every byte outside attr-char, upper-case hex, with the charset prefix.
    internal static string Rfc8187Encode(byte[] bytes, bool utf8)
    {
        var builder = new StringBuilder(utf8 ? "UTF-8''" : "ISO-8859-1''");
        foreach (var b in bytes)
        {
            if (IsAttrChar(b))
            {
                builder.Append((char)b);
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static bool IsAttrChar(byte b) =>
        b is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9')
            or (byte)'!' or (byte)'#' or (byte)'$' or (byte)'&' or (byte)'+' or (byte)'-' or (byte)'.' or (byte)'^'
            or (byte)'_' or (byte)'`' or (byte)'|' or (byte)'~';
}
