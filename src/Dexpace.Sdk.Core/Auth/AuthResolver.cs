// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// Picks the requirement a call is authenticated with (AUTH-4 to AUTH-7).
/// </summary>
/// <remarks>
/// A pure, stateless function. The tiers are, in order, the per-call descriptor, the operation descriptor and the
/// client descriptor; the first tier that is present is the only one consulted, so a higher tier that cannot be
/// satisfied never falls through to a lower one (AUTH-4).
/// </remarks>
public static class AuthResolver
{
    /// <summary>Resolves the requirement to use.</summary>
    /// <param name="perCall">The per-call tier, or <see langword="null"/>.</param>
    /// <param name="operation">The operation tier, or <see langword="null"/>.</param>
    /// <param name="client">The client tier, or <see langword="null"/>.</param>
    /// <param name="availableSchemes">The schemes the caller can serve.</param>
    /// <returns>The first requirement in preference order that is <see cref="AuthScheme.NoAuth"/> or available.</returns>
    /// <exception cref="ArgumentException">All three tiers are <see langword="null"/>.</exception>
    /// <exception cref="AuthResolutionException">No requirement of the selected tier is satisfiable.</exception>
    public static AuthRequirement Resolve(
        AuthDescriptor? perCall,
        AuthDescriptor? operation,
        AuthDescriptor? client,
        IReadOnlyCollection<AuthScheme> availableSchemes)
    {
        ArgumentNullException.ThrowIfNull(availableSchemes);
        var selected = perCall ?? operation ?? client
            ?? throw new ArgumentException("At least one authentication tier must be present.");

        foreach (var requirement in selected.Requirements)
        {
            if (requirement.Scheme == AuthScheme.NoAuth || availableSchemes.Contains(requirement.Scheme))
            {
                return requirement;
            }
        }

        var required = selected.Requirements.Select(static r => r.Scheme).Distinct().ToArray();
        var available = availableSchemes.Distinct().OrderBy(static s => (int)s).ToArray();
        throw new AuthResolutionException(required, available);
    }
}
