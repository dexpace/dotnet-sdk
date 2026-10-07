// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>
/// OBS-20, OBS-30, P5c-13: a listener callback is the contract party and is never wrapped, so a throw propagates; a
/// throw after a <see cref="Response"/> exists releases that response first. These tests install throwing listeners,
/// which are process-wide, so they run alone (<c>NoDiagnosticListeners</c>, P5c-15).
/// </summary>
[Collection("NoDiagnosticListeners")]
[Trait("Category", "Unit")]
public sealed class ListenerContractTests
{
    private static Request Get() => Request.Get("https://api.example.com/v1/items");

    private static ActivityListener Throwing(ActivityKind kind, bool onStart)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dexpace.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        void Throw(Activity activity)
        {
            if (activity.Kind == kind)
            {
                throw new InvalidOperationException("listener");
            }
        }

        if (onStart)
        {
            listener.ActivityStarted = Throw;
        }
        else
        {
            listener.ActivityStopped = Throw;
        }

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Theory]
    [InlineData(ActivityKind.Internal)]
    [InlineData(ActivityKind.Client)]
    public async Task A_throwing_ActivityStarted_propagates_out_of_SendAsync_and_Send(ActivityKind throwOn)
    {
        using var listener = Throwing(throwOn, onStart: true);
        var transport = new RecordingTransport();
        var pipeline = TracingFixtures.Pipeline(transport);
        var token = TestContext.Current.CancellationToken;

        var async = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.SendAsync(Get(), token));
        var sync = Assert.Throws<InvalidOperationException>(() => pipeline.Send(Get(), token));

        Assert.Equal("listener", async.Message);
        Assert.Equal("listener", sync.Message);
        Assert.Equal(0, transport.CallCount);
    }

    [Theory]
    [InlineData(ActivityKind.Client, true)]
    [InlineData(ActivityKind.Client, false)]
    [InlineData(ActivityKind.Internal, true)]
    [InlineData(ActivityKind.Internal, false)]
    public async Task A_throwing_ActivityStopped_disposes_the_response_before_propagating(ActivityKind throwOn, bool isAsync)
    {
        using var listener = Throwing(throwOn, onStart: false);
        var log = new List<string>();
        using var body = new TrackingResponseBody(log);
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: body)));
        var token = TestContext.Current.CancellationToken;

        if (isAsync)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.SendAsync(Get(), token));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => pipeline.Send(Get(), token));
        }

        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_throwing_metric_callback_propagates_out_of_SendAsync_and_releases_the_response()
    {
        using var meters = ThrowingMeterListener("http.client.request.duration");
        using var body = new TrackingResponseBody([]);
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: body)));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        Assert.Equal("callback", thrown.Message);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public void A_throwing_measurement_callback_propagates_from_the_instruments()
    {
        using var meters = ThrowingMeterListener("http.client.request.duration");
        using var response = TestResponses.Create(Status.Ok);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => HttpClientMetrics.RecordDuration(Get(), 0.01, response, failure: null));

        Assert.Equal("callback", thrown.Message);
    }

    [Fact]
    public void A_throwing_ActivityStopped_propagates_from_AttemptTelemetry_End_after_the_span_stopped()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var listener = Throwing(ActivityKind.Client, onStart: false);
        var (_, context) = TracingFixtures.TracedContext();
        var request = context.SeedRequest;
        var scope = new AttemptScope(request, context, UrlRedactor.Default);
        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);

        var thrown = Assert.Throws<InvalidOperationException>(() => telemetry.End());

        Assert.Equal("listener", thrown.Message);
        Assert.True(telemetry.Activity!.IsStopped);
    }

    [Fact]
    public void The_precondition_catches_a_leaked_listener()
    {
        Assert.False(DexpaceDiagnostics.ActivitySource.HasListeners());
        using (var recorder = new ActivityRecorder("Dexpace.Sdk"))
        {
            Assert.True(DexpaceDiagnostics.ActivitySource.HasListeners());
        }

        Assert.False(DexpaceDiagnostics.ActivitySource.HasListeners());
    }

    private static MeterListener ThrowingMeterListener(string instrument)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (published, l) =>
            {
                if (published.Name == instrument)
                {
                    l.EnableMeasurementEvents(published);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((_, _, _, _) => throw new InvalidOperationException("callback"));
        listener.Start();
        return listener;
    }
}
