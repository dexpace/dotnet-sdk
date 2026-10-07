// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-21, OBS-29: the operation span's start, completion, failure and stop (P5c-4, P5c-5).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class OperationTelemetryTests
{
    private static readonly DexpaceClientOptions s_options = new();

    [Fact]
    public void Start_with_no_listener_returns_null()
    {
        Assert.Null(OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options));
    }

    [Fact]
    public void Start_opens_an_internal_span_named_by_the_normalised_method()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");

        using var get = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);
        using var other = OperationTelemetry.Start(new Request(Method.Of("PURGE"), new Uri("https://api.example.com/")), s_options);

        Assert.NotNull(get);
        Assert.Equal(ActivityKind.Internal, get.Kind);
        Assert.Equal("GET", get.DisplayName);
        Assert.NotNull(other);
        Assert.Equal("HTTP", other.DisplayName);
        Assert.Equal("_OTHER", other.GetTagItem("http.request.method"));
        Assert.Equal("PURGE", other.GetTagItem("http.request.method_original"));
    }

    [Fact]
    public void Start_sets_the_seed_tags_with_the_redacted_url_only_when_recording()
    {
        using (var recorder = ActivityRecorder.Scoped("Dexpace.Sdk"))
        {
            using var full = OperationTelemetry.Start(Request.Get("https://api.example.com/v1?token=SECRET&api-version=2"), s_options);
            Assert.NotNull(full);
            Assert.Equal("api.example.com", full.GetTagItem("server.address"));
            Assert.Equal(443, full.GetTagItem("server.port"));
            Assert.Equal("https://api.example.com/v1?token=***&api-version=2", full.GetTagItem("url.full"));
        }

        using var listener = TracingFixtures.NonRecordingListener();
        using var quiet = OperationTelemetry.Start(Request.Get("https://api.example.com/v1?token=SECRET"), s_options);
        Assert.NotNull(quiet);
        Assert.Empty(quiet.TagObjects);
    }

    [Fact]
    public void Start_honours_the_calls_allowed_query_parameters()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var options = new DexpaceClientOptions { Logging = new HttpLoggingOptions { AllowedQueryParameters = ["keep"] } };

        using var span = OperationTelemetry.Start(Request.Get("https://api.example.com/v1?keep=1&drop=2"), options);

        Assert.Equal("https://api.example.com/v1?keep=1&drop=***", span!.GetTagItem("url.full"));
    }

    [Fact]
    public void Complete_records_the_final_status_code_and_leaves_status_unset()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);
        using var response = TestResponses.Create(Status.Ok);

        OperationTelemetry.Complete(span, response);
        OperationTelemetry.Stop(span, settled: true);

        Assert.Equal(200, span!.GetTagItem("http.response.status_code"));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    [Fact]
    public void Fail_records_the_exception_event_error_type_and_Error_status()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);
        var state = TestContexts.For().State;
        var failure = new ServiceRequestException("never sent");

        OperationTelemetry.Fail(span, failure, state);
        OperationTelemetry.Stop(span, settled: true);

        var exceptionEvent = Assert.Single(span!.Events);
        Assert.Equal("exception", exceptionEvent.Name);
        Assert.Equal(typeof(ServiceRequestException).FullName, TracingFixtures.EventTag(exceptionEvent, "exception.type"));
        Assert.Equal("never sent", TracingFixtures.EventTag(exceptionEvent, "exception.message"));
        Assert.Equal(typeof(ServiceRequestException).FullName, span.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("never sent", span.StatusDescription);
    }

    [Fact]
    public void The_exception_event_never_renders_the_suppressed_trail()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);
        var failure = new ServiceRequestException("never sent");
        ExceptionTrail.AddSuppressed(failure, new InvalidOperationException("https://api.example.com/?sig=SECRET"));

        OperationTelemetry.Fail(span, failure, TestContexts.For().State);

        var rendered = string.Join('\n', span!.Events.SelectMany(e => e.Tags).Select(t => t.Value?.ToString()));
        Assert.DoesNotContain("SECRET", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Fail_for_a_cancellation_is_a_failure()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);

        OperationTelemetry.Fail(span, new OperationCanceledException(), TestContexts.For().State);

        Assert.Equal(ActivityStatusCode.Error, span!.Status);
        Assert.Equal("System.OperationCanceledException", span.GetTagItem("error.type"));
    }

    [Fact]
    public void Stop_is_idempotent_and_notifies_the_listener_once()
    {
        var stops = 0;
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var counter = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dexpace.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            // Count only this test's trace: a pipeline call in a parallel class is sampled by this listener too.
            ActivityStopped = activity =>
            {
                if (activity.TraceId == recorder.Root!.TraceId)
                {
                    stops++;
                }
            },
        };
        ActivitySource.AddActivityListener(counter);
        var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);

        OperationTelemetry.Stop(span, settled: true);
        OperationTelemetry.Stop(span, settled: true);
        span!.Stop();

        Assert.Equal(1, stops);
    }

    [Fact]
    public void Stop_with_nothing_settled_sets_Error_without_description_and_no_exception_event()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);

        OperationTelemetry.Stop(span, settled: false);

        Assert.Equal(ActivityStatusCode.Error, span!.Status);
        Assert.Null(span.StatusDescription);
        Assert.Empty(span.Events);
    }

    [Fact]
    public void Stop_after_Complete_does_not_set_Error()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);

        OperationTelemetry.Stop(span, settled: true);

        Assert.NotEqual(ActivityStatusCode.Error, span!.Status);
    }

    [Fact]
    public void Every_mutator_is_a_no_op_on_a_null_span()
    {
        using var response = TestResponses.Create(Status.Ok);

        OperationTelemetry.Complete(null, response);
        OperationTelemetry.Fail(null, new InvalidOperationException(), TestContexts.For().State);
        OperationTelemetry.Stop(null, settled: false);
        OperationTelemetry.Stop(null, settled: true);
    }

    [Fact]
    public void Every_mutator_skips_a_non_recording_span()
    {
        using var listener = TracingFixtures.NonRecordingListener();
        var span = OperationTelemetry.Start(Request.Get("https://api.example.com/"), s_options);
        Assert.NotNull(span);
        using var response = TestResponses.Create(Status.Ok);

        OperationTelemetry.Complete(span, response);
        OperationTelemetry.Fail(span, new InvalidOperationException("boom"), TestContexts.For().State);
        OperationTelemetry.Stop(span, settled: false);

        Assert.Empty(span.TagObjects);
        Assert.Empty(span.Events);
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }
}
