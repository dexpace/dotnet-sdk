// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Text;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// One alternative a call accepts: a scheme with optional scopes and free-form parameters.
/// </summary>
/// <remarks>
/// Scopes and parameters are preserved and never interpreted by the resolver (AUTH-2). Equality is by content: the
/// scheme, the scopes in order and the parameters; two requirements built from different but equal lists are equal.
/// </remarks>
public sealed record AuthRequirement
{
    private static readonly ImmutableDictionary<string, string> s_noParameters =
        ImmutableDictionary<string, string>.Empty.WithComparers(StringComparer.Ordinal);

    private ImmutableArray<string> _scopes = [];
    private ImmutableDictionary<string, string> _parameters = s_noParameters;

    /// <summary>Initializes a requirement for <paramref name="scheme"/>.</summary>
    /// <param name="scheme">The scheme.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scheme"/> is not a defined value.</exception>
    public AuthRequirement(AuthScheme scheme)
    {
        if (!Enum.IsDefined(scheme))
        {
            throw new ArgumentOutOfRangeException(nameof(scheme), scheme, "The authentication scheme is not a defined value.");
        }

        Scheme = scheme;
    }

    /// <summary>The anonymous requirement: a call that sends no credential.</summary>
    public static AuthRequirement NoAuth { get; } = new(AuthScheme.NoAuth);

    /// <summary>The scheme.</summary>
    public AuthScheme Scheme { get; }

    /// <summary>The scopes, copied at initialisation; empty by default.</summary>
    /// <exception cref="ArgumentNullException">The value, or an element of it, is <see langword="null"/>.</exception>
    public IReadOnlyList<string> Scopes
    {
        get => _scopes.IsDefault ? [] : _scopes;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            var builder = ImmutableArray.CreateBuilder<string>();
            foreach (var scope in value)
            {
                if (scope is null)
                {
                    throw new ArgumentException("A scope must not be null.", nameof(value));
                }

                builder.Add(scope);
            }

            _scopes = builder.ToImmutable();
        }
    }

    /// <summary>The free-form parameters, copied at initialisation with ordinal keys; empty by default.</summary>
    public IReadOnlyDictionary<string, string> Parameters
    {
        get => _parameters;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _parameters = s_noParameters.AddRange(value);
        }
    }

    /// <inheritdoc/>
    public bool Equals(AuthRequirement? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Scheme != other.Scheme || !Scopes.SequenceEqual(other.Scopes, StringComparer.Ordinal)
            || _parameters.Count != other._parameters.Count)
        {
            return false;
        }

        foreach (var (key, value) in _parameters)
        {
            if (!other._parameters.TryGetValue(key, out var theirs) || !string.Equals(value, theirs, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Scheme);
        foreach (var scope in Scopes)
        {
            hash.Add(scope, StringComparer.Ordinal);
        }

        var parameterHash = 0;
        foreach (var (key, value) in _parameters)
        {
            parameterHash += HashCode.Combine(key, value);
        }

        hash.Add(parameterHash);
        return hash.ToHashCode();
    }

    private bool PrintMembers(StringBuilder builder)
    {
        // A parameter value may be a tenant identifier; it is not printed.
        builder.Append("Scheme = ").Append(Scheme).Append(", Scopes = [").Append(string.Join(", ", Scopes)).Append(']');
        return true;
    }
}
