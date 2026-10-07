// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/context/context.test.ts; Ruby cases (R2 CTX-9, R4 measurement, CTX-19) from ruby-sdk@90075b1's 4a design.
// Ruby's gem tests are not in the local clone.

using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using Dexpace.Sdk.Core.Execution;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Execution;

/// <summary>CTX-4, CTX-5, CTX-6, CTX-15: the call key.</summary>
[Trait("Category", "Unit")]
public sealed class CallKeyTests
{
    private static ActivityContext SampleContext() =>
        new(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

    [Fact]
    public void Two_keys_minted_from_identical_trace_and_span_differ()
    {
        var context = SampleContext();
        var a = CallKey.Next(context);
        var b = CallKey.Next(context);

        Assert.NotEqual(a, b);
        Assert.Equal(a.TraceId, b.TraceId);
        Assert.Equal(a.SpanId, b.SpanId);
        Assert.True(b.Sequence >= a.Sequence + 1);
    }

    [Fact]
    public void Keys_minted_under_None_are_distinct()
    {
        Assert.NotEqual(CallKey.Next(), CallKey.Next());
        Assert.NotEqual(CallKey.Next(InstrumentationContext.None.ActivityContext), CallKey.Next(InstrumentationContext.None.ActivityContext));
    }

    [Fact]
    public void A_minted_key_has_a_positive_sequence_and_default_has_zero()
    {
        Assert.True(CallKey.Next().Sequence > 0);
        Assert.Equal(0, default(CallKey).Sequence);
    }

    [Fact]
    public void Keys_minted_concurrently_across_threads_are_pairwise_distinct()
    {
        const int Threads = 16;
        const int PerThread = 10_000;
        var results = new long[Threads][];
        using var barrier = new Barrier(Threads);
        var threads = Enumerable.Range(0, Threads).Select(t => new Thread(() =>
        {
            var local = new long[PerThread];
            barrier.SignalAndWait();
            for (var i = 0; i < PerThread; i++)
            {
                local[i] = CallKey.Next().Sequence;
            }

            results[t] = local;
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        var all = new HashSet<long>(results.SelectMany(r => r));
        Assert.Equal(Threads * PerThread, all.Count);
    }

    [Fact]
    public void ToString_renders_trace_span_and_sequence_with_colons()
    {
        var key = CallKey.Next(SampleContext());
        Assert.Matches(new Regex("^[0-9a-f]{32}:[0-9a-f]{16}:[0-9]+$"), key.ToString());

        var zero = CallKey.Next().ToString();
        Assert.StartsWith(new string('0', 32) + ":" + new string('0', 16) + ":", zero, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_is_a_value_type_with_field_wise_equality()
    {
        var key = CallKey.Next();
        var copy = key;

        Assert.Equal(key, copy);
        Assert.Equal(key.GetHashCode(), copy.GetHashCode());
        Assert.NotEqual(key, CallKey.Next());
    }

    [Fact]
    public void CallKey_declares_no_public_constructor()
    {
        var constructors = typeof(CallKey).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(constructors, c => c.GetParameters().Length > 0);
    }
}
