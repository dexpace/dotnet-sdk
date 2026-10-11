// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>Text helpers for messages that must not echo an oversized or hostile input.</summary>
internal static class Text
{
    /// <summary>The longest caller-supplied token a message may echo.</summary>
    internal const int MaxEchoLength = 64;

    /// <summary><paramref name="value"/> cut to <see cref="MaxEchoLength"/> characters, with an ellipsis when it was cut.</summary>
    /// <param name="value">The text to echo.</param>
    internal static string Truncate(string value) =>
        value.Length <= MaxEchoLength ? value : string.Concat(value.AsSpan(0, MaxEchoLength), "...");
}
