// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Text;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// One parsed RFC 7235 authentication challenge: a scheme and its parameters (AUTH-12).
/// </summary>
/// <remarks>
/// <para>
/// The scheme and the parameter names are lower-cased with an ASCII-only fold; parameter values are kept verbatim
/// (quotes stripped and escapes resolved). A <c>token68</c> value sits under the synthetic key
/// <see cref="Token68Key"/>. A duplicate parameter name keeps the <b>first</b> value (P6c-15): RFC 7235 says each name
/// occurs once per challenge, and first-wins means a hostile later duplicate cannot overwrite a realm a handler already
/// compared.
/// </para>
/// <para>
/// <see cref="Parse(string?)"/> is lenient (AUTH-13): it never throws, it is linear in the input and it uses no regular
/// expression. A scheme that is not a token, a parameter without <c>=</c>, or a value that is neither a token nor a
/// quoted string abandons the current element and resumes after the next top-level (unquoted) comma, keeping the
/// challenge and the parameters parsed so far. An unterminated quoted string ends at the end of the input with its
/// content kept; a trailing backslash is dropped. Blank or <see langword="null"/> input yields an empty list.
/// </para>
/// </remarks>
public sealed class AuthenticationChallenge
{
    /// <summary>The synthetic parameter key a <c>token68</c> value is recorded under.</summary>
    public const string Token68Key = "token68";

    /// <summary>Initializes a challenge, folding the scheme and the parameter names to lower case.</summary>
    /// <param name="scheme">The authentication scheme. Must not be blank.</param>
    /// <param name="parameters">The parameters, copied; the first of two names that fold equal is kept.</param>
    /// <exception cref="ArgumentException"><paramref name="scheme"/> is blank.</exception>
    public AuthenticationChallenge(string scheme, IReadOnlyDictionary<string, string>? parameters = null)
        : this(RequireScheme(scheme), Fold(parameters))
    {
    }

    internal AuthenticationChallenge(string foldedScheme, ImmutableDictionary<string, string> foldedParameters)
    {
        Scheme = foldedScheme;
        Parameters = foldedParameters;
    }

    /// <summary>The scheme, in ASCII lower case.</summary>
    public string Scheme { get; }

    /// <summary>The parameters, with lower-case names and verbatim values.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>The <c>token68</c> value, or <see langword="null"/> when the challenge carries none.</summary>
    public string? Token68 => Parameters.TryGetValue(Token68Key, out var value) ? value : null;

    /// <summary>Parses every challenge in one header field value, in wire order.</summary>
    /// <param name="value">The field value, or <see langword="null"/>.</param>
    /// <returns>The challenges; empty for blank input. Never throws.</returns>
    public static IReadOnlyList<AuthenticationChallenge> Parse(string? value) =>
        value is null ? [] : ChallengeParser.Parse(value.AsSpan());

    /// <summary>Parses every challenge in one header field value, in wire order.</summary>
    /// <param name="value">The field value.</param>
    /// <returns>The challenges; empty for blank input. Never throws.</returns>
    public static IReadOnlyList<AuthenticationChallenge> Parse(ReadOnlySpan<char> value) => ChallengeParser.Parse(value);

    /// <summary>Renders the scheme and the parameter names; values are never printed (they may carry nonces).</summary>
    /// <returns>A description without parameter values.</returns>
    public override string ToString() =>
        new StringBuilder("AuthenticationChallenge { Scheme = ")
            .Append(Scheme)
            .Append(", Parameters = [")
            .AppendJoin(", ", Parameters.Keys)
            .Append("] }")
            .ToString();

    private static string RequireScheme(string scheme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        return ChallengeParser.FoldAscii(scheme.Trim());
    }

    private static ImmutableDictionary<string, string> Fold(IReadOnlyDictionary<string, string>? parameters)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                builder.TryAdd(ChallengeParser.FoldAscii(name), value);
            }
        }

        return builder.ToImmutable();
    }
}
