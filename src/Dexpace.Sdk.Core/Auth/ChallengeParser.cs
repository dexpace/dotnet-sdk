// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Text;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// The RFC 7235 section 2.1 challenge grammar as a single-pass, character-level state machine (AUTH-12, AUTH-13).
/// </summary>
/// <remarks>
/// The one ambiguity of the grammar, whether a token after a comma is a parameter name or a new scheme, is decided by a
/// one-token lookahead: a token followed by optional whitespace, <c>=</c> and then a character that is not <c>=</c> is a
/// parameter; anything else starts a new challenge. Every challenge read consumes at least one character, so the loop
/// terminates, and every character is scanned a bounded number of times, so the parse is linear.
/// </remarks>
internal static class ChallengeParser
{
    internal static IReadOnlyList<AuthenticationChallenge> Parse(ReadOnlySpan<char> value)
    {
        var challenges = new List<AuthenticationChallenge>();
        var reader = new Reader(value);
        while (true)
        {
            reader.SkipSeparators();
            if (reader.AtEnd)
            {
                return challenges;
            }

            var before = reader.Position;
            ReadChallenge(ref reader, challenges);
            if (reader.Position == before)
            {
                reader.Advance();
            }
        }
    }

    internal static string FoldAscii(string text)
    {
        var needsFold = false;
        foreach (var c in text)
        {
            if (c is >= 'A' and <= 'Z')
            {
                needsFold = true;
                break;
            }
        }

        return needsFold ? string.Create(text.Length, text, static (span, source) => Fold(source, span)) : text;
    }

    private static void Fold(ReadOnlySpan<char> source, Span<char> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            destination[i] = c is >= 'A' and <= 'Z' ? (char)(c + ('a' - 'A')) : c;
        }
    }

    private static void ReadChallenge(ref Reader reader, List<AuthenticationChallenge> challenges)
    {
        if (!reader.TryReadToken(out var scheme))
        {
            reader.SkipToTopLevelComma();
            return;
        }

        var foldedScheme = FoldAscii(scheme.ToString());
        var afterScheme = reader.Position;
        reader.SkipBlanks();
        if (reader.Peek == '=')
        {
            // A parameter with no scheme ahead of it: discard the element.
            reader.SkipToTopLevelComma();
            return;
        }

        var parameters = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (reader.Position == afterScheme && !reader.AtEnd && reader.Peek != ',')
        {
            // The scheme is not followed by SP: keep the challenge, abandon the rest of the element.
            reader.SkipToTopLevelComma();
        }
        else if (!reader.AtEnd && reader.Peek != ',' && !reader.TryReadToken68(parameters))
        {
            ReadParameters(ref reader, parameters);
        }

        challenges.Add(new AuthenticationChallenge(foldedScheme, parameters.ToImmutable()));
    }

    private static void ReadParameters(ref Reader reader, ImmutableDictionary<string, string>.Builder parameters)
    {
        while (true)
        {
            if (reader.LooksLikeParameter() && reader.TryReadParameter(out var name, out var value))
            {
                parameters.TryAdd(FoldAscii(name), value);
                reader.SkipBlanks();
            }

            // A malformed element, or junk after a parameter: abandon it, keep what was parsed.
            if (!reader.AtEnd && reader.Peek != ',')
            {
                reader.SkipToTopLevelComma();
            }

            reader.SkipSeparators();
            if (reader.AtEnd || !(reader.LooksLikeParameter() || reader.StartsMalformedElement))
            {
                return;
            }
        }
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<char> _text;
        private int _position;

        internal Reader(ReadOnlySpan<char> text)
        {
            _text = text;
            _position = 0;
        }

        internal readonly int Position => _position;

        internal readonly bool AtEnd => _position >= _text.Length;

        internal readonly char Peek => _position < _text.Length ? _text[_position] : '\0';

        internal readonly bool StartsMalformedElement => !AtEnd && !IsTokenChar(Peek);

        internal void Advance() => _position++;

        internal void SkipBlanks()
        {
            while (_position < _text.Length && _text[_position] is ' ' or '\t')
            {
                _position++;
            }
        }

        internal void SkipSeparators()
        {
            while (_position < _text.Length && _text[_position] is ' ' or '\t' or ',')
            {
                _position++;
            }
        }

        internal bool TryReadToken(out ReadOnlySpan<char> token)
        {
            var start = _position;
            while (_position < _text.Length && IsTokenChar(_text[_position]))
            {
                _position++;
            }

            token = _text[start.._position];
            return _position > start;
        }

        // token68 = 1*( ALPHA / DIGIT / "-" / "." / "_" / "~" / "+" / "/" ) *"=", then a comma or the end.
        internal bool TryReadToken68(ImmutableDictionary<string, string>.Builder parameters)
        {
            var start = _position;
            var p = start;
            while (p < _text.Length && IsToken68Char(_text[p]))
            {
                p++;
            }

            if (p == start)
            {
                return false;
            }

            while (p < _text.Length && _text[p] == '=')
            {
                p++;
            }

            var end = p;
            while (p < _text.Length && _text[p] is ' ' or '\t')
            {
                p++;
            }

            if (p < _text.Length && _text[p] != ',')
            {
                return false;
            }

            parameters.TryAdd(AuthenticationChallenge.Token68Key, _text[start..end].ToString());
            _position = p;
            return true;
        }

        internal readonly bool LooksLikeParameter()
        {
            var p = _position;
            var start = p;
            while (p < _text.Length && IsTokenChar(_text[p]))
            {
                p++;
            }

            if (p == start)
            {
                return false;
            }

            while (p < _text.Length && _text[p] is ' ' or '\t')
            {
                p++;
            }

            return p < _text.Length - 1 && _text[p] == '=' && _text[p + 1] != '=';
        }

        internal bool TryReadParameter(out string name, out string value)
        {
            name = string.Empty;
            value = string.Empty;
            if (!TryReadToken(out var nameSpan))
            {
                return false;
            }

            SkipBlanks();
            if (Peek != '=')
            {
                return false;
            }

            _position++;
            SkipBlanks();
            name = nameSpan.ToString();
            if (Peek == '"')
            {
                value = ReadQuoted();
                return true;
            }

            if (TryReadToken(out var valueSpan))
            {
                value = valueSpan.ToString();
                return true;
            }

            return false;
        }

        // Positioned at the opening quote. An unterminated string ends at the end of the input; a trailing backslash is dropped.
        private string ReadQuoted()
        {
            _position++;
            var builder = new StringBuilder();
            while (_position < _text.Length)
            {
                var c = _text[_position++];
                if (c == '"')
                {
                    break;
                }

                if (c == '\\')
                {
                    if (_position >= _text.Length)
                    {
                        break;
                    }

                    c = _text[_position++];
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        // Skips to the next comma outside a quoted string, leaving the position on it (or at the end).
        internal void SkipToTopLevelComma()
        {
            var inQuote = false;
            while (_position < _text.Length)
            {
                var c = _text[_position];
                if (inQuote)
                {
                    if (c == '\\')
                    {
                        _position++;
                    }
                    else if (c == '"')
                    {
                        inQuote = false;
                    }
                }
                else if (c == '"')
                {
                    inQuote = true;
                }
                else if (c == ',')
                {
                    return;
                }

                _position++;
            }

            _position = _text.Length;
        }

        private static bool IsTokenChar(char c) =>
            c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';

        private static bool IsToken68Char(char c) =>
            c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '.' or '_' or '~' or '+' or '/';
    }
}
