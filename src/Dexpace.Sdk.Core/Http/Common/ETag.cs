// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Dexpace.Sdk.Core.Http.Common;

/// <summary>
/// An entity tag (RFC 9110 §8.8.3): a strong tag, a weak tag, or the <c>*</c> wildcard (HTTP-48).
/// </summary>
/// <remarks>
/// The opaque characters are <c>etagc</c>: <c>0x21</c>, <c>0x23</c> to <c>0x7E</c> and obs-text (<c>0x80</c> to
/// <c>0xFF</c>); the quote and the space are not. A strong opaque must not be empty, while a weak one may be.
/// <see cref="ToString"/> renders exactly the accepted spelling (<c>*</c>, <c>"x"</c>, <c>W/"x"</c>), so the text
/// round-trips through <see cref="Parse"/>. Equality is by kind and opaque. An obs-text tag is valid here, but
/// <see cref="Request.RequestConditions"/> cannot send it, because header values are held to the outbound ASCII rule
/// (P2a-3: the outbound rule wins). Construct through <see cref="Any"/>, <see cref="Strong"/>, <see cref="Weak"/>
/// or <see cref="Parse"/>.
/// </remarks>
public sealed record ETag
{
    private ETag(bool isAny, bool isWeak, string opaque)
    {
        IsAny = isAny;
        IsWeak = isWeak;
        Opaque = opaque;
    }

    /// <summary>The <c>*</c> wildcard, which matches any current representation.</summary>
    public static ETag Any { get; } = new(isAny: true, isWeak: false, opaque: string.Empty);

    /// <summary>True for the <c>*</c> wildcard.</summary>
    public bool IsAny { get; }

    /// <summary>True for a weak tag (<c>W/"x"</c>).</summary>
    public bool IsWeak { get; }

    /// <summary>The opaque tag without its quotes; <c>""</c> for <see cref="Any"/>.</summary>
    public string Opaque { get; }

    /// <summary>Creates a strong tag.</summary>
    /// <param name="opaque">The opaque tag: non-empty, <c>etagc</c> characters only.</param>
    /// <returns>The strong <see cref="ETag"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="opaque"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="opaque"/> is empty or holds a character that is not <c>etagc</c>. The message names the code point
    /// and never echoes the text.
    /// </exception>
    public static ETag Strong(string opaque)
    {
        ArgumentNullException.ThrowIfNull(opaque);
        if (opaque.Length == 0)
        {
            throw new ArgumentException("A strong entity tag must not be empty.", nameof(opaque));
        }

        RequireEtagc(opaque);
        return new ETag(isAny: false, isWeak: false, opaque);
    }

    /// <summary>Creates a weak tag.</summary>
    /// <param name="opaque">The opaque tag: <c>etagc</c> characters only; it may be empty.</param>
    /// <returns>The weak <see cref="ETag"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="opaque"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="opaque"/> holds a character that is not <c>etagc</c>.
    /// </exception>
    public static ETag Weak(string opaque)
    {
        ArgumentNullException.ThrowIfNull(opaque);
        RequireEtagc(opaque);
        return new ETag(isAny: false, isWeak: true, opaque);
    }

    /// <summary>
    /// Parses <c>*</c>, <c>"x"</c> or <c>W/"x"</c> (surrounding SP/HTAB is ignored). Blank input is absent, not an error.
    /// </summary>
    /// <param name="value">The header text.</param>
    /// <returns>The tag, or <see langword="null"/> when <paramref name="value"/> is <see langword="null"/> or blank.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is malformed: unterminated, unquoted, a lower-case <c>w/</c>, an empty strong tag, or an
    /// opaque with a character that is not <c>etagc</c>. The message never echoes the input.
    /// </exception>
    public static ETag? Parse(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.Trim(' ', '\t');
        if (text.Length == 0)
        {
            return null;
        }

        if (text == "*")
        {
            return Any;
        }

        var weak = text.StartsWith("W/", StringComparison.Ordinal);
        var quoted = weak ? text[2..] : text;
        if (quoted.Length < 2 || quoted[0] != '"' || quoted[^1] != '"')
        {
            throw new ArgumentException("An entity tag must be *, \"opaque\" or W/\"opaque\".", nameof(value));
        }

        var opaque = quoted[1..^1];
        return weak ? Weak(opaque) : Strong(opaque);
    }

    /// <summary>Parses like <see cref="Parse"/> without throwing.</summary>
    /// <param name="value">The header text.</param>
    /// <param name="etag">The tag, or <see langword="null"/> when parsing failed or the input is blank.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a well-formed, non-blank entity tag.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out ETag? etag)
    {
        try
        {
            etag = Parse(value);
            return etag is not null;
        }
        catch (ArgumentException)
        {
            etag = null;
            return false;
        }
    }

    /// <summary>Renders <c>*</c>, <c>"x"</c> or <c>W/"x"</c>: exactly the spelling <see cref="Parse"/> accepts.</summary>
    /// <returns>The wire text.</returns>
    public override string ToString() => IsAny ? "*" : IsWeak ? $"W/\"{Opaque}\"" : $"\"{Opaque}\"";

    private static bool IsEtagc(char c) => c is '!' or (>= '#' and <= '~') or (>= '\u0080' and <= 'ÿ');

    private static void RequireEtagc(string opaque)
    {
        for (var i = 0; i < opaque.Length; i++)
        {
            if (!IsEtagc(opaque[i]))
            {
                throw new ArgumentException(
                    $"An entity tag contains the invalid character {HeaderSyntax.CodePoint(opaque[i])} at index "
                    + $"{i.ToString(CultureInfo.InvariantCulture)}; only etagc characters are allowed.",
                    nameof(opaque));
            }
        }
    }
}
