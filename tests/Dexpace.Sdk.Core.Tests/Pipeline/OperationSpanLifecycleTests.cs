// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Diagnostics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>OBS-25, OBS-26, OBS-28, OBS-29, CTX-14, CTX-15: the operation span and the bundle it populates (P5c-2, P5c-3).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class OperationSpanLifecycleTests
{
    private static Request Get() => Request.Get("https://api.example.com/v1/items");

    private static readonly Func<Response> s_serviceUnavailable = () => TestResponses.Create(Status.ServiceUnavailable);

    private static Activity Operation(ActivityRecorder recorder) => Assert.Single(recorder.StartedOfKind(ActivityKind.Internal));

    [Fact]
    public async Task A_succeeding_call_ends_one_operation_span_without_error()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport());

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        var operation = Operation(recorder);
        Assert.Equal("GET", operation.DisplayName);
        Assert.NotEqual(ActivityStatusCode.Error, operation.Status);
        Assert.Equal(200, operation.GetTagItem("http.response.status_code"));
        Assert.Single(recorder.Stopped, a => a.Kind == ActivityKind.Internal);
        Assert.Empty(operation.Events);
    }

    [Fact]
    public async Task A_returned_4xx_without_error_mapping_is_a_successful_operation()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy()).Build(new RecordingTransport(_ => TestResponses.Create(Status.NotFound)));

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.NotEqual(ActivityStatusCode.Error, Operation(recorder).Status);
        Assert.Equal(ActivityStatusCode.Error, Assert.Single(recorder.StartedOfKind(ActivityKind.Client)).Status);
    }

    [Fact]
    public async Task A_returned_4xx_on_the_default_pipeline_is_a_successful_operation()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var pipeline = DexpacePipeline.CreateDefault(new RecordingTransport(_ => TestResponses.Create(Status.NotFound)));

        // The default pipeline does not contain ErrorMappingPolicy: the 404 is returned.
        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.NotFound, response.Status);
        Assert.NotEqual(ActivityStatusCode.Error, Operation(recorder).Status);
        Assert.Equal(ActivityStatusCode.Error, Assert.Single(recorder.StartedOfKind(ActivityKind.Client)).Status);
    }

    [Fact]
    public async Task A_mapped_4xx_fails_the_operation()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(_ => TestResponses.Create(Status.NotFound)), mapped: true);

        await Assert.ThrowsAsync<HttpResponseException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        var operation = Operation(recorder);
        Assert.Equal(ActivityStatusCode.Error, operation.Status);
        Assert.Contains("exception", TracingFixtures.EventNames(operation));
        Assert.Equal(ActivityStatusCode.Error, Assert.Single(recorder.StartedOfKind(ActivityKind.Client)).Status);
    }

    [Fact]
    public async Task A_retry_exhausted_call_ends_with_exhausted_then_exception_carrying_the_same_type()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(_ => throw new ServiceRequestException("never sent")), retries: 2);

        await Assert.ThrowsAsync<ServiceRequestException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        var operation = Operation(recorder);
        Assert.Equal(
            ["dexpace.attempt.failed", "dexpace.attempt.failed", "dexpace.retry.exhausted", "exception"],
            TracingFixtures.EventNames(operation));
        var exhausted = TracingFixtures.Event(operation, "dexpace.retry.exhausted");
        var exception = TracingFixtures.Event(operation, "exception");
        Assert.Equal(3, TracingFixtures.EventTag(exhausted, "dexpace.retry.attempts"));
        Assert.Equal(typeof(ServiceRequestException).FullName, TracingFixtures.EventTag(exhausted, "error.type"));
        Assert.Equal(TracingFixtures.EventTag(exhausted, "error.type"), TracingFixtures.EventTag(exception, "exception.type"));
        Assert.Equal(ActivityStatusCode.Error, operation.Status);
        Assert.Equal(3, recorder.StartedOfKind(ActivityKind.Client).Count);

        // The listener sees the attempts end before the operation, and the operation end once.
        Assert.Equal(
            [ActivityKind.Client, ActivityKind.Client, ActivityKind.Client, ActivityKind.Internal],
            recorder.Stopped.Select(a => a.Kind));
    }

    [Fact]
    public async Task A_returned_503_without_error_mapping_emits_no_exhausted_event()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(
            new ScriptedTransport(s_serviceUnavailable, s_serviceUnavailable, s_serviceUnavailable),
            retries: 2);

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        var operation = Operation(recorder);
        Assert.Equal(Status.ServiceUnavailable, response.Status);
        Assert.Equal(["dexpace.attempt.failed", "dexpace.attempt.failed"], TracingFixtures.EventNames(operation));
        Assert.NotEqual(ActivityStatusCode.Error, operation.Status);
    }

    [Fact]
    public async Task A_returned_503_with_error_mapping_emits_exhausted_then_exception()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(
            new ScriptedTransport(s_serviceUnavailable, s_serviceUnavailable, s_serviceUnavailable),
            mapped: true,
            retries: 2);

        await Assert.ThrowsAsync<HttpResponseException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        var operation = Operation(recorder);
        Assert.Equal(["dexpace.retry.exhausted", "exception"], TracingFixtures.EventNames(operation).TakeLast(2));
        Assert.Equal(typeof(HttpResponseException).FullName, TracingFixtures.EventTag(TracingFixtures.Event(operation, "dexpace.retry.exhausted"), "error.type"));
        Assert.Equal(typeof(HttpResponseException).FullName, TracingFixtures.EventTag(TracingFixtures.Event(operation, "exception"), "exception.type"));
    }

    [Fact]
    public async Task A_returned_503_on_the_default_pipeline_emits_no_exhausted_event()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var pipeline = DexpacePipeline.CreateDefault(
            new ScriptedTransport(s_serviceUnavailable, s_serviceUnavailable, s_serviceUnavailable, s_serviceUnavailable),
            timeProvider: new Dexpace.Sdk.TestSupport.Time.InstantTimeProvider());
        var options = new DexpaceClientOptions { Retry = new RetryOptions { MaxRetryAttempts = 2, BaseDelay = TimeSpan.FromMilliseconds(1) } };

        using var response = await pipeline.SendAsync(Get(), options, TestContext.Current.CancellationToken);

        Assert.Equal(Status.ServiceUnavailable, response.Status);
        Assert.DoesNotContain("dexpace.retry.exhausted", TracingFixtures.EventNames(Operation(recorder)));
    }

    [Fact]
    public async Task A_deadline_cancellation_after_exhaustion_does_not_pair_the_wrong_exception()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var builder = new PipelineBuilder().AddStandardResilience(new Dexpace.Sdk.TestSupport.Time.InstantTimeProvider());
        builder.Add(new DelegatePolicy(PipelineStage.PerCall, async (request, context, next) =>
        {
            using var response = await next.RunAsync(request, context).ConfigureAwait(false);
            throw new OperationCanceledException("deadline");
        }));
        var pipeline = builder.Build(
            new ScriptedTransport(s_serviceUnavailable, s_serviceUnavailable, s_serviceUnavailable),
            new DexpaceClientOptions { Retry = new RetryOptions { MaxRetryAttempts = 2, BaseDelay = TimeSpan.FromMilliseconds(1) } });

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        var operation = Operation(recorder);
        Assert.Equal(typeof(OperationCanceledException).FullName, TracingFixtures.EventTag(TracingFixtures.Event(operation, "dexpace.retry.exhausted"), "error.type"));
        Assert.Equal(["dexpace.retry.exhausted", "exception"], TracingFixtures.EventNames(operation).TakeLast(2));
    }

    [Fact]
    public async Task Attempt_spans_are_children_of_the_operation_span()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new ScriptedTransport(s_serviceUnavailable, () => TestResponses.Create(Status.Ok)));

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        var operation = Operation(recorder);
        var attempts = recorder.StartedOfKind(ActivityKind.Client);
        Assert.Equal(2, attempts.Count);
        Assert.All(attempts, a =>
        {
            Assert.Equal(operation.Id, a.ParentId);
            Assert.Equal(operation.TraceId, a.TraceId);
        });
        Assert.Equal(recorder.Root!.Id, operation.ParentId);
    }

    [Fact]
    public async Task An_attempt_span_exists_only_under_an_operation_span()
    {
        var started = new List<ActivityKind>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dexpace.Sdk",

            // Parent-based sampling in miniature: the operation span is dropped, everything else would be kept.
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                options.Kind == ActivityKind.Internal ? ActivitySamplingResult.None : ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => started.Add(activity.Kind),
        };
        ActivitySource.AddActivityListener(listener);
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport());

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Empty(started);
    }

    [Fact]
    public async Task A_fatal_exception_ends_the_operation_span_in_error_without_an_exception_event()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
