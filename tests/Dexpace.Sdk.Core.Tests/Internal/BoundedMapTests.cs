// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/context/store.test.ts (bounded drain, cap below one).
// Ruby's gem tests are not in the local clone.

using System.Collections.Concurrent;
using System.Reflection;
using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

/// <summary>CTX-8, CTX-9, CTX-11, CTX-12, CTX-13, CTX-19: the bounded map and its identity-comparing slot.</summary>
[Trait("Category", "Unit")]
public sealed class BoundedMapTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_capacity_below_one_is_rejected(int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedMap<int, object>(capacity));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void An_insert_burst_past_the_capacity_ends_at_or_under_it()
    {
        var map = new BoundedMap<int, object>(10);
        for (var i = 0; i < 1_000; i++)
        {
            map.Set(i, new object());
            Assert.True(map.Count <= 10);
        }

        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void The_tracked_count_matches_the_dictionary_after_quiescence()
    {
        var map = new BoundedMap<int, object>(1_000);
        using var barrier = new Barrier(8);
        var threads = Enumerable.Range(0, 8).Select(t => new Thread(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 2_000; i++)
            {
                var key = (t * 100) + (i % 100);
                var value = new object();
                map.Set(key, value);
                if (i % 3 == 0)
                {
                    map.TryRemoveIfSame(key, value);
                }

                if (i % 5 == 0)
                {
                    map.TryRemove(key);
                }
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.Equal(map.EntryCount, map.Count);
    }

    [Fact]
    public void One_drain_call_removes_every_excess_entry()
    {
        var dictionary = new ConcurrentDictionary<int, BoundedMap<int, object>.Slot>();
        for (var i = 0; i < 100; i++)
        {
            dictionary[i] = new BoundedMap<int, object>.Slot(new object());
        }

        var count = 100;
        BoundedMap<int, object>.Drain(dictionary, ref count, 10);

        Assert.Equal(10, count);
        Assert.Equal(10, dictionary.Count);
    }

    [Fact]
    public void Concurrent_inserts_from_16_threads_converge_to_the_capacity()
    {
        var map = new BoundedMap<int, object>(64);
        using var barrier = new Barrier(16);
        var threads = Enumerable.Range(0, 16).Select(t => new Thread(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 500; i++)
            {
                map.Set((t * 500) + i, new object());
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.True(map.Count <= 64);
        Assert.Equal(map.EntryCount, map.Count);
    }

    [Fact]
    public void A_capacity_one_map_holds_exactly_one_of_two_inserts()
    {
        var map = new BoundedMap<int, object>(1);
        map.Set(1, new object());
        map.Set(2, new object());

        Assert.Equal(1, map.Count);
        Assert.Equal(1, map.EntryCount);
        Assert.True(map.TryGetValue(1, out _) ^ map.TryGetValue(2, out _));
    }

    [Fact]
    public void Set_installs_then_replaces_without_changing_the_count()
    {
        var map = new BoundedMap<string, object>(10);
        var first = new object();
        var second = new object();
        map.Set("k", first);
        map.Set("k", second);

        Assert.Equal(1, map.Count);
        Assert.True(map.TryGetValue("k", out var found));
        Assert.Same(second, found);
    }

    [Fact]
    public void TryAdd_installs_only_if_absent()
    {
        var map = new BoundedMap<string, object>(10);
        var first = new object();
        Assert.True(map.TryAdd("k", first));
        Assert.False(map.TryAdd("k", new object()));

        Assert.Equal(1, map.Count);
        Assert.True(map.TryGetValue("k", out var found));
        Assert.Same(first, found);
    }

    [Fact]
    public void TryGetValue_of_an_unknown_key_returns_false_and_null()
    {
        var map = new BoundedMap<string, object>(10);
        Assert.False(map.TryGetValue("nope", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryRemove_of_an_unknown_key_returns_false_and_leaves_the_count()
    {
        var map = new BoundedMap<string, object>(10);
        map.Set("k", new object());

        Assert.False(map.TryRemove("other"));
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void TryRemoveIfSame_compares_by_reference_not_value()
    {
        var map = new BoundedMap<string, string>(10);
        var stored = new string('a', 3);
        var equalButDistinct = new string('a', 3);
        Assert.Equal(stored, equalButDistinct);
        Assert.NotSame(stored, equalButDistinct);
        map.Set("k", stored);

        Assert.False(map.TryRemoveIfSame("k", equalButDistinct));
        Assert.Equal(1, map.Count);
        Assert.True(map.TryRemoveIfSame("k", stored));
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void TryRemoveIfSame_after_a_replacement_is_a_no_op_for_the_replaced_value()
    {
        var map = new BoundedMap<string, object>(10);
        var a = new object();
        var b = new object();
        map.Set("k", a);
        map.Set("k", b);

        Assert.False(map.TryRemoveIfSame("k", a));
        Assert.True(map.TryGetValue("k", out var found));
        Assert.Same(b, found);
    }

    [Fact]
    public void The_slot_type_declares_no_equality_override()
    {
        var slot = typeof(BoundedMap<,>.Slot);
        const BindingFlags Flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        Assert.Null(slot.GetMethod(nameof(object.Equals), Flags, [typeof(object)]));
        Assert.Null(slot.GetMethod(nameof(object.GetHashCode), Flags, Type.EmptyTypes));
    }

    [Fact]
    public void A_re_set_of_the_same_value_gets_a_new_slot()
    {
        var map = new BoundedMap<string, object>(10);
        var value = new object();
        map.Set("k", value);
        Assert.True(map.TryRemoveIfSame("k", value));
        map.Set("k", value);

        Assert.True(map.TryRemoveIfSame("k", value));
        Assert.False(map.TryRemoveIfSame("k", value));
        Assert.Equal(0, map.Count);
    }
}
