// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>Bounds the strings the SDK logs (OBS-7, P5b-15).</summary>
internal static class LogText
{
    /// <summary>The most UTF-16 chars kept of a logged URL or header value.</summary>
    internal const int Limit = 8192;

    /// <summary>The marker appended to a cut value.</summary>
    internal const string Suffix = "…[truncated]";

    /// <summary>Cuts <paramref name="value"/> to <see cref="Limit"/> chars plus <see cref="Suffix"/>, never inside a surrogate pair.</summary>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns>The value itself when it fits, otherwise the cut form.</returns>
    internal static string? Truncate(string? value)
    {
        if (value is null || value.Length <= Limit)
        {
            return value;
        }

        // A high surrogate at the last kept index would be stranded from its low half: drop it with the pair.
        var keep = char.IsHighSurrogate(value[Limit - 1]) ? Limit - 1 : Limit;
        return string.Concat(value.AsSpan(0, keep), Suffix);
    }
}
