// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/context/store.test.ts; Ruby cases (R2 CTX-9, R4 measurement, CTX-19) from ruby-sdk@90075b1's 4a design.
// Ruby's gem tests are not in the local clone.

using Dexpace.Sdk.Core.Execution;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Execution;

/// <summary>CTX-3, CTX-7, CTX-8, CTX-9, CTX-18: the context store (isolated instances only).</summary>
[Trait("Category", "Unit")]
public sealed class ContextStoreTests
{
    [Fact]
    public void Set_overwrites_and_never_throws()
    {
        var store = new ContextStore(10);
        var key = CallKey.Next();
        var first = ExecutionTestSupport.NewDispatch(store, key);
        var second = ExecutionTestSupport.NewDispatch(store, key);

        store.Set(first);
        store.Set(second);

        Assert.True(store.TryGet(key, out var found));
        Assert.Same(second, found);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Add_on_an_occupied_key_throws_naming_the_key()
    {
        var store = new ContextStore(10);
        var key = CallKey.Next();
        store.Add(ExecutionTestSupport.NewDispatch(store, key));

        var ex = Assert.Throws<ArgumentException>(() => store.Add(ExecutionTestSupport.NewDispatch(store, key)));

        Assert.Equal("context", ex.ParamName);
        Assert.Contains(key.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rejected_Add_leaves_the_incumbent()
    {
        var store = new ContextStore(10);
        var key = CallKey.Next();
        var incumbent = ExecutionTestSupport.NewDispatch(store, key);
        store.Add(incumbent);

        Assert.Throws<ArgumentException>(() => store.Add(ExecutionTestSupport.NewDispatch(store, key)));

        Assert.True(store.TryGet(key, out var found));
        Assert.Same(incumbent, found);
    }

    [Fact]
    public void Concurrent_Add_of_one_key_admits_exactly_one_winner()
    {
        const int Threads = 32;
        var store = new ContextStore(10);
        var key = CallKey.Next();
        var wins = 0;
        var losses = 0;
        using var barrier = new Barrier(Threads);
        var threads = Enumerable.Range(0, Threads).Select(_ => new Thread(() =>
        {
            var context = ExecutionTestSupport.NewDispatch(store, key);
            barrier.SignalAndWait();
            try
            {
                store.Add(context);
                Interlocked.Increment(ref wins);
            }
            catch (ArgumentException ex) when (ex.Message.Contains(key.ToString(), StringComparison.Ordinal))
            {
                Interlocked.Increment(ref losses);
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.Equal(1, wins);
        Assert.Equal(Threads - 1, losses);
    }

    [Fact]
    public void TryGet_of_an_unknown_key_returns_false_and_null()
    {
        var store = new ContextStore(10);

        Assert.False(store.TryGet(CallKey.Next(), out var found));
        Assert.Null(found);
    }

    [Fact]
    public void A_value_equal_stale_context_does_not_evict_the_live_one()
    {
        var store = new ContextStore(10);
        var live = ExecutionTestSupport.NewDispatch(store, CallKey.Next());
        var clone = live with { };
        Assert.Equal(live, clone);
        Assert.NotSame(live, clone);
        store.Set(live);

        Assert.False(store.Release(clone));
        Assert.True(store.TryGet(live.Key, out var found));
        Assert.Same(live, found);
        Assert.True(store.Release(live));
        Assert.False(store.TryGet(live.Key, out _));
    }

    [Fact]
    public void Release_of_an_unknown_context_is_a_no_op_returning_false()
    {
        var store = new ContextStore(10);

        Assert.False(store.Release(ExecutionTestSupport.NewDispatch(store)));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Release_of_a_replaced_context_is_a_no_op()
    {
        var store = new ContextStore(10);
        var key = CallKey.Next();
        var first = ExecutionTestSupport.NewDispatch(store, key);
        var second = ExecutionTestSupport.NewDispatch(store, key);
        store.Set(first);
        store.Set(second);

        Assert.False(store.Release(first));
        Assert.True(store.TryGet(key, out var found));
        Assert.Same(second, found);
    }

    [Fact]
    public void The_default_capacity_is_ten_thousand() => Assert.Equal(10_000, ContextStore.DefaultCapacity);

    [Fact]
    public void Shared_is_one_instance_with_the_default_capacity() => Assert.Same(ContextStore.Shared, ContextStore.Shared);

    [Fact]
    public void Distinct_keys_register_overwrite_and_release_concurrently_without_loss()
    {
        const int Threads = 16;
        var store = new ContextStore(100_000);
        using var barrier = new Barrier(Threads);
        var threads = Enumerable.Range(0, Threads).Select(_ => new Thread(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < 200; i++)
            {
                var key = CallKey.Next();
                var first = ExecutionTestSupport.NewDispatch(store, key);
                var second = ExecutionTestSupport.NewDispatch(store, key);
                store.Set(first);
                store.Set(second);
                store.Release(first);
                store.Release(second);
            }
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        Assert.Equal(0, store.Count);
        Assert.Equal(0, store.EntryCount);
    }
}
