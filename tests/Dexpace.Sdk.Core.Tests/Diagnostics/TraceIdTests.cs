// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-26, OBS-27: the W3C and no-op trace-id flavours (design §10 entry 24, P5c-14).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class TraceIdTests
{
    [Fact]
    public void Generated_trace_ids_are_32_lowercase_hex_and_non_zero()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var request = Request.Get("https://api.example.com/");

        for (var i = 0; i < 10_000; i++)
        {
            var span = OperationTelemetry.Start(request, new DexpaceClientOptions());
            Assert.NotNull(span);
            var hex = span.TraceId.ToHexString();
            Assert.Matches("^[0-9a-f]{32}$", hex);
            Assert.NotEqual(new string('0', 32), hex);
            OperationTelemetry.Stop(span, settled: true);
        }
    }

    [Fact]
    public void Generated_span_ids_are_16_lowercase_hex_and_non_zero()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var request = Request.Get("https://api.example.com/");

        for (var i = 0; i < 10_000; i++)
        {
            var span = OperationTelemetry.Start(request, new DexpaceClientOptions());
            Assert.NotNull(span);
            var hex = span.SpanId.ToHexString();
            Assert.Matches("^[0-9a-f]{16}$", hex);
            Assert.NotEqual(new string('0', 16), hex);
            OperationTelemetry.Stop(span, settled: true);
        }
    }

    [Fact]
    public void The_W3C_flavour_is_reported_and_the_no_op_flavour_is_the_zero_id()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), new DexpaceClientOptions());

        var traced = InstrumentationContext.FromActivity(span);

        Assert.Equal(ActivityIdFormat.W3C, traced.TraceIdFormat);
        Assert.Equal(default, InstrumentationContext.None.TraceId);
        Assert.Equal(default, InstrumentationContext.None.SpanId);
        Assert.Equal(ActivityIdFormat.Unknown, InstrumentationContext.None.TraceIdFormat);
        OperationTelemetry.Stop(span, settled: true);
    }

    [Fact]
    public async Task The_sdk_does_not_install_a_trace_id_generator()
    {
        var before = Activity.TraceIdGenerator;
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport());

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/"), TestContext.Current.CancellationToken);

        Assert.Same(before, Activity.TraceIdGenerator);
    }

    [Fact]
    public async Task A_hierarchical_parent_yields_a_bundle_that_reports_its_real_format()
    {
        using var recorder = new ActivityRecorder("Dexpace.Sdk");
        using var parent = new Activity("legacy");
        parent.SetIdFormat(ActivityIdFormat.Hierarchical);
        parent.Start();
        InstrumentationContext? bundle = null;
        var pipeline = new PipelineBuilder()
            .Add(new DelegatePolicy(PipelineStage.PerCall, (request, context, next) =>
            {
                bundle = context.Instrumentation;
                return next.RunAsync(request, context);
            }))
            .Build(new RecordingTransport());

        try
        {
            using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/"), TestContext.Current.CancellationToken);
        }
        finally
        {
            parent.Stop();
        }

        var operation = recorder.Started.First(a => a.Kind == ActivityKind.Internal && a.Parent == parent);
        Assert.NotNull(bundle);
        Assert.Equal(operation.IdFormat, bundle.TraceIdFormat);
        Assert.Equal(operation.IdFormat == ActivityIdFormat.W3C, bundle.IsValid);
    }
}
