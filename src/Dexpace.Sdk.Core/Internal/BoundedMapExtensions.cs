// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Internal;

/// <summary>Helpers over <see cref="BoundedMap{TKey, TValue}"/>.</summary>
internal static class BoundedMapExtensions
{
    /// <summary>
    /// Returns the value for <paramref name="key"/>, creating and adding one when absent. Under a race exactly one value
    /// wins; a value evicted while a caller still holds it keeps working for that caller (the cap's documented price,
    /// XCUT-14).
    /// </summary>
    internal static TValue GetOrAdd<TKey, TValue>(this BoundedMap<TKey, TValue> map, TKey key, Func<TValue> factory)
        where TKey : notnull
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(factory);
        if (map.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = factory();
        if (map.TryAdd(key, created))
        {
            return created;
        }

        return map.TryGetValue(key, out existing) ? existing : created;
    }
}
