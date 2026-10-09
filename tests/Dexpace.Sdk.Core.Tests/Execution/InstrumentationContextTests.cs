// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/context/instrumentation.test.ts; Ruby cases (R2 CTX-9, R4 measurement, CTX-19) from ruby-sdk@90075b1's 4a design.
// Ruby's gem tests are not in the local clone.

using System.Diagnostics;
using System.Reflection;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Execution;

/// <summary>CTX-14, CTX-15, CTX-20: the correlation bundle.</summary>
[Trait("Category", "Unit")]
[Collection("Instrumentation")]
public sealed class InstrumentationContextTests
{
    private static readonly string[] s_blankNames = [null!, string.Empty, "  "];

    [Fact]
    public void FromActivity_exposes_the_activitys_identifiers_flags_state_and_span()
    {
        using var recorder = new ActivityRecorder("Dexpace.Sdk");
        using var activity = DexpaceDiagnostics.ActivitySource.StartActivity("op");
        Assert.NotNull(activity);

        var bundle = InstrumentationContext.FromActivity(activity);

        Assert.Equal(activity.TraceId, bundle.TraceId);
        Assert.Equal(activity.SpanId, bundle.SpanId);
        Assert.Equal(activity.ActivityTraceFlags, bundle.TraceFlags);
        Assert.Equal(activity.Context, bundle.ActivityContext);
        Assert.True(bundle.IsValid);
        Assert.False(bundle.IsRemote);
        Assert.Same(activity, bundle.ActiveSpan);
        Assert.Equal(ActivityIdFormat.W3C, bundle.TraceIdFormat);
    }

    [Fact]
    public void FromActivity_with_no_trace_state_reports_the_empty_string_not_null()
    {
        using var recorder = new ActivityRecorder("Dexpace.Sdk");
        using var activity = DexpaceDiagnostics.ActivitySource.StartActivity("op");
        Assert.NotNull(activity);

        Assert.Equal(string.Empty, InstrumentationContext.FromActivity(activity).TraceState);
    }

    [Fact]
    public void FromActivity_keeps_a_set_trace_state_verbatim()
    {
        using var recorder = new ActivityRecorder("Dexpace.Sdk");
        using var activity = DexpaceDiagnostics.ActivitySource.StartActivity("op");
        Assert.NotNull(activity);
        activity.TraceStateString = "a=b";

        Assert.Equal("a=b", InstrumentationContext.FromActivity(activity).TraceState);
    }

    [Fact]
    public void FromContext_of_a_remote_parent_is_remote_and_has_no_active_span()
    {
        var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, "k=v", isRemote: true);

        var bundle = InstrumentationContext.FromContext(context);

