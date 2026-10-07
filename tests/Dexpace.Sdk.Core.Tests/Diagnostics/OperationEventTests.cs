// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-28, OBS-29: the per-attempt and per-hop event shape phase 6 emits through (P5c-6, P5c-7).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class OperationEventTests
{
    [Fact]
    public void Attempt_failed_carries_resend_count_error_type_status_and_delay_in_seconds()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext();
        context.State.NextTransmission();
        using var response = TestResponses.Create(Status.ServiceUnavailable);

        OperationTelemetry.AttemptFailed(context, response, failure: null, TimeSpan.FromMilliseconds(1500));
        OperationTelemetry.AttemptFailed(context, response: null, new ServiceRequestException("x"), TimeSpan.FromSeconds(2));

        var first = operation.Events.First();
        Assert.Equal("dexpace.attempt.failed", first.Name);
        Assert.Equal(0, TracingFixtures.EventTag(first, "http.request.resend_count"));
        Assert.Equal("503", TracingFixtures.EventTag(first, "error.type"));
        Assert.Equal(503, TracingFixtures.EventTag(first, "http.response.status_code"));
        Assert.Equal(1.5d, Assert.IsType<double>(TracingFixtures.EventTag(first, "dexpace.retry.delay")));
        var second = operation.Events.Last();
        Assert.Equal(typeof(ServiceRequestException).FullName, TracingFixtures.EventTag(second, "error.type"));
        Assert.Null(TracingFixtures.EventTag(second, "http.response.status_code"));
        Assert.Equal(2d, TracingFixtures.EventTag(second, "dexpace.retry.delay"));
    }

    [Fact]
    public void Attempt_failed_is_emitted_on_the_operation_span_not_the_attempt_span()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext();
        var attempt = DexpaceDiagnostics.ActivitySource.StartActivity("attempt", ActivityKind.Client, operation.Context);

        OperationTelemetry.AttemptFailed(context, response: null, new ServiceRequestException("x"), TimeSpan.Zero);

        Assert.Single(operation.Events);
        Assert.Empty(attempt!.Events);
    }

    [Fact]
    public void Nothing_is_emitted_on_an_untraced_call()
    {
        var context = TestContexts.For(dispatch: new DispatchContext(InstrumentationContext.None));

        OperationTelemetry.AttemptFailed(context, response: null, new ServiceRequestException("x"), TimeSpan.Zero);
        OperationTelemetry.RedirectHop(context, 1, 302, new Uri("https://other.example.com/"), crossOrigin: true);

        Assert.Null(context.Instrumentation.ActiveSpan);
    }

    [Fact]
    public void Nothing_is_emitted_on_a_non_recording_operation_span()
    {
        using var listener = TracingFixtures.NonRecordingListener();
        var (operation, context) = TracingFixtures.TracedContext();

        OperationTelemetry.AttemptFailed(context, response: null, new ServiceRequestException("x"), TimeSpan.Zero);
        OperationTelemetry.RedirectHop(context, 1, 302, new Uri("https://other.example.com/"), crossOrigin: true);

        Assert.Empty(operation.Events);
    }

    [Fact]
    public void RetriesExhausted_records_on_the_call_state_and_emits_nothing_yet()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext();

        OperationTelemetry.RetriesExhausted(context, 3);

        Assert.True(context.State.TryGetExhaustion(out var attempts));
        Assert.Equal(3, attempts);
        Assert.Empty(operation.Events);
    }

    [Fact]
    public void RetrySequenceStarted_clears_the_exhaustion_record()
    {
        var context = TestContexts.For();
        OperationTelemetry.RetriesExhausted(context, 3);

        OperationTelemetry.RetrySequenceStarted(context);

        Assert.False(context.State.TryGetExhaustion(out _));
    }

    [Fact]
    public void Fail_emits_exhausted_immediately_before_the_exception_event()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext();
        OperationTelemetry.RetriesExhausted(context, 3);
        var failure = new ServiceResponseException("unreadable");

        OperationTelemetry.Fail(operation, failure, context.State);

        Assert.Equal(["dexpace.retry.exhausted", "exception"], TracingFixtures.EventNames(operation));
        var exhausted = operation.Events.First();
        var exception = operation.Events.Last();
        Assert.Equal(3, TracingFixtures.EventTag(exhausted, "dexpace.retry.attempts"));
        Assert.Equal(TracingFixtures.EventTag(exception, "exception.type"), TracingFixtures.EventTag(exhausted, "error.type"));
    }

    [Fact]
    public void Fail_without_an_exhaustion_record_emits_only_the_exception_event()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext();

        OperationTelemetry.Fail(operation, new ServiceResponseException("unreadable"), context.State);

        Assert.Equal(["exception"], TracingFixtures.EventNames(operation));
    }

    [Fact]
    public void Exhaustion_on_an_earlier_hop_does_not_leak_into_a_later_one()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext();

        OperationTelemetry.RetriesExhausted(context, 3);
        OperationTelemetry.RetrySequenceStarted(context);
        OperationTelemetry.Fail(operation, new ServiceResponseException("unreadable"), context.State);

        Assert.Equal(["exception"], TracingFixtures.EventNames(operation));
    }

    [Fact]
    public void RedirectHop_carries_hop_status_the_redacted_target_and_cross_origin()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext();

        OperationTelemetry.RedirectHop(context, 1, 302, new Uri("https://other.example.com/next?token=SECRET&api-version=2"), crossOrigin: true);

        var hop = Assert.Single(operation.Events);
        Assert.Equal("dexpace.redirect.hop", hop.Name);
        Assert.Equal(1, TracingFixtures.EventTag(hop, "dexpace.redirect.hop"));
        Assert.Equal(302, TracingFixtures.EventTag(hop, "http.response.status_code"));
        Assert.Equal("https://other.example.com/next?token=***&api-version=2", TracingFixtures.EventTag(hop, "url.full"));
        Assert.Equal(true, TracingFixtures.EventTag(hop, "dexpace.redirect.cross_origin"));
    }

    [Fact]
    public void RedirectHop_redacts_the_target()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var (operation, context) = TracingFixtures.TracedContext();

        OperationTelemetry.RedirectHop(context, 2, 307, new Uri("https://user:pass@other.example.com/next?sig=secret"), crossOrigin: false);

        var rendered = string.Join('\n', operation.Events.SelectMany(e => e.Tags).Select(t => t.Value?.ToString()));
        Assert.DoesNotContain("secret", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("pass", rendered, StringComparison.Ordinal);
    }
}
