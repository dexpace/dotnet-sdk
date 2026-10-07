// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// A bounded, lock-free-on-the-hot-path concurrent map: the SDK's one bounded-map implementation (CTX-8, CTX-9,
/// CTX-11, CTX-12, CTX-13, CTX-19; XCUT-14; design §5.4, P4a-10, P4a-12).
/// </summary>
/// <remarks>
/// <para>
/// Every stored value is wrapped in a fresh <see cref="Slot"/> that declares no equality, so the slot-reference
/// <c>TryUpdate</c> and <c>TryRemove(KeyValuePair)</c> are reference-identity compare-and-swap operations. This is what
/// makes <see cref="TryRemoveIfSame"/> safe against a value-equal stale sibling (CTX-9): a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> otherwise compares values with the default equality comparer.
/// </para>
/// <para>
/// The count is tracked in an <see cref="Interlocked"/> field beside the dictionary, never read from
/// <see cref="ConcurrentDictionary{TKey,TValue}.Count"/> (which takes every lock). After every successful add the map
/// drains until the count is at or under the capacity (CTX-11, CTX-12). The loop is load-bearing: there is no global
/// lock, so several inserters can each push the count over before any drains. Victim selection is arbitrary, the
/// enumerator's order, and the just-inserted entry may itself be evicted (CTX-13).
/// </para>
/// <para>
/// Values are held strongly: no weak table and no weak reference is used, and the capacity, not the collector, is the
/// backstop (CTX-19). Between an insert and its drain the count may transiently exceed the capacity.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type; compared by reference where identity matters.</typeparam>
internal sealed class BoundedMap<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    private readonly ConcurrentDictionary<TKey, Slot> _map;
    private int _count;

    /// <summary>Creates a map that holds at most <paramref name="capacity"/> entries once quiescent.</summary>
    /// <param name="capacity">The bound; at least 1.</param>
    /// <param name="comparer">An optional key comparer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is below 1.</exception>
    internal BoundedMap(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _map = new ConcurrentDictionary<TKey, Slot>(comparer);
    }

    /// <summary>The bound.</summary>
    internal int Capacity { get; }

    /// <summary>The tracked entry count (an interlocked counter, not the dictionary's count).</summary>
    internal int Count => Volatile.Read(ref _count);

    /// <summary>
    /// The dictionary's own entry count. Diagnostic only: it takes every lock, and production code never calls it.
    /// </summary>
    internal int EntryCount => _map.Count;

    /// <summary>Drains <paramref name="map"/> until <paramref name="count"/> is at or under <paramref name="capacity"/> (CTX-12).</summary>
    /// <param name="map">The slot dictionary.</param>
    /// <param name="count">The tracked count; decremented once per successful removal.</param>
    /// <param name="capacity">The bound.</param>
    internal static void Drain(ConcurrentDictionary<TKey, Slot> map, ref int count, int capacity)
    {
        while (Volatile.Read(ref count) > capacity)
        {
            var removedAny = false;

            // The live enumerator takes no lock; Keys would take every lock and copy.
            foreach (var pair in map)
            {
                if (Volatile.Read(ref count) <= capacity)
                {
                    return;
                }

                if (map.TryRemove(pair.Key, out _))
                {
                    Interlocked.Decrement(ref count);
                    removedAny = true;
                }
            }

            if (!removedAny && map.IsEmpty)
            {
                return;
            }
        }
    }

    /// <summary>Installs or replaces the value under <paramref name="key"/> (CTX-8).</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    internal void Set(TKey key, TValue value)
    {
        var slot = new Slot(value);
        while (true)
        {
            if (_map.TryAdd(key, slot))
            {
                Interlocked.Increment(ref _count);
                Drain(_map, ref _count, Capacity);
                return;
            }

            if (_map.TryGetValue(key, out var current) && _map.TryUpdate(key, slot, current))
            {
                return;
            }
        }
    }

    /// <summary>Installs <paramref name="value"/> only if <paramref name="key"/> is absent (CTX-8).</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when installed.</returns>
    internal bool TryAdd(TKey key, TValue value)
    {
        if (!_map.TryAdd(key, new Slot(value)))
        {
            return false;
        }

        Interlocked.Increment(ref _count);
        Drain(_map, ref _count, Capacity);
        return true;
    }

    /// <summary>Looks up <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when present.</returns>
    internal bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue? value)
    {
        if (_map.TryGetValue(key, out var slot))
        {
            value = slot.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Removes <paramref name="key"/> whatever its value.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when an entry was removed.</returns>
    internal bool TryRemove(TKey key)
    {
        if (!_map.TryRemove(key, out _))
        {
            return false;
        }

        Interlocked.Decrement(ref _count);
        return true;
    }

    /// <summary>
    /// Removes <paramref name="key"/> only when its current value is the same reference as <paramref name="value"/> (CTX-9).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value that must be the occupant.</param>
    /// <returns><see langword="true"/> when the entry was removed.</returns>
    internal bool TryRemoveIfSame(TKey key, TValue value)
    {
        if (!_map.TryGetValue(key, out var slot) || !ReferenceEquals(slot.Value, value))
        {
            return false;
        }

        // The one sanctioned use of the value-comparing overload (design §5.4, P13, P4a-12): Slot declares no
        // Equals, so EqualityComparer<Slot>.Default is reference identity and this is a reference compare-and-remove.
#pragma warning disable RS0030
        var removed = _map.TryRemove(new KeyValuePair<TKey, Slot>(key, slot));
#pragma warning restore RS0030
        if (removed)
        {
            Interlocked.Decrement(ref _count);
        }

        return removed;
    }

    /// <summary>
    /// The per-insert wrapper. It deliberately declares no <c>Equals</c> or <c>GetHashCode</c> (design §5.4, facts 1
    /// and 2): that makes slot comparison reference identity. Do not turn it into a record.
    /// </summary>
    internal sealed class Slot
    {
        internal Slot(TValue value) => Value = value;

        internal TValue Value { get; }
    }
}
