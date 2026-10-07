// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>
/// Placed in a dedicated xUnit collection to prevent parallel execution with other test classes
/// that also exercise DexpaceDiagnostics.ActivitySource, avoiding cross-test activity leakage.
/// </summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class InstrumentationPolicyTests : IDisposable
{
    private readonly ActivityRecorder _listener = new("Dexpace.Sdk");

    private IReadOnlyList<Activity> Activities => _listener.Started;

    public void Dispose() => _listener.Dispose();

    // ─── helpers ─────────────────────────────────────────────────────────────

    private static Request MakeRequest(Uri url) =>
        new Request(Method.Get, url);

    private static DexpaceClientOptions DefaultOptions() => new();

    // Runs the policy with a scripted transport.
    private static async Task<Response> RunAsync(
        HttpPipelinePolicy policy,
        Request request,
        IAsyncHttpClient transport)
    {
        var pipeline = new PipelineBuilder().Add(policy).Build(transport);
        return await pipeline.SendAsync(request, DefaultOptions(), TestContext.Current.CancellationToken);
    }

    // ─── Stage ───────────────────────────────────────────────────────────────

    [Fact]
    public void Stage_IsDiagnostics()
    {
        Assert.Equal(PipelineStage.Diagnostics, new InstrumentationPolicy().Stage);
    }

    // ─── Activity tracing ────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_StartsActivity_WithClientKind()
    {
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(Activities);
        Assert.Equal(ActivityKind.Client, activity.Kind);
    }

    [Fact]
    public async Task ProcessAsync_ActivityName_IsHttpMethod()
    {
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(Activities);
        Assert.Equal("GET", activity.DisplayName);
    }

    [Fact]
    public async Task ProcessAsync_Activity_HasExpectedOtelTags()
    {
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com:8443/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(Activities);
        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
        Assert.NotNull(activity.GetTagItem("url.full"));
        Assert.Equal("api.example.com", activity.GetTagItem("server.address"));
        Assert.Equal(8443, activity.GetTagItem("server.port"));
        Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public async Task ProcessAsync_UrlFull_IsSensitiveParamRedacted()
    {
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        // Default-deny: neither "api_key" nor "page" is in UrlRedactor.DefaultQueryAllowList
        var url = new Uri("https://api.example.com/v1/items?api_key=SECRET123&page=2");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(Activities);
        var urlFull = activity.GetTagItem("url.full") as string;
        Assert.NotNull(urlFull);
        Assert.DoesNotContain("SECRET123", urlFull);
        Assert.Contains("api_key=***", urlFull);
        // A value that is not allow-listed is redacted too (OBS-12)
        Assert.Contains("page=***", urlFull);
    }

    [Fact]
    public async Task ProcessAsync_AttemptNumber_SetOnResendCountTag()
    {
        // Use RetryPolicy + InstrumentationPolicy so AttemptNumber increments
        var transport = new ScriptedTransport([
            TestResponses.Create(Status.ServiceUnavailable),
            TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Add(new InstrumentationPolicy())
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(new Uri("https://api.example.com/")), DefaultOptions(), TestContext.Current.CancellationToken);

        // Two activities should have been started: attempt 0 and attempt 1
        Assert.Equal(2, Activities.Count);
        Assert.Equal(0, Activities[0].GetTagItem("http.request.resend_count"));
        Assert.Equal(1, Activities[1].GetTagItem("http.request.resend_count"));
    }

    [Fact]
    public async Task ProcessAsync_ActivitySetOnContext_DuringContinuation()
    {
        // CapturingPolicy must run AFTER InstrumentationPolicy sets the activity on the context it passes downstream.
        // InstrumentationPolicy is at Diagnostics=600; the Serde stage (700) sorts after it.
        Activity? capturedActivity = null;
        var capturingPolicy = new CapturingPolicy(ctx => capturedActivity = ctx.Activity, stage: PipelineStage.Serde);

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder()
            .Add(new InstrumentationPolicy())
            .Add(capturingPolicy)
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(new Uri("https://api.example.com/")), DefaultOptions(), TestContext.Current.CancellationToken);

        Assert.NotNull(capturedActivity);
    }

    [Fact]
    public async Task ProcessAsync_Exception_SetsErrorTypeTag_AndActivityStatusError()
    {
        var ex = new InvalidOperationException("boom");
        await using var transport = new RecordingTransport(_ => throw ex);
        var policy = new InstrumentationPolicy();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport));

        Assert.Same(ex, thrown);

        var activity = Assert.Single(Activities);
        var errorType = activity.GetTagItem("error.type") as string;
        Assert.NotNull(errorType);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public async Task ProcessAsync_NoListener_DoesNotThrow()
    {
        // Dispose the listener — now no listener is active, StartActivity returns null.
        _listener.Dispose();

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();

        // Must not throw even when Activity is null
        var result = await RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport);
        Assert.Equal(Status.Ok, result.Status);
    }

    // ─── Metrics ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_RecordsDurationHistogram()
    {
        using var meterListener = new MetricRecorder("Dexpace.Sdk", "http.client.request.duration");

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        await RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport);

        meterListener.RecordObservableInstruments();
        var recordedDuration = meterListener.For("http.client.request.duration") is [.., var lastDuration]
            ? lastDuration.Value
            : (double?)null;
        Assert.NotNull(recordedDuration);
        Assert.True(recordedDuration >= 0, "Duration must be non-negative");
    }

    [Fact]
    public async Task ProcessAsync_ActiveRequestsCounter_IncrementsThenDecrements()
    {
        using var meterListener = new MetricRecorder("Dexpace.Sdk", "http.client.active_requests");

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        await RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport);

        meterListener.RecordObservableInstruments();
        long maxObserved = 0;
        long lastObserved = 0;
        foreach (var measurement in meterListener.For("http.client.active_requests"))
        {
            lastObserved += (long)measurement.Value;
            if (lastObserved > maxObserved)
            {
                maxObserved = lastObserved;
            }
        }

        // After completion the counter should be back to 0 (net effect)
        Assert.Equal(0, lastObserved);
        // And at some point during the call it was positive
        Assert.True(maxObserved > 0, "Active requests should have been incremented");
    }

    // ─── Logging ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_LogsStructuredEvent_WithRedactedUrl()
    {
        var logger = new RecordingLogger();
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy(logger);
        var url = new Uri("https://api.example.com/v1/items?api_key=SECRET&x=1");

        await RunAsync(policy, MakeRequest(url), transport);

        Assert.NotEmpty(logger.Entries);
        // Verify that no log entry contains the secret
        foreach (var (_, message) in logger.Entries)
        {
            Assert.DoesNotContain("SECRET", message);
        }
        // Verify the redacted marker is present in at least one entry
        Assert.Contains(logger.Entries, e => e.Message.Contains("api_key=***"));
    }

    [Fact]
    public async Task ProcessAsync_NullLogger_DoesNotThrow()
    {
        // Passing null logger should fall back to NullLogger.Instance
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy(null);
        var result = await RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport);
        Assert.Equal(Status.Ok, result.Status);
    }

    // ─── W3C trace-context injection ─────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_W3CActivity_InjectsTraceparentHeader()
    {
        // Arrange: capture the request the transport receives.
        Request? capturedRequest = null;
        var transport = new RecordingTransport(req =>
        {
            capturedRequest = req;
            return TestResponses.Create(Status.Ok);
        });
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        // The listener fixture uses W3C format (the .NET default).
        var started = Assert.Single(Activities);
        Assert.Equal(ActivityIdFormat.W3C, started.IdFormat);
        Assert.NotNull(capturedRequest);
        var traceparent = capturedRequest.Headers.Get("traceparent");
        Assert.NotNull(traceparent);
        Assert.Equal(started.Id, traceparent);
    }

    [Fact]
    public async Task ProcessAsync_W3CActivity_InjectsTracestateHeader_WhenNonEmpty()
    {
        // Arrange: start a parent activity with tracestate so the child inherits it.
        using var parentActivity = new Activity("parent");
        parentActivity.TraceStateString = "vendor=value";
        parentActivity.Start();

        try
        {
            Request? capturedRequest = null;
            var transport = new RecordingTransport(req =>
            {
                capturedRequest = req;
                return TestResponses.Create(Status.Ok);
            });
            var policy = new InstrumentationPolicy();

            await RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport);

            Assert.NotNull(capturedRequest);
            var tracestate = capturedRequest.Headers.Get("tracestate");
            Assert.NotNull(tracestate);
            Assert.False(string.IsNullOrEmpty(tracestate));
        }
        finally
        {
            parentActivity.Stop();
        }
    }

    [Fact]
    public async Task ProcessAsync_NoListener_DoesNotInjectTraceparentHeader()
    {
        // Dispose the listener so StartActivity returns null.
        _listener.Dispose();

        Request? capturedRequest = null;
        var transport = new RecordingTransport(req =>
        {
            capturedRequest = req;
            return TestResponses.Create(Status.Ok);
        });
        var policy = new InstrumentationPolicy();

        await RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport);

        Assert.NotNull(capturedRequest);
        Assert.False(capturedRequest.Headers.Contains("traceparent"), "traceparent must not be added when there is no activity");
    }

    // ─── Metric dimensions ────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_DurationHistogram_CarriesMethodAndStatusTags()
    {
        // The recorder materialises each measurement's tag span, so the tags can be inspected after the call.
        using var meterListener = new MetricRecorder("Dexpace.Sdk", "http.client.request.duration");

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        await RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport);

        meterListener.RecordObservableInstruments();
        var capturedTags = meterListener.For("http.client.request.duration") is [.., var last] ? last.Tags : null;
        Assert.NotNull(capturedTags);

        var tagDict = capturedTags.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.True(tagDict.ContainsKey("http.request.method"), "Missing http.request.method tag");
        Assert.True(tagDict.ContainsKey("http.response.status_code"), "Missing http.response.status_code tag");
        Assert.Equal("GET", tagDict["http.request.method"]);
    }

    // ─── url.scheme tag ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_Activity_HasUrlSchemeTag()
    {
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(Activities);
        Assert.Equal("https", activity.GetTagItem("url.scheme"));
    }

    // ─── the activity travels downstream only (PIPE-16) ──────────────────────

    [Fact]
    public async Task The_downstream_sees_the_activity_and_the_upstream_does_not()
    {
        // The write cannot reach upward: the policy above instrumentation holds its own context and sees no span set by
        // the policy below it, so there is nothing to restore.
        Activity? upstreamAfter = new Activity("untouched");
        Activity? downstream = null;

        var upstream = new DelegatePolicy(
            PipelineStage.Auth,
            async (request, ctx, next) =>
            {
                var response = await next.RunAsync(request, ctx).ConfigureAwait(false);
                upstreamAfter = ctx.Activity;
                return response;
            });
        var capture = new CapturingPolicy(ctx => downstream = ctx.Activity, stage: PipelineStage.Serde);

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder()
            .Add(upstream)
            .Add(new InstrumentationPolicy())
            .Add(capture)
            .Build(transport);

        using var response = await pipeline.SendAsync(MakeRequest(new Uri("https://api.example.com/")), DefaultOptions(), TestContext.Current.CancellationToken);

        Assert.NotNull(downstream);
        Assert.Null(upstreamAfter);
    }

    [Fact]
    public void Process_sync_records_activity_and_returns_the_response()
    {
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy()).Build(transport);

        using var response = pipeline.Send(MakeRequest(new Uri("https://api.example.com/")), DefaultOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        var activity = Assert.Single(Activities);
        Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
    }

    // ─── Nested helpers ──────────────────────────────────────────────────────

    private sealed class CapturingPolicy(Action<PipelineContext> capture, PipelineStage stage = PipelineStage.PerAttempt) : HttpPipelinePolicy
    {
        public override PipelineStage Stage => stage;

        public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
        {
            capture(context);
            return continuation.RunAsync(request, context);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
