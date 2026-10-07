// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.Metrics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-31, OBS-32, OBS-33: the two instruments and their stable attribute sets (P5c-10).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class HttpClientMetricsTests
{
    private const string Duration = "http.client.request.duration";
    private const string Active = "http.client.active_requests";

    private static Request RequestTo(string host, string scheme = "https", Method? method = null) =>
        new(method ?? Method.Get, new Uri($"{scheme}://{host}/v1/items"));

    private static Dictionary<string, object?> TagsOf(RecordedMeasurement measurement) =>
        measurement.Tags.ToDictionary(t => t.Key, t => t.Value);

    [Fact]
    public void Duration_has_the_stable_attribute_set()
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Duration);
        using var response = TestResponses.Create(Status.Ok);

        HttpClientMetrics.RecordDuration(RequestTo(host), 0.01, response, failure: null);

        var tags = TagsOf(Assert.Single(recorder.For(Duration)));
        Assert.Equal("GET", tags["http.request.method"]);
        Assert.Equal(host, tags["server.address"]);
        Assert.Equal(443, tags["server.port"]);
        Assert.Equal("https", tags["url.scheme"]);
        Assert.Equal(200, tags["http.response.status_code"]);
        Assert.Equal("1.1", tags["network.protocol.version"]);
        Assert.DoesNotContain("error.type", tags.Keys);
    }

    [Fact]
    public void Duration_is_in_seconds_and_the_unit_is_s()
    {
        Assert.Equal("s", HttpClientMetrics.RequestDuration.Unit);
        Assert.Equal("{request}", HttpClientMetrics.ActiveRequests.Unit);
    }

    [Fact]
    public void Active_requests_has_the_start_attribute_set_and_the_same_tags_for_plus_and_minus_one()
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Active);
        var request = RequestTo(host, "http");

        var counted = HttpClientMetrics.RequestStarted(request);
        HttpClientMetrics.RequestEnded(request, counted);

        var measurements = recorder.For(Active);
        Assert.Equal([1d, -1d], measurements.Select(m => m.Value));
        foreach (var measurement in measurements)
        {
            Assert.Equal(
                ["http.request.method", "server.address", "server.port", "url.scheme"],
                measurement.Tags.Select(t => t.Key).Order(StringComparer.Ordinal));
            Assert.Equal(80, measurement.Tag("server.port"));
        }

        Assert.Equal(
            recorder.For(Active)[0].Tags.Select(t => (t.Key, t.Value)),
            recorder.For(Active)[1].Tags.Select(t => (t.Key, t.Value)));
    }

    [Fact]
    public async Task Active_requests_returns_to_zero_after_success_and_after_failure()
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Active);
        var failing = TracingFixtures.Pipeline(new RecordingTransport(_ => throw new InvalidOperationException("boom")));
        var succeeding = TracingFixtures.Pipeline(new RecordingTransport());

        using var response = await succeeding.SendAsync(RequestTo(host), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failing.SendAsync(RequestTo(host), TestContext.Current.CancellationToken));

        Assert.Equal(4, recorder.For(Active).Count);
        Assert.Equal(0d, recorder.For(Active).Sum(m => m.Value));
    }

    [Fact]
    public void Active_requests_does_not_decrement_what_it_did_not_increment()
    {
        var host = TestHosts.Unique();
        var request = RequestTo(host);

        var counted = HttpClientMetrics.RequestStarted(request);
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Active);
        HttpClientMetrics.RequestEnded(request, counted);

        Assert.False(counted);
        Assert.Empty(recorder.For(Active));
    }

    [Theory]
    [InlineData("https", 443)]
    [InlineData("http", 80)]
    public void Server_port_is_the_port_number_for_a_default_port(string scheme, int port)
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Duration);
        using var response = TestResponses.Create(Status.Ok);

        HttpClientMetrics.RecordDuration(RequestTo(host, scheme), 0.01, response, failure: null);

        Assert.Equal(port, Assert.Single(recorder.For(Duration)).Tag("server.port"));
    }

    [Fact]
    public void An_explicit_port_is_recorded_as_is()
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Duration);
        using var response = TestResponses.Create(Status.Ok);

        HttpClientMetrics.RecordDuration(new Request(Method.Get, new Uri($"https://{host}:8443/")), 0.01, response, failure: null);

        Assert.Equal(8443, Assert.Single(recorder.For(Duration)).Tag("server.port"));
    }

    [Fact]
    public void A_4xx_sets_error_type_to_the_status_code_string_on_the_duration()
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Duration);
        using var response = TestResponses.Create(Status.NotFound);

        HttpClientMetrics.RecordDuration(RequestTo(host), 0.01, response, failure: null);

        var measurement = Assert.Single(recorder.For(Duration));
        Assert.Equal("404", measurement.Tag("error.type"));
        Assert.Equal(404, measurement.Tag("http.response.status_code"));
    }

    [Fact]
    public void A_failure_sets_error_type_to_the_exception_type_and_no_status_code()
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Duration);

        HttpClientMetrics.RecordDuration(RequestTo(host), 0.01, response: null, new InvalidOperationException());

        var measurement = Assert.Single(recorder.For(Duration));
        Assert.Equal("System.InvalidOperationException", measurement.Tag("error.type"));
        Assert.Null(measurement.Tag("http.response.status_code"));
    }

    [Fact]
    public void An_unknown_method_is_OTHER_on_both_instruments()
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host);
        var request = RequestTo(host, method: Method.Of("PURGE"));
        using var response = TestResponses.Create(Status.Ok);

        var counted = HttpClientMetrics.RequestStarted(request);
        HttpClientMetrics.RecordDuration(request, 0.01, response, failure: null);
        HttpClientMetrics.RequestEnded(request, counted);

        Assert.All(recorder.Measurements, m => Assert.Equal("_OTHER", m.Tag("http.request.method")));
        Assert.Equal(3, recorder.Measurements.Count);
    }

    [Fact]
    public void The_histogram_tolerates_NaN_and_infinity()
    {
        var histogram = HttpClientMetrics.RequestDuration;

        histogram.Record(double.NaN);
        histogram.Record(double.PositiveInfinity);
        histogram.Record(double.NegativeInfinity);

        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host, Duration);
        var tag = new KeyValuePair<string, object?>("server.address", host);
        histogram.Record(double.NaN, tag);
        histogram.Record(double.PositiveInfinity, tag);
        histogram.Record(double.NegativeInfinity, tag);

        Assert.Equal(3, recorder.For(Duration).Count);
    }

    [Fact]
    public void The_instruments_manufacture_the_three_kinds_with_per_measurement_tags()
    {
        var host = TestHosts.Unique();
        using var recorder = MetricRecorder.ForServer("Dexpace.Sdk", host);
        var tag = new KeyValuePair<string, object?>("server.address", host);
        var counter = DexpaceDiagnostics.Meter.CreateCounter<long>("tests.counter");
        var upDown = DexpaceDiagnostics.Meter.CreateUpDownCounter<long>("tests.updown");
        var histogram = DexpaceDiagnostics.Meter.CreateHistogram<double>("tests.histogram");

        counter.Add(2, tag);
        upDown.Add(-3, tag);
        histogram.Record(0.5, tag);

        Assert.Equal(2d, Assert.Single(recorder.For("tests.counter")).Value);
        Assert.Equal(-3d, Assert.Single(recorder.For("tests.updown")).Value);
        Assert.Equal(0.5d, Assert.Single(recorder.For("tests.histogram")).Value);
    }

    [Fact]
    public void The_histogram_carries_the_bucket_advice()
    {
        Assert.Equal(
            [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10],
            HttpClientMetrics.RequestDuration.Advice?.HistogramBucketBoundaries);
    }
}