        Assert.True(bundle.IsRemote);
        Assert.Null(bundle.ActiveSpan);
        Assert.Equal("k=v", bundle.TraceState);
        Assert.True(bundle.IsValid);
    }

    [Fact]
    public void FromContext_of_a_started_context_reports_its_trace_flags()
    {
        var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

        var bundle = InstrumentationContext.FromContext(context);

        Assert.Equal(ActivityTraceFlags.Recorded, bundle.TraceFlags);
        Assert.Equal(string.Empty, bundle.TraceState);
        Assert.Equal(ActivityIdFormat.W3C, bundle.TraceIdFormat);
    }

    [Fact]
    public void The_tracer_factory_starts_a_child_of_the_bundle_under_a_listener()
    {
        using var recorder = new ActivityRecorder("Dexpace.Sdk");
        var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var bundle = InstrumentationContext.FromContext(parentContext);

        using var unrelated = new Activity("unrelated");
        unrelated.Start();
        using var child = bundle.StartActivity("child", ActivityKind.Client);

        Assert.NotNull(child);
        Assert.Equal(bundle.TraceId, child.TraceId);
        Assert.Equal(bundle.SpanId, child.ParentSpanId);
        Assert.Equal(ActivityKind.Client, child.Kind);
    }

    [Fact]
    public void A_legacy_hierarchical_activity_reports_the_hierarchical_format_and_is_not_valid()
    {
        var original = Activity.DefaultIdFormat;
        var originalForce = Activity.ForceDefaultIdFormat;
        try
        {
            Activity.DefaultIdFormat = ActivityIdFormat.Hierarchical;
            Activity.ForceDefaultIdFormat = true;
            using var activity = new Activity("legacy");
            activity.Start();

            var bundle = InstrumentationContext.FromActivity(activity);

            Assert.Equal(ActivityIdFormat.Hierarchical, bundle.TraceIdFormat);
            Assert.False(bundle.IsValid);
        }
        finally
        {
            Activity.DefaultIdFormat = original;
            Activity.ForceDefaultIdFormat = originalForce;
        }
    }

    [Fact]
    public void None_reserves_the_invalid_sentinels()
    {
        var none = InstrumentationContext.None;

        Assert.Equal(default, none.TraceId);
        Assert.Equal(default, none.SpanId);
        Assert.Equal(ActivityTraceFlags.None, none.TraceFlags);
        Assert.Equal(string.Empty, none.TraceState);
        Assert.False(none.IsValid);
        Assert.False(none.IsRemote);
        Assert.Null(none.ActiveSpan);
        Assert.Equal(ActivityIdFormat.Unknown, none.TraceIdFormat);
    }

    [Fact]
    public void None_never_starts_an_activity_even_with_a_listener()
    {
        // Scoped: the listener is process-wide, and a test in another collection that runs the default pipeline starts its
        // own "GET" span on the same source while this one listens (P5c-15). An activity None started would join the root's trace.
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");

        Assert.Null(InstrumentationContext.None.StartActivity("op"));
        Assert.Empty(recorder.Started);
    }

    [Fact]
    public void FromActivity_null_and_FromContext_default_return_None_itself()
    {
        Assert.Same(InstrumentationContext.None, InstrumentationContext.FromActivity(null));
        Assert.Same(InstrumentationContext.None, InstrumentationContext.FromContext(default));
    }

    [Fact]
    public void A_default_ActivityContext_keeps_the_key_call_unique_under_None()
    {
        var a = CallKey.Next(InstrumentationContext.None.ActivityContext);
        var b = CallKey.Next(InstrumentationContext.None.ActivityContext);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void StartActivity_with_a_blank_name_throws_on_every_bundle_including_None()
    {
        var real = InstrumentationContext.FromContext(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));

        foreach (var name in s_blankNames)
        {
            Assert.ThrowsAny<ArgumentException>(() => InstrumentationContext.None.StartActivity(name));
            Assert.ThrowsAny<ArgumentException>(() => real.StartActivity(name));
        }
    }

    [Fact]
    public void With_no_listener_the_factory_returns_null()
    {
        Assert.False(DexpaceDiagnostics.ActivitySource.HasListeners(), "premise: no listener is attached to the SDK source");
        var bundle = InstrumentationContext.FromContext(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));

        Assert.Null(bundle.StartActivity("op"));
    }

    [Fact]
    public void The_tracer_factory_is_safe_under_concurrent_invocation()
    {
        const int Threads = 16;
        const int PerThread = 50;
        using var recorder = new ActivityRecorder("Dexpace.Sdk");
        var traceId = ActivityTraceId.CreateRandom();
        var bundle = InstrumentationContext.FromContext(new ActivityContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));
        var started = new Activity?[Threads][];
        using var barrier = new Barrier(Threads);
        var threads = Enumerable.Range(0, Threads).Select(t => new Thread(() =>
        {
            var local = new Activity?[PerThread];
            barrier.SignalAndWait();
            for (var i = 0; i < PerThread; i++)
            {
                local[i] = bundle.StartActivity("op");
                local[i]?.Stop();
            }

            started[t] = local;
        })).ToList();
        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());

        var all = started.SelectMany(a => a).ToList();
        Assert.All(all, Assert.NotNull);
        Assert.Equal(Threads * PerThread, all.Select(a => a!.SpanId).Distinct().Count());

        // The listener is process-wide: count only this test's trace (P5c-15).
        Assert.Equal(Threads * PerThread, recorder.Started.Count(a => a.TraceId == traceId));
        Assert.Equal(Threads * PerThread, recorder.Stopped.Count(a => a.TraceId == traceId));
    }

    [Fact]
    public void The_class_is_sealed_and_has_no_public_constructor()
    {
        Assert.True(typeof(InstrumentationContext).IsSealed);
        Assert.Empty(typeof(InstrumentationContext).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }
}
