// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// The one RFC 3986 component encoder (HTTP-32; design §3.5): <c>Query</c> renders and parses through it, and
/// the transport projection (SEAM-27) and the pagination splice (PAGE-22) reuse it.
/// </summary>
/// <remarks>
/// Encoding leaves exactly the unreserved set (<c>A-Z a-z 0-9 - . _ ~</c>) and percent-encodes every other UTF-8
/// byte, so a space is <c>%20</c> and never <c>+</c>. Decoding is lenient and leaves <c>+</c> alone: it is not
/// <c>application/x-www-form-urlencoded</c>. A malformed escape, and an escaped sequence that is not valid UTF-8
/// (including an encoded lone surrogate), stay raw. A lone surrogate in the <em>input</em> of
/// <see cref="EncodeComponent"/> becomes U+FFFD's bytes, which is why <c>Query.Builder</c> rejects them first.
/// </remarks>
internal static class Rfc3986
{
    /// <summary>Percent-encodes <paramref name="value"/> as one RFC 3986 component.</summary>
    /// <param name="value">The text to encode.</param>
    /// <returns>The encoded component.</returns>
    internal static string EncodeComponent(string value) => Uri.EscapeDataString(value);

    /// <summary>Decodes percent-escapes in <paramref name="value"/> leniently; <c>+</c> stays <c>+</c>.</summary>
    /// <param name="value">The encoded component.</param>
    /// <returns>The decoded text.</returns>
    internal static string DecodeComponent(string value) => Uri.UnescapeDataString(value);
}
