// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The lookup over <see cref="RequirementCatalog"/>, the table generated from appendix C (P8a-13). Ordinal and
/// case-sensitive: <c>TRANSPORT-24</c> is a requirement, <c>transport-24</c> is an assertion name.
/// </summary>
internal static class RequirementIndex
{
    private static readonly FrozenDictionary<string, RequirementLevel> s_levels =
        RequirementCatalog.Rows.ToFrozenDictionary(row => row.Id, row => row.Level, StringComparer.Ordinal);

    /// <summary>Every catalogued requirement ID, in appendix order.</summary>
    internal static IReadOnlyList<string> All { get; } = [.. RequirementCatalog.Rows.Select(row => row.Id)];

    /// <summary>Looks up the level of <paramref name="id"/>.</summary>
    /// <param name="id">The requirement ID.</param>
    /// <param name="level">The level, when the ID is catalogued.</param>
    /// <returns>Whether <paramref name="id"/> is catalogued.</returns>
    internal static bool TryGetLevel(string id, out RequirementLevel level) => s_levels.TryGetValue(id, out level);

    /// <summary>Whether <paramref name="id"/> is a catalogued requirement.</summary>
    /// <param name="id">The requirement ID.</param>
    internal static bool Contains(string id) => s_levels.ContainsKey(id);

    /// <summary>Returns <paramref name="id"/> when it names a catalogued requirement.</summary>
    /// <param name="id">The requirement ID.</param>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or not in the catalogue; the message echoes at most 64 characters of it.</exception>
    internal static string Require(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Contains(id)
            ? id
            : throw new ArgumentException(
                $"'{Text.Truncate(id)}' is not a requirement in the catalogue generated from appendix C.",
                nameof(id));
    }
}
