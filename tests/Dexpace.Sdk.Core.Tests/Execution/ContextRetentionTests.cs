// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/context/store.test.ts; Ruby cases (R2 CTX-9, R4 measurement, CTX-19) from ruby-sdk@90075b1's 4a design.
// Ruby's gem tests are not in the local clone.

using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Execution;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Execution;

/// <summary>CTX-11, CTX-19: registered contexts are held strongly; a burst never exceeds the cap once quiescent.</summary>
[Trait("Category", "Unit")]
public sealed class ContextRetentionTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CallKey[] RegisterExchanges(ContextStore store, int count)
    {
        var keys = new CallKey[count];
        for (var i = 0; i < count; i++)
        {
            var request = ExecutionTestSupport.NewRequest();
            var response = ExecutionTestSupport.NewResponse(request);
            var exchange = ExecutionTestSupport.NewDispatch(store).PromoteToRequest(request).PromoteToExchange(response);
            keys[i] = exchange.Key;
        }

        return keys;
    }

    [Fact]
    public void Registered_contexts_survive_a_full_collection_with_no_other_reference()
    {
        var store = new ContextStore(2_000);
        var keys = RegisterExchanges(store, 1_000);

        for (var round = 0; round < 3; round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.All(keys, key => Assert.True(store.TryGet(key, out _)));
    }

    [Fact]
    public async Task A_burst_above_the_capacity_never_exceeds_it_and_never_throws()
    {
        const int Threads = 16;
        const int Capacity = 100;
        var store = new ContextStore(Capacity);
        var request = ExecutionTestSupport.NewRequest();
        var maxObserved = 0;
        using var barrier = new Barrier(Threads);
        var workers = Enumerable.Range(0, Threads).Select(_ => Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait();
                for (var i = 0; i < 500; i++)
                {
                    ExecutionTestSupport.NewDispatch(store).PromoteToRequest(request);
                    var observed = store.Count;
                    int seen;
                    while (observed > (seen = Volatile.Read(ref maxObserved)) && Interlocked.CompareExchange(ref maxObserved, observed, seen) != seen)
                    {
                    }
                }
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)).ToArray();

        // An exception on any worker surfaces here as an AggregateException and fails the test.
        await Task.WhenAll(workers);

        // Ruby R4: the transient maximum overshoots the capacity by at most one entry per racing thread.
        Assert.True(maxObserved >= 1);
        Assert.True(maxObserved <= Capacity + Threads, $"maximum observed {maxObserved}");
        Assert.True(store.Count <= Capacity);
        Assert.Equal(store.EntryCount, store.Count);
    }
}
