// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Concurrent;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>The bounded-map probes of the XCUT-14 controls: the conforming one and the broken ones (plan 1.7).</summary>
internal static class BoundedProbes
{
    /// <summary>A conforming map: a lock and a loop that drains to the cap after every insert.</summary>
    internal static BoundedMapProbe Conforming(int capacity)
    {
        var map = new Dictionary<string, byte>();
        var gate = new object();
        return new BoundedMapProbe(
            capacity,
            key =>
            {
                lock (gate)
                {
                    map[key] = 1;
                    while (map.Count > capacity)
                    {
                        map.Remove(map.Keys.First());
                    }
                }
            },
            () =>
            {
                lock (gate)
                {
                    return map.Count;
                }
            });
    }

    /// <summary>A map that never evicts.</summary>
    internal static BoundedMapProbe Unbounded(int capacity)
    {
        var map = new ConcurrentDictionary<string, byte>();
        return new BoundedMapProbe(capacity, key => map[key] = 1, () => map.Count);
    }

    /// <summary>A map whose single pre-insert check uses "above" for "at", so it settles one entry over the cap.</summary>
    internal static BoundedMapProbe OffByOne(int capacity)
    {
        var map = new ConcurrentDictionary<string, byte>();
        return new BoundedMapProbe(
            capacity,
            key =>
            {
                if (map.Count > capacity)
                {
                    EvictOne(map);
                }

                map[key] = 1;
            },
            () => map.Count);
    }

    /// <summary>
    /// A map that checks the size once before it inserts and evicts one entry if it was full: right sequentially, wrong the moment
    /// several threads straddle the cap. The first insert of each burst thread rendezvous at the check, so the straddle is certain
    /// (as many threads as the suite runs: <see cref="InvariantOptions.Racers"/>), and the map then settles above its cap for good.
    /// </summary>
    internal static BoundedMapProbe PreInsertCheckThenEvict(int capacity)
    {
        var map = new ConcurrentDictionary<string, byte>();
#pragma warning disable CA2000 // Lives as long as the probe; the test process ends it.
        var rendezvous = new Barrier(InvariantOptions.Racers);
        var arrived = new ThreadLocal<bool>();
#pragma warning restore CA2000
        return new BoundedMapProbe(
            capacity,
            key =>
            {
                var seen = map.Count;
                if (seen == capacity - 1 && !arrived.Value)
                {
                    arrived.Value = true;
                    rendezvous.SignalAndWait(TimeSpan.FromSeconds(20));
                }

                if (seen >= capacity)
                {
                    EvictOne(map);
                }

                map[key] = 1;
            },
            () => map.Count);
    }

    private static void EvictOne(ConcurrentDictionary<string, byte> map)
    {
        foreach (var key in map.Keys)
        {
            if (map.TryRemove(key, out _))
            {
                return;
            }
        }
    }
}
