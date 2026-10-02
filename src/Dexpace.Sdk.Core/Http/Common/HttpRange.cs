// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// A single byte range for a <c>Range</c> request header (RFC 9110 §14.1.2; HTTP-49).
/// </summary>
/// <remarks>
/// Three shapes exist: <see cref="Bounded"/> (<c>bytes=o-(o+l-1)</c>), <see cref="Suffix"/> (<c>bytes=-l</c>) and
/// <see cref="From"/> (<c>bytes=o-</c>). <see cref="Parse"/> accepts only the <c>bytes</c> unit (compared
/// case-insensitively) and exactly one range, and keeps the text verbatim for <see cref="ToString"/>. Equality is
/// semantic, over <see cref="Offset"/> and <see cref="Length"/>: the verbatim text is excluded from equality and the
/// hash, so <c>Bytes=0-9</c> equals <c>Bounded(0, 10)</c>. Set the header with
/// <c>headers.Set(HttpHeaderName.WellKnown.Range, range.ToString())</c>.
/// </remarks>
public sealed record HttpRange
{
    private readonly string? _text;

    private HttpRange(long? offset, long? length, string? text)
    {
        Offset = offset;
        Length = length;
        _text = text;
    }

    /// <summary>The first byte offset; <see langword="null"/> for a suffix range.</summary>
    public long? Offset { get; }

    /// <summary>The number of bytes (or the suffix length); <see langword="null"/> for an open-ended range.</summary>
    public long? Length { get; }

    /// <summary>Creates <c>bytes=offset-(offset+length-1)</c>.</summary>
    /// <param name="offset">The first byte's offset; not negative.</param>
    /// <param name="length">The number of bytes; positive.</param>
    /// <returns>The bounded range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is negative, <paramref name="length"/> is not positive, or
    /// <c>offset + length - 1</c> overflows a 64-bit integer.
    /// </exception>
    public static HttpRange Bounded(long offset, long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (offset > long.MaxValue - (length - 1))
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The last byte of the range overflows a 64-bit integer.");
        }

        return new HttpRange(offset, length, null);
    }

    /// <summary>Creates <c>bytes=-length</c>: the last <paramref name="length"/> bytes.</summary>
    /// <param name="length">The suffix length; positive.</param>
    /// <returns>The suffix range.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is not positive.</exception>
    public static HttpRange Suffix(long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        return new HttpRange(null, length, null);
    }

    /// <summary>Creates <c>bytes=offset-</c>: every byte from <paramref name="offset"/> on.</summary>
    /// <param name="offset">The first byte's offset; not negative.</param>
    /// <returns>The open-ended range.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    public static HttpRange From(long offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        return new HttpRange(offset, null, null);
    }

    /// <summary>Parses one <c>bytes</c> range, keeping the text verbatim for <see cref="ToString"/>.</summary>
    /// <param name="value">The header value, such as <c>bytes=0-499</c>.</param>
    /// <returns>The parsed range.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is not <c>bytes=first-last</c>, <c>bytes=first-</c> or <c>bytes=-length</c> with decimal
    /// 64-bit numbers, names another unit, holds more than one range (a comma), or has <c>last</c> below
    /// <c>first</c>. The message never echoes the input.
    /// </exception>
    public static HttpRange Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return TryParseCore(value) ?? throw new ArgumentException(
            "A range must be a single bytes range: bytes=first-last, bytes=first- or bytes=-length.",
            nameof(value));
    }

    /// <summary>Parses like <see cref="Parse"/> without throwing.</summary>
    /// <param name="value">The header value.</param>
    /// <param name="range">The range, or <see langword="null"/> when parsing failed.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a well-formed single bytes range.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out HttpRange? range)
    {
        range = value is null ? null : TryParseCore(value);
        return range is not null;
    }

    /// <summary>Semantic equality over <see cref="Offset"/> and <see cref="Length"/>; the verbatim text is ignored.</summary>
    /// <param name="other">The range to compare with.</param>
    /// <returns><see langword="true"/> when both describe the same bytes.</returns>
    public bool Equals(HttpRange? other) => other is not null && Offset == other.Offset && Length == other.Length;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Offset, Length);

    /// <summary>The verbatim text when parsed, else the canonical <c>bytes=...</c> form.</summary>
    /// <returns>The header value.</returns>
    public override string ToString() => _text ?? Canonical();

    private string Canonical() => (Offset, Length) switch
    {
        ({ } o, { } l) => string.Create(CultureInfo.InvariantCulture, $"bytes={o}-{o + l - 1}"),
        (null, { } l) => string.Create(CultureInfo.InvariantCulture, $"bytes=-{l}"),
        ({ } o, null) => string.Create(CultureInfo.InvariantCulture, $"bytes={o}-"),
        _ => "bytes=",
    };

    private static HttpRange? TryParseCore(string value)
    {
        var trimmed = value.Trim(' ', '\t');
        const string Unit = "bytes=";
        if (!trimmed.StartsWith(Unit, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var spec = trimmed.AsSpan(Unit.Length);
        var dash = spec.IndexOf('-');
        if (dash < 0)
        {
            return null;
        }

        var first = spec[..dash];
        var last = spec[(dash + 1)..];
        if (first.IsEmpty)
        {
            return TryNumber(last, out var suffixLength) && suffixLength > 0 ? new HttpRange(null, suffixLength, trimmed) : null;
        }

        if (!TryNumber(first, out var offset))
        {
            return null;
        }

        if (last.IsEmpty)
        {
            return new HttpRange(offset, null, trimmed);
        }

        // last >= first; the length last - first + 1 must fit a 64-bit integer.
        return TryNumber(last, out var end) && end >= offset && end - offset != long.MaxValue
            ? new HttpRange(offset, end - offset + 1, trimmed)
            : null;
    }

    // Decimal digits only: no sign, space, hex or fraction; a value that overflows a long is not a number here.
    private static bool TryNumber(ReadOnlySpan<char> digits, out long number) =>
        long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number);
}
