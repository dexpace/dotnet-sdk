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
public sealed class InstrumentationPolicyTests
{
    // Every HttpPipeline call opens an operation span (OBS-29); these tests assert on the Client-kind attempt spans.
    private static IReadOnlyList<Activity> ClientSpans(ActivityRecorder recorder) => recorder.StartedOfKind(ActivityKind.Client);

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
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(ClientSpans(recorder));
        Assert.Equal(ActivityKind.Client, activity.Kind);
    }

    [Fact]
    public async Task ProcessAsync_ActivityName_IsHttpMethod()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(ClientSpans(recorder));
        Assert.Equal("GET", activity.DisplayName);
    }

    [Fact]
    public async Task ProcessAsync_Activity_HasExpectedOtelTags()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com:8443/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(ClientSpans(recorder));
        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
        Assert.NotNull(activity.GetTagItem("url.full"));
        Assert.Equal("api.example.com", activity.GetTagItem("server.address"));
        Assert.Equal(8443, activity.GetTagItem("server.port"));
        Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public async Task ProcessAsync_UrlFull_IsSensitiveParamRedacted()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        // Default-deny: neither "api_key" nor "page" is in UrlRedactor.DefaultQueryAllowList
        var url = new Uri("https://api.example.com/v1/items?api_key=SECRET123&page=2");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(ClientSpans(recorder));
        var urlFull = activity.GetTagItem("url.full") as string;
        Assert.NotNull(urlFull);
        Assert.DoesNotContain("SECRET123", urlFull);
        Assert.Contains("api_key=***", urlFull);
        // A value that is not allow-listed is redacted too (OBS-12)
        Assert.Contains("page=***", urlFull);
    }

    [Fact]
    public async Task The_url_full_tag_honours_the_calls_allowed_query_parameters()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy()).Build(transport);
        var request = MakeRequest(new Uri("https://api.example.com/v1/items?keep=1&drop=2"));

        using var configured = await pipeline.SendAsync(
            request,
            new DexpaceClientOptions { Logging = new HttpLoggingOptions { AllowedQueryParameters = ["keep"] } },
            TestContext.Current.CancellationToken);
        using var defaulted = await pipeline.SendAsync(request, DefaultOptions(), TestContext.Current.CancellationToken);

        Assert.Equal("https://api.example.com/v1/items?keep=1&drop=***", ClientSpans(recorder)[0].GetTagItem("url.full"));
        Assert.Equal("https://api.example.com/v1/items?keep=***&drop=***", ClientSpans(recorder)[1].GetTagItem("url.full"));
    }

    [Fact]
    public async Task The_default_url_full_tag_is_unchanged()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));

        using var response = await RunAsync(
            new InstrumentationPolicy(),
            MakeRequest(new Uri("https://api.example.com/v1/items?api-version=2&token=T")),
            transport);

        Assert.Equal("https://api.example.com/v1/items?api-version=2&token=***", Assert.Single(ClientSpans(recorder)).GetTagItem("url.full"));
    }

    [Fact]
    public async Task Resend_count_is_absent_on_the_first_transmission_and_counts_retries()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
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

        // Two attempt spans: the first transmission carries no resend count (the convention's SHOULD NOT), the second 1.
        Assert.Equal(2, ClientSpans(recorder).Count);
        Assert.Null(ClientSpans(recorder)[0].GetTagItem("http.request.resend_count"));
        Assert.Equal(1, ClientSpans(recorder)[1].GetTagItem("http.request.resend_count"));
    }

    [Fact]
    public async Task ProcessAsync_ActivitySetOnContext_DuringContinuation()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
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
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var ex = new InvalidOperationException("boom");
        await using var transport = new RecordingTransport(_ => throw ex);
        var policy = new InstrumentationPolicy();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(policy, MakeRequest(new Uri("https://api.example.com/")), transport));

        Assert.Same(ex, thrown);

        var activity = Assert.Single(ClientSpans(recorder));
        var errorType = activity.GetTagItem("error.type") as string;
        Assert.NotNull(errorType);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public async Task ProcessAsync_NoListener_DoesNotThrow()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        // Dispose the listener — now no listener is active, StartActivity returns null.
        recorder.Dispose();

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
        var host = TestHosts.Unique();
        using var meterListener = MetricRecorder.ForServer("Dexpace.Sdk", host, "http.client.request.duration");

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        await RunAsync(policy, MakeRequest(new Uri($"https://{host}/")), transport);

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
        var host = TestHosts.Unique();
        using var meterListener = MetricRecorder.ForServer("Dexpace.Sdk", host, "http.client.active_requests");

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        await RunAsync(policy, MakeRequest(new Uri($"https://{host}/")), transport);

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
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(logger)).Build(transport);
        var url = new Uri("https://api.example.com/v1/items?api_key=SECRET&x=1");

        // Logging is opt-in (OBS-34): nothing is logged at the default level.
        using var silent = await pipeline.SendAsync(MakeRequest(url), DefaultOptions(), TestContext.Current.CancellationToken);
        Assert.Empty(logger.Entries);

        using var response = await pipeline.SendAsync(
            MakeRequest(url),
            new DexpaceClientOptions { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers } },
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(logger.Entries);
        Assert.All(logger.Entries, e => Assert.DoesNotContain("SECRET", e.Message, StringComparison.Ordinal));
        Assert.All(logger.Entries, e => Assert.Contains("api_key=***", (string)e[DexpaceLogKeys.UrlFull]!, StringComparison.Ordinal));
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
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
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
        var started = Assert.Single(ClientSpans(recorder));
        Assert.Equal(ActivityIdFormat.W3C, started.IdFormat);
        Assert.NotNull(capturedRequest);
        var traceparent = capturedRequest.Headers.Get("traceparent");
        Assert.NotNull(traceparent);
        Assert.Equal(started.Id, traceparent);
    }

    [Fact]
    public async Task ProcessAsync_W3CActivity_InjectsTracestateHeader_WhenNonEmpty()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
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
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        // Dispose the listener so StartActivity returns null.
        recorder.Dispose();

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
        var host = TestHosts.Unique();
        using var meterListener = MetricRecorder.ForServer("Dexpace.Sdk", host, "http.client.request.duration");

        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        await RunAsync(policy, MakeRequest(new Uri($"https://{host}/")), transport);

        meterListener.RecordObservableInstruments();
        var capturedTags = meterListener.For("http.client.request.duration") is [.., var last] ? last.Tags : null;
        Assert.NotNull(capturedTags);

        // OBS-32: the stable attribute set.
        var tagDict = capturedTags.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal("GET", tagDict["http.request.method"]);
        Assert.Equal(host, tagDict["server.address"]);
        Assert.Equal(443, tagDict["server.port"]);
        Assert.Equal("https", tagDict["url.scheme"]);
        Assert.Equal(200, tagDict["http.response.status_code"]);
        Assert.Equal("1.1", tagDict["network.protocol.version"]);
        Assert.False(tagDict.ContainsKey("error.type"));
    }

    // ─── url.scheme tag ───────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_Activity_HasUrlSchemeTag()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var policy = new InstrumentationPolicy();
        var url = new Uri("https://api.example.com/v1/items");

        await RunAsync(policy, MakeRequest(url), transport);

        var activity = Assert.Single(ClientSpans(recorder));
        Assert.Equal("https", activity.GetTagItem("url.scheme"));
    }

    // ─── the activity travels downstream only (PIPE-16) ──────────────────────

    [Fact]
    public async Task The_downstream_sees_the_activity_and_the_upstream_does_not()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
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
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy()).Build(transport);

        using var response = pipeline.Send(MakeRequest(new Uri("https://api.example.com/")), DefaultOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        var activity = Assert.Single(ClientSpans(recorder));
        Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
    }

    // ─── phase 5c: the attempt span follows the HTTP client conventions ───────

    [Theory]
    [InlineData("https://api.example.com/", 443)]
    [InlineData("http://api.example.com/", 80)]
    [InlineData("https://api.example.com:8443/", 8443)]
    public async Task Server_port_is_the_port_number_for_a_default_port(string url, int port)
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));

        using var response = await RunAsync(new InstrumentationPolicy(), MakeRequest(new Uri(url)), transport);

        Assert.Equal(port, Assert.Single(ClientSpans(recorder)).GetTagItem("server.port"));
    }

    [Fact]
    public async Task A_4xx_response_sets_error_type_and_Error_status_on_the_attempt_span()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.NotFound));

        using var response = await RunAsync(new InstrumentationPolicy(), MakeRequest(new Uri("https://api.example.com/")), transport);

        var activity = Assert.Single(ClientSpans(recorder));
        Assert.Equal("404", activity.GetTagItem("error.type"));
        Assert.Equal(404, activity.GetTagItem("http.response.status_code"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.StatusDescription);
    }

    [Fact]
    public async Task A_redirect_hop_continues_the_resend_count()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new ScriptedTransport([
            () => TestResponses.Redirect(302, "https://api.example.com/next"),
            () => TestResponses.Create(Status.Ok),
        ]);
        var pipeline = new PipelineBuilder()
            .Add(new RedirectPolicy())
            .Add(new InstrumentationPolicy())
            .Build(transport);

        using var response = await pipeline.SendAsync(MakeRequest(new Uri("https://api.example.com/")), DefaultOptions(), TestContext.Current.CancellationToken);

        var spans = ClientSpans(recorder);
        Assert.Equal(2, spans.Count);
        Assert.Null(spans[0].GetTagItem("http.request.resend_count"));
        Assert.Equal(1, spans[1].GetTagItem("http.request.resend_count"));
    }

    [Fact]
    public async Task An_unknown_method_is_OTHER_and_the_span_is_named_HTTP()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));

        using var response = await RunAsync(
            new InstrumentationPolicy(),
            new Request(Method.Of("PURGE"), new Uri("https://api.example.com/")),
            transport);

        var activity = Assert.Single(ClientSpans(recorder));
        Assert.Equal("HTTP", activity.DisplayName);
        Assert.Equal("_OTHER", activity.GetTagItem("http.request.method"));
        Assert.Equal("PURGE", activity.GetTagItem("http.request.method_original"));
    }

    [Fact]
    public async Task The_attempt_span_is_a_child_of_the_operation_span_not_of_the_ambient_activity()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.Ok));

        using var response = await RunAsync(new InstrumentationPolicy(), MakeRequest(new Uri("https://api.example.com/")), transport);

        var operation = Assert.Single(recorder.StartedOfKind(ActivityKind.Internal));
        Assert.Equal(operation.Id, Assert.Single(ClientSpans(recorder)).ParentId);
        Assert.Equal(recorder.Root!.Id, operation.ParentId);
    }

    [Fact]
    public async Task Spans_and_metrics_record_at_the_default_log_level()
    {
        // OBS-34 (5b's row): the span lifecycle and the instruments run whatever the log level; the default is None.
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var host = TestHosts.Unique();
        using var metrics = MetricRecorder.ForServer("Dexpace.Sdk", host, "http.client.request.duration");
        var logger = new RecordingLogger();
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(logger)).Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(new Uri($"https://{host}/")), DefaultOptions(), TestContext.Current.CancellationToken);

        Assert.Empty(logger.Entries);
        Assert.Single(ClientSpans(recorder));
        Assert.Single(metrics.For("http.client.request.duration"));
    }

    [Fact]
    public void The_sync_path_fills_the_same_tags_as_the_async_path()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new RecordingTransport(_ => TestResponses.Create(Status.NotFound));
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy()).Build(transport);

        using var response = pipeline.Send(MakeRequest(new Uri("https://api.example.com:8443/v1/items?x=1")), DefaultOptions(), TestContext.Current.CancellationToken);

        var activity = Assert.Single(ClientSpans(recorder));
        Assert.Equal(8443, activity.GetTagItem("server.port"));
        Assert.Equal("https", activity.GetTagItem("url.scheme"));
        Assert.Equal("404", activity.GetTagItem("error.type"));
        Assert.Equal("1.1", activity.GetTagItem("network.protocol.version"));
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
}
