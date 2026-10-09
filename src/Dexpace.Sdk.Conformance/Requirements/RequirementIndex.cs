// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The requirement catalogue's lookup. The task-1.3 stub accepts any non-empty ID; task 1.5 replaces it with the table
/// generated from appendix C.
/// </summary>
internal static class RequirementIndex
{
    /// <summary>Returns <paramref name="id"/> when it names a catalogued requirement.</summary>
    /// <param name="id">The requirement ID.</param>
    internal static string Require(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("The requirement ID must not be empty.", nameof(id)) : id;
    }
}
