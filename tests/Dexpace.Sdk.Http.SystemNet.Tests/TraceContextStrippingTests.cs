// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// Design §8.1, phase 5c P5c-11: the rule that lets the runtime own <c>traceparent</c> on the wire. The "propagation off"
/// clause is tested on the decision, not in-process, because the runtime reads its switch once into a static (plan R4).
/// </summary>
[Trait("Category", "Unit")]
public sealed class TraceContextStrippingTests
{
    private const string Id = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

    [Fact]
    public void A_traceparent_equal_to_the_current_activity_id_is_stripped_when_the_runtime_propagates()
    {
        Assert.True(TraceContextStripping.ShouldStripTraceparent(Id, Id, runtimePropagates: true));
    }

    [Fact]
    public void A_traceparent_is_kept_when_the_runtime_does_not_propagate()
    {
        Assert.False(TraceContextStripping.ShouldStripTraceparent(Id, Id, runtimePropagates: false));
    }

    [Theory]
    [InlineData("00-ffffffffffffffffffffffffffffffff-ffffffffffffffff-01")]
    [InlineData("")]
    public void A_caller_traceparent_that_is_not_the_current_id_is_kept(string value)
    {
        Assert.False(TraceContextStripping.ShouldStripTraceparent(value, Id, runtimePropagates: true));
    }

    [Fact]
    public void A_null_current_activity_never_strips()
    {
        Assert.False(TraceContextStripping.ShouldStripTraceparent(Id, currentId: null, runtimePropagates: true));
        Assert.False(TraceContextStripping.ShouldStripTracestate("a=b", currentTraceState: null, traceparentStripped: true));
    }

    [Fact]
    public void A_tracestate_is_stripped_only_when_it_equals_the_current_trace_state_and_the_traceparent_was_stripped()
    {
        Assert.True(TraceContextStripping.ShouldStripTracestate("a=b", "a=b", traceparentStripped: true));
        Assert.False(TraceContextStripping.ShouldStripTracestate("a=b", "a=b", traceparentStripped: false));
        Assert.False(TraceContextStripping.ShouldStripTracestate("a=b", "c=d", traceparentStripped: true));
    }

    [Fact]
    public void The_header_name_comparison_is_case_insensitive_the_value_comparison_is_ordinal()
    {
        Assert.True(TraceContextStripping.IsTraceparent("TraceParent"));
        Assert.True(TraceContextStripping.IsTracestate("TRACESTATE"));
        Assert.False(TraceContextStripping.IsTraceparent("tracestate"));
        Assert.False(TraceContextStripping.ShouldStripTraceparent(Id.ToUpperInvariant(), Id, runtimePropagates: true));
    }

    [Theory]
    [InlineData(false, null, false)]
    [InlineData(false, "1", false)]
    [InlineData(true, "0", true)]
    [InlineData(null, "0", false)]
    [InlineData(null, "false", false)]
    [InlineData(null, "FALSE", false)]
    [InlineData(null, "1", true)]
    [InlineData(null, "true", true)]
    [InlineData(null, null, true)]
    [InlineData(null, "nonsense", true)]
    public void The_switch_resolver_prefers_the_AppContext_switch_then_the_environment_then_true(bool? appContext, string? environment, bool expected)
    {
        Assert.Equal(expected, TraceContextStripping.ResolvePropagation(appContext, environment));
    }

    [Fact]
    public void The_runtime_value_is_read_once()
    {
        // A static read once: the property returns the cached value on every call.
        Assert.Equal(TraceContextStripping.RuntimeSwitch, TraceContextStripping.RuntimeSwitch);
        Assert.True(TraceContextStripping.RuntimeSwitch);
    }
}
