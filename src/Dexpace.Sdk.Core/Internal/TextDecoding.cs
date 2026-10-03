// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// The one routine both string readers decode through (HTTP-42, IO-13, P3b-12): the declared charset, else UTF-8, with a
/// leading byte-order mark skipped when it matches the resolved encoding's preamble.
/// </summary>
/// <remarks>
/// <see cref="Encoding.GetString(ReadOnlySpan{byte})"/> keeps a BOM as U+FEFF for every encoding. A BOM that does not
/// match the resolved charset is kept, because the declared charset wins.
/// </remarks>
internal static class TextDecoding
{
    /// <summary>Decodes <paramref name="bytes"/> with <paramref name="declared"/> (UTF-8 when <see langword="null"/>).</summary>
    /// <param name="bytes">The payload bytes.</param>
    /// <param name="declared">The encoding the response declared, or <see langword="null"/>.</param>
    /// <returns>The decoded text without a matching leading BOM.</returns>
    internal static string Decode(ReadOnlySpan<byte> bytes, Encoding? declared)
    {
        var encoding = declared ?? Encoding.UTF8;
        var preamble = encoding.Preamble;
        if (!preamble.IsEmpty && bytes.StartsWith(preamble))
        {
            bytes = bytes[preamble.Length..];
        }

        return encoding.GetString(bytes);
    }
}
