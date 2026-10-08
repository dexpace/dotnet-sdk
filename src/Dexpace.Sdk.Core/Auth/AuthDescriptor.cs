// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Text;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// An ordered set of alternative <see cref="AuthRequirement"/>s, strongest preference first (AUTH-3).
/// </summary>
/// <remarks>
/// The requirements are copied at construction and exposed read-only. There is no <c>init</c> member, so a
/// <c>with</c> expression cannot empty the descriptor (P6c-4). Equality is by the ordered requirements.
/// </remarks>
public sealed record AuthDescriptor
{
    private readonly ImmutableArray<AuthRequirement> _requirements;

    /// <summary>Initializes a descriptor over <paramref name="requirements"/>.</summary>
    /// <param name="requirements">The alternatives, in preference order.</param>
    /// <exception cref="ArgumentException">There are none, or one is <see langword="null"/>.</exception>
    public AuthDescriptor(params IEnumerable<AuthRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        var builder = ImmutableArray.CreateBuilder<AuthRequirement>();
        foreach (var requirement in requirements)
        {
            if (requirement is null)
            {
                throw new ArgumentException("A requirement must not be null.", nameof(requirements));
            }

            builder.Add(requirement);
        }

        if (builder.Count == 0)
        {
            throw new ArgumentException("A descriptor needs at least one requirement.", nameof(requirements));
        }

        _requirements = builder.ToImmutable();
        AllowsAnonymous = _requirements.Any(static r => r.Scheme == AuthScheme.NoAuth);
    }

    /// <summary>The alternatives, in preference order.</summary>
    public IReadOnlyList<AuthRequirement> Requirements => _requirements;

    /// <summary><see langword="true"/> when any requirement is <see cref="AuthScheme.NoAuth"/>.</summary>
    public bool AllowsAnonymous { get; }

    /// <inheritdoc/>
    public bool Equals(AuthDescriptor? other) =>
        other is not null && (ReferenceEquals(this, other) || _requirements.AsSpan().SequenceEqual(other._requirements.AsSpan()));

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var requirement in _requirements)
        {
            hash.Add(requirement);
        }

        return hash.ToHashCode();
    }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Requirements = [").Append(string.Join(", ", _requirements.Select(static r => r.Scheme))).Append(']');
        return true;
    }
}
