// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-21, OBS-25, OBS-30, OBS-32: the span and metric half of an attempt (P5c-8, P5c-13).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class AttemptTelemetryTests
{
    private static AttemptScope ScopeOf(Request request, PipelineContext context) => new(request, context, UrlRedactor.Default);

    [Fact]
    public void An_untraced_begin_holds_no_span()
    {
        var request = Request.Get("https://api.example.com/v1/items?token=SECRET");
        var context = TestContexts.For(request);
        var scope = ScopeOf(request, context);

        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
        telemetry.End();

        Assert.Null(telemetry.Activity);
        Assert.Same(request, telemetry.Outgoing);
        Assert.Same(context, telemetry.Downstream);
    }

    [Fact]
    public void A_traced_begin_starts_a_client_span_under_the_bundle_with_the_start_tags()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext(Request.Get("https://api.example.com/v1/items?token=SECRET"));
        var request = context.SeedRequest;
        var scope = ScopeOf(request, context);

        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);

        var span = telemetry.Activity;
        Assert.NotNull(span);
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal("GET", span.DisplayName);
        Assert.Equal(operation.Id, span.ParentId);
        Assert.Equal("GET", span.GetTagItem("http.request.method"));
        Assert.Equal("api.example.com", span.GetTagItem("server.address"));
        Assert.Equal(443, span.GetTagItem("server.port"));
        Assert.Equal("https", span.GetTagItem("url.scheme"));
        Assert.Equal("https://api.example.com/v1/items?token=***", span.GetTagItem("url.full"));
        Assert.Equal(span.Id, telemetry.Outgoing.Headers.Get("traceparent"));
        Assert.Same(span, telemetry.Downstream.Activity);
        telemetry.End();
    }

    [Fact]
    public void The_first_transmission_omits_resend_count_and_a_later_one_carries_its_ordinal()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (_, context) = TracingFixtures.TracedContext();
        var request = context.SeedRequest;
        var spans = new List<Activity>();

        for (var i = 0; i < 3; i++)
        {
            var scope = ScopeOf(request, context);
            var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
            spans.Add(telemetry.Activity!);
            telemetry.End();
        }

        Assert.Null(spans[0].GetTagItem("http.request.resend_count"));
        Assert.Equal(1, spans[1].GetTagItem("http.request.resend_count"));
        Assert.Equal(2, spans[2].GetTagItem("http.request.resend_count"));
    }

    [Fact]
    public void An_unknown_method_is_OTHER_with_the_original_and_the_span_is_named_HTTP()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (_, context) = TracingFixtures.TracedContext(new Request(Method.Of("PURGE"), new Uri("https://api.example.com/")));
        var request = context.SeedRequest;
        var scope = ScopeOf(request, context);

        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);

        Assert.Equal("HTTP", telemetry.Activity!.DisplayName);
        Assert.Equal("_OTHER", telemetry.Activity.GetTagItem("http.request.method"));
        Assert.Equal("PURGE", telemetry.Activity.GetTagItem("http.request.method_original"));
        telemetry.End();
    }

    [Fact]
    public void Succeeded_sets_status_code_and_protocol_version_and_leaves_the_status_unset_for_a_2xx()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (_, context) = TracingFixtures.TracedContext();
        var request = context.SeedRequest;
        var scope = ScopeOf(request, context);
        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
        using var response = TestResponses.Create(Status.Ok, protocol: Protocol.Http2);

        telemetry.Succeeded(response, ref scope);
        telemetry.End();

        Assert.Equal(200, telemetry.Activity!.GetTagItem("http.response.status_code"));
        Assert.Equal("2", telemetry.Activity.GetTagItem("network.protocol.version"));
        Assert.Equal(ActivityStatusCode.Unset, telemetry.Activity.Status);
        Assert.Null(telemetry.Activity.GetTagItem("error.type"));
    }

    [Fact]
    public void Succeeded_with_a_4xx_or_5xx_sets_error_type_to_the_status_string_and_status_Error_without_description()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (_, context) = TracingFixtures.TracedContext();
        var request = context.SeedRequest;
        var scope = ScopeOf(request, context);
        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
        using var response = TestResponses.Create(Status.ServiceUnavailable);

        telemetry.Succeeded(response, ref scope);
        telemetry.End();

        Assert.Equal("503", telemetry.Activity!.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, telemetry.Activity.Status);
        Assert.Null(telemetry.Activity.StatusDescription);
    }

    [Fact]
    public void Failed_sets_error_type_to_the_exception_type_and_status_Error_with_the_message()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (_, context) = TracingFixtures.TracedContext();
        var request = context.SeedRequest;
        var scope = ScopeOf(request, context);
        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);

        telemetry.Failed(new InvalidOperationException("boom"), ref scope);
        telemetry.End();

        Assert.Equal("System.InvalidOperationException", telemetry.Activity!.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, telemetry.Activity.Status);
        Assert.Equal("boom", telemetry.Activity.StatusDescription);
    }

    [Fact]
    public void A_non_recording_span_is_not_written_to()
    {
        using var listener = TracingFixtures.NonRecordingListener();
        var (operation, context) = TracingFixtures.TracedContext();
        var request = context.SeedRequest;
        var scope = ScopeOf(request, context);

        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
        using var response = TestResponses.Create(Status.NotFound);
        telemetry.Succeeded(response, ref scope);
        telemetry.Failed(new InvalidOperationException("boom"), ref scope);
        telemetry.End();
        operation.Dispose();

        var span = telemetry.Activity;
        Assert.NotNull(span);
        Assert.False(span.IsAllDataRequested);
        Assert.Empty(span.TagObjects);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    [Fact]
    public void End_is_idempotent_and_stops_the_span_once()
    {
        var stops = 0;
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var counter = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dexpace.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.Kind == ActivityKind.Client)
                {
                    stops++;
                }
            },
        };
        ActivitySource.AddActivityListener(counter);
        var (_, context) = TracingFixtures.TracedContext();
        var request = context.SeedRequest;
        var scope = ScopeOf(request, context);
        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);

        telemetry.End();
        telemetry.End();
        telemetry.Activity!.Stop();

        Assert.Equal(1, stops);
    }

    [Fact]
    public void The_metrics_pair_is_recorded_by_End_even_when_the_attempt_failed()
    {
        var host = TestHosts.Unique();
        using var metrics = MetricRecorder.ForServer("Dexpace.Sdk", host, "http.client.request.duration", "http.client.active_requests");
        var request = Request.Get($"https://{host}/");
        var context = TestContexts.For(request);
        var scope = ScopeOf(request, context);

        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
        telemetry.Failed(new InvalidOperationException(), ref scope);
        telemetry.End();

        Assert.Single(metrics.For("http.client.request.duration"));
        Assert.Equal([1d, -1d], metrics.For("http.client.active_requests").Select(m => m.Value));
    }

    [Fact]
    public void An_untraced_bundle_still_counts_the_transmission()
    {
        var request = Request.Get("https://api.example.com/");
        var context = TestContexts.For(request, dispatch: new DispatchContext(InstrumentationContext.None));
        var scope = ScopeOf(request, context);

        var telemetry = AttemptTelemetry.Begin(ref scope, request, context);
        telemetry.End();

        Assert.Equal(1, context.State.Transmissions);
    }
}
