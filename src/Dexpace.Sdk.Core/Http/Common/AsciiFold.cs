// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// ASCII-only, culture-independent case folding for header names (HTTP-13): never <c>ToLowerInvariant</c>, so the
/// Kelvin sign (U+212A) does not fold to <c>k</c> and a Turkish culture cannot change a name.
/// </summary>
internal static class AsciiFold
{
    /// <summary>Lower-cases an all-ASCII <paramref name="value"/>; a value already lower case is returned as is.</summary>
    /// <param name="value">An ASCII string (a validated token).</param>
    /// <returns>The folded string.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> holds a non-ASCII character.</exception>
    internal static string ToLower(string value) =>
        TryToLower(value) ?? throw new ArgumentException("A header name must be ASCII.", nameof(value));

    /// <summary>
    /// Lower-cases <paramref name="value"/> when it is all ASCII, or returns <see langword="null"/> when it holds a
    /// non-ASCII character, which no stored name can contain (so a lookup with it finds nothing).
    /// </summary>
    /// <param name="value">The candidate name.</param>
    /// <returns>The folded string, or <see langword="null"/> for non-ASCII input.</returns>
    internal static string? TryToLower(string value)
    {
        if (!Ascii.IsValid(value))
        {
            return null;
        }

        if (!value.AsSpan().ContainsAnyInRange('A', 'Z'))
        {
            return value;
        }

        return string.Create(value.Length, value, static (span, source) => Ascii.ToLower(source, span, out _));
    }
}
