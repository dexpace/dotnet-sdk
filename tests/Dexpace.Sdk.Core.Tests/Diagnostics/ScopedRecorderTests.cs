// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>The scoped recorders keep one test's telemetry out of another's (P5c-15).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class ScopedRecorderTests
{
    private static readonly ActivitySource s_source = new("Dexpace.Sdk.Tests.ScopedRecorder");

    [Fact]
    public void A_scoped_recorder_keeps_only_activities_in_its_own_trace()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk.Tests.ScopedRecorder");

        using var child = s_source.StartActivity("child");
        var elsewhere = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        using var sibling = s_source.StartActivity("sibling", ActivityKind.Internal, elsewhere);

        var started = Assert.Single(recorder.Started);
        Assert.Same(child, started);
        Assert.NotNull(sibling);
        Assert.NotEqual(recorder.Root!.TraceId, sibling.TraceId);
    }

    [Fact]
    public void The_scoped_root_is_the_ambient_activity_in_the_creating_context()
    {
        var before = Activity.Current;
        var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");

        Assert.NotNull(recorder.Root);
        Assert.Same(recorder.Root, Activity.Current);

        recorder.Dispose();

        Assert.Same(before, Activity.Current);
    }

    [Fact]
    public void A_scoped_recorder_excludes_its_own_root_from_Started_and_Stopped()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk.Tests.ScopedRecorder");
        s_source.StartActivity("child")?.Dispose();

        recorder.Root!.Stop();

        Assert.DoesNotContain(recorder.Started, a => ReferenceEquals(a, recorder.Root));
        Assert.DoesNotContain(recorder.Stopped, a => ReferenceEquals(a, recorder.Root));
        Assert.Single(recorder.Stopped);
    }

    [Fact]
    public void A_host_from_TestHosts_Unique_is_a_valid_unique_host()
    {
        var first = TestHosts.Unique();
        var second = TestHosts.Unique();

        Assert.NotEqual(first, second);
        Assert.Equal(first.ToLowerInvariant(), first);
        Assert.EndsWith(".example.test", first, StringComparison.Ordinal);
        Assert.Equal(first, new Uri($"https://{first}/").Host);
    }

    [Fact]
    public void A_scoped_metric_recorder_keeps_only_measurements_for_its_server_address()
    {
        using var meter = new Meter("Dexpace.Sdk.Tests.ScopedMetrics");
        var counter = meter.CreateCounter<long>("tests.scoped");
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk.Tests.ScopedMetrics", host);

        counter.Add(1, new KeyValuePair<string, object?>("server.address", host));
        counter.Add(1, new KeyValuePair<string, object?>("server.address", "other.example.test"));

        var kept = Assert.Single(recorder.Measurements);
        Assert.Equal(host, kept.Tag("server.address"));
    }
}
