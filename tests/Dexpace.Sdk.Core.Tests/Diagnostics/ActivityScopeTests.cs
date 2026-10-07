// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-21, OBS-22, OBS-23, OBS-30: span scope, log correlation and concurrency (design 5c position C and G).</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class ActivityScopeTests
{
    private static readonly DexpaceClientOptions s_headers = new() { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers } };

    private static Request Get() => Request.Get("https://api.example.com/v1/items");

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Activity_Current_is_restored_after_the_call_including_on_throw(bool isAsync, bool succeeds)
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(_ => succeeds
            ? TestResponses.Create(Status.Ok)
            : throw new InvalidOperationException("boom")));
        var token = TestContext.Current.CancellationToken;

        if (succeeds)
        {
            using var response = isAsync ? await pipeline.SendAsync(Get(), token) : pipeline.Send(Get(), token);
        }
        else if (isAsync)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipeline.SendAsync(Get(), token));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => pipeline.Send(Get(), token));
        }

        Assert.Same(recorder.Root, Activity.Current);
    }

    [Fact]
    public async Task The_attempt_span_is_Activity_Current_inside_the_transport()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        Activity? inside = null;
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(_ =>
        {
            inside = Activity.Current;
            return TestResponses.Create(Status.Ok);
        }));

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Same(Assert.Single(recorder.StartedOfKind(ActivityKind.Client)), inside);
    }

    [Fact]
    public async Task The_operation_span_is_Activity_Current_in_a_PerCall_policy()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        Activity? inside = null;
        var pipeline = new PipelineBuilder()
            .Add(new DelegatePolicy(PipelineStage.PerCall, (request, context, next) =>
            {
                inside = Activity.Current;
                return next.RunAsync(request, context);
            }))
            .Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Same(Assert.Single(recorder.StartedOfKind(ActivityKind.Internal)), inside);
    }

    [Fact]
    public async Task The_attempt_span_is_current_at_the_log_site()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var logger = new RecordingLogger();
        var succeeding = TracingFixtures.Pipeline(new RecordingTransport(), logger: logger);

        using var response = await succeeding.SendAsync(Get(), s_headers, TestContext.Current.CancellationToken);

        var attempt = Assert.Single(recorder.StartedOfKind(ActivityKind.Client));
        Assert.Same(attempt, logger.Entries.Single(e => e.EventId.Id == 100).Activity);
        Assert.Same(attempt, logger.Entries.Single(e => e.EventId.Id == 101).Activity);

        var failingLogger = new RecordingLogger();
        var failing = TracingFixtures.Pipeline(new RecordingTransport(_ => throw new InvalidOperationException("boom")), logger: failingLogger);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await failing.SendAsync(Get(), s_headers, TestContext.Current.CancellationToken));

        var failedAttempt = recorder.StartedOfKind(ActivityKind.Client)[^1];
        Assert.Same(failedAttempt, failingLogger.Entries.Single(e => e.EventId.Id == 102).Activity);
    }

    [Fact]
    public async Task An_untraced_call_leaves_the_callers_Activity_Current_untouched_and_log_sites_see_it()
    {
        var logger = new RecordingLogger();
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport(), logger: logger);
        using var ambient = new Activity("ambient");
        ambient.Start();

        try
        {
            using var response = await pipeline.SendAsync(Get(), s_headers, TestContext.Current.CancellationToken);

            Assert.Same(ambient, Activity.Current);
            Assert.NotEmpty(logger.Entries);
            Assert.All(logger.Entries, e => Assert.Same(ambient, e.Activity));
        }
        finally
        {
            ambient.Stop();
        }
    }

    [Fact]
    public async Task Activity_Current_survives_a_thread_hop_in_the_transport()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var transport = new HoppingTransport();
        var pipeline = TracingFixtures.Pipeline(transport);

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Same(Assert.Single(recorder.StartedOfKind(ActivityKind.Client)), transport.Seen);
        Assert.Same(recorder.Root, Activity.Current);
    }

    [Fact]
    public async Task Sdk_writes_to_a_non_recording_span_are_skipped()
    {
        using var listener = TracingFixtures.NonRecordingListener();
        var started = new List<Activity>();
        listener.ActivityStarted = started.Add;
        var pipeline = TracingFixtures.Pipeline(new ScriptedTransport(
            () => TestResponses.Create(Status.ServiceUnavailable),
            () => TestResponses.Create(Status.Ok)));

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(2, started.Count(a => a.Kind == ActivityKind.Client));
        Assert.Single(started, a => a.Kind == ActivityKind.Internal);
        Assert.All(started, span =>
        {
            Assert.False(span.IsAllDataRequested);
            Assert.Empty(span.TagObjects);
            Assert.Empty(span.Events);
            Assert.Equal(ActivityStatusCode.Unset, span.Status);
        });
    }

    [Fact]
    public async Task Stop_is_idempotent()
    {
        var stops = 0;
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        using var counter = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dexpace.Sdk",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = _ => Interlocked.Increment(ref stops),
        };
        ActivitySource.AddActivityListener(counter);
        var pipeline = TracingFixtures.Pipeline(new RecordingTransport());

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);
        foreach (var span in recorder.Started)
        {
            span.Stop();
        }

        // One operation span and one attempt span, each stopped once.
        Assert.Equal(2, Volatile.Read(ref stops));
    }

    [Fact]
    public async Task Concurrent_calls_keep_their_spans_in_their_own_traces()
    {
        using var recorder = new ActivityRecorder("Dexpace.Sdk");
        var hosts = Enumerable.Range(0, 64).Select(_ => TestHosts.Unique()).ToArray();

        var keyed = TracingFixtures.Pipeline(new RecordingTransport());
        await Task.WhenAll(hosts.Select(host => Task.Run(async () =>
        {
            Activity.Current = null;
            using var response = await keyed.SendAsync(Request.Get($"https://{host}/"), TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken)));

        var mine = recorder.Started.Where(a => hosts.Contains((string?)a.GetTagItem("server.address"))).ToList();
        var operations = mine.Where(a => a.Kind == ActivityKind.Internal).ToList();
        var attempts = mine.Where(a => a.Kind == ActivityKind.Client).ToList();
        Assert.Equal(64, operations.Count);
        Assert.Equal(64, attempts.Count);
        Assert.Equal(64, operations.Select(o => o.TraceId).Distinct().Count());
        Assert.All(attempts, attempt =>
        {
            var operation = Assert.Single(operations, o => o.Id == attempt.ParentId);
            Assert.Equal(operation.TraceId, attempt.TraceId);
            Assert.Equal(operation.GetTagItem("server.address"), attempt.GetTagItem("server.address"));
        });
    }

    private sealed class HoppingTransport : IAsyncHttpClient
    {
        public Activity? Seen { get; private set; }

        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Seen = Activity.Current;
            return TestResponses.Create(Status.Ok);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
