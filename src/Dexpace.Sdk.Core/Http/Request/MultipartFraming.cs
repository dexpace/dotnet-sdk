// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;
using System.Text;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// The one multipart framing routine (HTTP-51, design P3b-8): boundary generation and validation, and the per-part header
/// and trailer bytes, computed once so the length and the write cannot drift.
/// </summary>
/// <remarks>
/// A part is framed as <c>--B CRLF headers CRLF body CRLF</c> and the body ends with <c>--B-- CRLF</c>. The header bytes
/// carry the part's <c>Content-Disposition</c> and, when its body has a media type, its <c>Content-Type</c>.
/// </remarks>
internal static class MultipartFraming
{
    private const string BoundaryAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const int MaxBoundaryLength = 70;
    private static readonly UTF8Encoding s_utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The precomputed bytes of a composite body.</summary>
    /// <param name="PartHeaders">For each part, its delimiter line and header block, ending with the blank line.</param>
    /// <param name="Trailer">The closing delimiter line.</param>
    internal sealed record Framing(byte[][] PartHeaders, byte[] Trailer);

    /// <summary><c>dexpace-</c> plus 32 random alphanumerics, a spec-valid boundary that is also a bare token.</summary>
    /// <returns>A new boundary.</returns>
    internal static string GenerateBoundary() => "dexpace-" + RandomNumberGenerator.GetString(BoundaryAlphabet, 32);

    /// <summary>Checks a caller boundary against RFC 2046: 1 to 70 <c>bchars</c>, not ending in a space.</summary>
    /// <param name="boundary">The boundary to check.</param>
    /// <exception cref="ArgumentException">The boundary is not a valid RFC 2046 boundary.</exception>
    internal static void ValidateBoundary(string boundary)
    {
        if (boundary.Length is 0 or > MaxBoundaryLength)
        {
            throw new ArgumentException($"A multipart boundary must be 1 to {MaxBoundaryLength} characters long.", nameof(boundary));
        }

        if (boundary[^1] == ' ')
        {
            throw new ArgumentException("A multipart boundary must not end in a space.", nameof(boundary));
        }

        foreach (var c in boundary)
        {
            if (!IsBoundaryChar(c))
            {
                throw new ArgumentException(
                    $"A multipart boundary holds the character {HeaderSyntax.CodePoint(c)}, which RFC 2046 does not allow.",
                    nameof(boundary));
            }
        }
    }

    /// <summary>The <c>multipart/form-data</c> media type carrying <paramref name="boundary"/>, quoted when it is not a token.</summary>
    /// <param name="boundary">A validated boundary.</param>
    /// <returns>The media type.</returns>
    internal static MediaType ContentTypeFor(string boundary) =>
        MediaType.Of("multipart", "form-data", new Dictionary<string, string> { ["boundary"] = boundary });

    /// <summary>Computes every part's header bytes and the trailer.</summary>
    /// <param name="parts">The parts, in order.</param>
    /// <param name="boundary">A validated boundary.</param>
    /// <returns>The framing bytes.</returns>
    internal static Framing Frame(IReadOnlyList<MultipartPart> parts, string boundary)
    {
        var headers = new byte[parts.Count][];
        for (var i = 0; i < parts.Count; i++)
        {
            headers[i] = s_utf8.GetBytes(PartHeader(parts[i], boundary));
        }

        return new Framing(headers, s_utf8.GetBytes($"--{boundary}--\r\n"));
    }

    private static string PartHeader(MultipartPart part, string boundary)
    {
        var header = new StringBuilder();
        header.Append("--").Append(boundary).Append("\r\n");
        header.Append("Content-Disposition: form-data; name=\"").Append(Escape(part.Name)).Append('"');
        if (part.FileName is not null)
        {
            header.Append("; filename=\"").Append(Escape(part.FileName)).Append('"');
        }

        header.Append("\r\n");
        var contentType = part.Body.ContentType?.ToString();
        if (contentType is not null)
        {
            // MediaType is validated at construction, so this cannot fire for a SDK type; RequestBody is open, so it is checked.
            var invalid = HeaderSyntax.IndexOfInvalidOutbound(contentType);
            if (invalid >= 0)
            {
                throw new ArgumentException(
                    $"A part's media type holds the character {HeaderSyntax.CodePoint(contentType[invalid])}, which cannot appear in a header.",
                    nameof(part));
            }

            header.Append("Content-Type: ").Append(contentType).Append("\r\n");
        }

        return header.Append("\r\n").ToString();
    }

    private static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    // RFC 2046 bchars: DIGIT / ALPHA / "'" / "(" / ")" / "+" / "_" / "," / "-" / "." / "/" / ":" / "=" / "?" / space.
    private static bool IsBoundaryChar(char c) =>
        c is (>= '0' and <= '9') or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')
            or '\'' or '(' or ')' or '+' or '_' or ',' or '-' or '.' or '/' or ':' or '=' or '?' or ' ';
}
