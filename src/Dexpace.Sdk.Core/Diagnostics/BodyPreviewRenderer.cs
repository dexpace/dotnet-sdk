// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// Renders captured body bytes for a log event (OBS-38, P5b-14): text media types decode with the declared charset (else
/// UTF-8, with replacement characters, never throwing); anything else, an absent media type and <c>multipart/*</c> included,
/// renders as <c>[binary N bytes captured]</c>; empty input renders as the empty string.
/// </summary>
/// <remarks>The decision keys on the media type, never on a guess at the bytes: rendering unknown bytes as text is how a log line acquires a control character.</remarks>
internal static class BodyPreviewRenderer
{
    private static readonly FrozenSet<string> s_textSubtypes = new[]
    {
        "json", "xml", "x-www-form-urlencoded", "javascript", "x-ndjson", "yaml", "graphql",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Renders <paramref name="bytes"/> as a log-safe string.</summary>
    /// <param name="bytes">The captured bytes (at most the preview size).</param>
    /// <param name="mediaType">The body's media type, or <see langword="null"/> when it declares none.</param>
    /// <returns>The preview text.</returns>
    internal static string Render(ReadOnlySpan<byte> bytes, MediaType? mediaType)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        return IsText(mediaType)
            ? TextDecoding.Decode(bytes, mediaType!.Charset)
            : string.Create(CultureInfo.InvariantCulture, $"[binary {bytes.Length} bytes captured]");
    }

    private static bool IsText(MediaType? mediaType)
    {
        if (mediaType is null)
        {
            return false;
        }

        if (string.Equals(mediaType.Type, "text", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var subtype = mediaType.Subtype;
        return s_textSubtypes.Contains(subtype)
            || subtype.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
            || subtype.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)
            || subtype.EndsWith("+yaml", StringComparison.OrdinalIgnoreCase);
    }
}
