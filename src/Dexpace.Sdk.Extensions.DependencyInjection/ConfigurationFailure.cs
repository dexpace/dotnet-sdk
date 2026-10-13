// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// One configuration failure: the configuration path of the key and the rule it broke (P9-9). It never carries the configured value, a
/// URL or a credential (XCUT-18, XCUT-19); an accessor's numeric range message may echo the rejected number (P9-28).
/// </summary>
/// <param name="Path">The key's full configuration path (the section's path plus the key), or empty for a failure about the section.</param>
/// <param name="Rule">The rule the key broke.</param>
internal readonly record struct ConfigurationFailure(string Path, string Rule)
{
    /// <summary>Formats the failure as <c>"&lt;path&gt;: &lt;rule&gt;"</c>.</summary>
    /// <returns>The failure's text.</returns>
    public override string ToString() => Path.Length == 0 ? Rule : Path + ": " + Rule;
}
