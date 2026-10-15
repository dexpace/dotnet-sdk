// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The portable face of a bounded map for <c>XCUT-14</c>: what a driver hands the invariant suite instead of naming an internal
/// type. The suite inserts distinct keys through <see cref="Insert"/> and reads the map's size through <see cref="Count"/>.
/// </summary>
public sealed class BoundedMapProbe
{
    /// <summary>Creates a probe.</summary>
    /// <param name="capacity">The hard cap the map was built with; positive.</param>
    /// <param name="insert">Inserts a key (a new entry for a key the map has not seen); must be safe to call from many threads.</param>
    /// <param name="count">The number of entries the map holds now; must be safe to call from many threads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="insert"/> or <paramref name="count"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public BoundedMapProbe(int capacity, Action<string> insert, Func<int> count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentNullException.ThrowIfNull(insert);
        ArgumentNullException.ThrowIfNull(count);
        Capacity = capacity;
        Insert = insert;
        Count = count;
    }

    /// <summary>The hard cap the map was built with.</summary>
    public int Capacity { get; }

    /// <summary>Inserts a key.</summary>
    public Action<string> Insert { get; }

    /// <summary>The number of entries the map holds now.</summary>
    public Func<int> Count { get; }
}