#pragma warning disable CA2201 // The point: the runtime-reserved fatal type must pass through every SDK frame.
        var fatal = new OutOfMemoryException("fatal");
#pragma warning restore CA2201
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(_ => throw fatal));

        var thrown = await Assert.ThrowsAsync<OutOfMemoryException>(async () => await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken));

        Assert.Same(fatal, thrown);
        var operation = Operation(recorder);
        Assert.Equal(ActivityStatusCode.Error, operation.Status);
        Assert.Null(operation.StatusDescription);
        Assert.Empty(operation.Events);
        Assert.Single(recorder.Stopped, a => a.Kind == ActivityKind.Internal);
    }

    [Fact]
    public void Send_of_T_ends_the_operation_span_before_the_handler_runs()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport());
        var stoppedAtHandler = -1;

        var result = pipeline.Send(
            Get(),
            response =>
            {
                stoppedAtHandler = recorder.Stopped.Count(a => a.Kind == ActivityKind.Internal);
                return response.Status.Code;
            },
            RequestOptions.Empty,
            TestContext.Current.CancellationToken);

        Assert.Equal(200, result);
        Assert.Equal(1, stoppedAtHandler);
    }

    [Fact]
    public async Task The_operation_span_exists_for_every_pipeline_shape()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var empty = DexpacePipeline.CreateEmpty(new RecordingTransport());
        var custom = new PipelineBuilder()
            .Add(new DelegatePolicy(PipelineStage.PerCall, (request, context, next) => next.RunAsync(request, context)))
            .Build(new RecordingTransport());

        using var first = await empty.SendAsync(Get(), TestContext.Current.CancellationToken);
        Assert.Single(recorder.StartedOfKind(ActivityKind.Internal));
        using var second = await custom.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(2, recorder.StartedOfKind(ActivityKind.Internal).Count);
    }

    [Fact]
    public async Task A_traced_call_bundle_is_the_operation_span()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        InstrumentationContext? bundle = null;
        CallKey key = default;
        var pipeline = new PipelineBuilder()
            .Add(new DelegatePolicy(PipelineStage.PerCall, (request, context, next) =>
            {
                bundle = context.Instrumentation;
                key = context.CallKey;
                return next.RunAsync(request, context);
            }))
            .Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        var operation = Operation(recorder);
        Assert.NotNull(bundle);
        Assert.Same(operation, bundle.ActiveSpan);
        Assert.Equal(operation.TraceId, bundle.TraceId);
        Assert.Equal(operation.SpanId, bundle.SpanId);
        Assert.Equal(ActivityIdFormat.W3C, bundle.TraceIdFormat);
        Assert.True(bundle.IsValid);
        Assert.Equal(operation.TraceId, key.TraceId);
        Assert.Equal(operation.SpanId, key.SpanId);
    }

    [Fact]
    public async Task An_untraced_call_bundle_is_None_even_under_an_ambient_activity()
    {
        InstrumentationContext? bundle = null;
        CallKey key = default;
        Activity? currentInside = null;
        var pipeline = new PipelineBuilder()
            .Add(new DelegatePolicy(PipelineStage.PerCall, (request, context, next) =>
            {
                bundle = context.Instrumentation;
                key = context.CallKey;
                currentInside = Activity.Current;
                return next.RunAsync(request, context);
            }))
            .Build(new RecordingTransport());
        using var ambient = new Activity("ambient");
        ambient.Start();

        try
        {
            using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

            Assert.Same(InstrumentationContext.None, bundle);
            Assert.Equal(default, key.TraceId);
            Assert.Same(ambient, currentInside);
            Assert.Same(ambient, Activity.Current);
        }
        finally
        {
            ambient.Stop();
        }
    }

    [Fact]
    public async Task A_traced_bundle_has_W3C_ids_and_is_valid()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        InstrumentationContext? bundle = null;
        var pipeline = new PipelineBuilder()
            .Add(new DelegatePolicy(PipelineStage.PerCall, (request, context, next) =>
            {
                bundle = context.Instrumentation;
                return next.RunAsync(request, context);
            }))
            .Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.NotNull(bundle);
        Assert.Matches("^[0-9a-f]{32}$", bundle.TraceId.ToHexString());
        Assert.Matches("^[0-9a-f]{16}$", bundle.SpanId.ToHexString());
        Assert.True(bundle.IsValid);
    }

    [Fact]
    public async Task The_dispatch_context_is_not_re_keyed()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var keys = new List<CallKey>();
        var pipeline = new PipelineBuilder()
            .Add(new DelegatePolicy(PipelineStage.PerCall, (request, context, next) =>
            {
                keys.Add(context.CallKey);
                return next.RunAsync(request, context);
            }))
            .Add(new DelegatePolicy(PipelineStage.Serde, (request, context, next) =>
            {
                keys.Add(context.CallKey);
                return next.RunAsync(request, context);
            }))
            .Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(2, keys.Count);
        Assert.Equal(keys[0], keys[1]);
        Assert.Equal(Operation(recorder).TraceId, keys[0].TraceId);
    }
}
