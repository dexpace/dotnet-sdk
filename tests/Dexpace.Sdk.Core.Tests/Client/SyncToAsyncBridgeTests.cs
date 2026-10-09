// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Reflection;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Threading;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// <c>AsAsync</c> (SEAM-11, SEAM-13, SEAM-14, SEAM-16, SEAM-18, SEAM-24, SEAM-25, SEAM-30; design §3.3, §5.3; plan
/// rulings 2 and 3): the caller's scheduler, options and token threaded through, the check after return, and no disposal
/// of what it wraps.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SyncToAsyncBridgeTests
{
    private static Request NewRequest() => Request.Get("https://api.example.com/v1/items");

    private static async Task<Task> CompletesOrTimesOut(Task task)
    {
        await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(task.IsCompleted, "the bridged task did not complete within 10 seconds");
        return task;
    }

    private sealed class FuncSyncTransport(Func<Request, RequestOptions, CancellationToken, Response> execute) : IHttpClient
    {
        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            execute(request, options, cancellationToken);

        public void Dispose()
        {
        }
    }

    private sealed class ThrowingDisposeBody : ResponseBody
    {
        public override Dexpace.Sdk.Core.Http.Common.MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            throw new InvalidOperationException("dispose failed");
        }
    }

    [Fact]
    public void No_AsAsync_overload_lacks_a_TaskScheduler()
    {
        var overloads = typeof(HttpClientExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(HttpClientExtensions.AsAsync))
            .ToArray();

        Assert.NotEmpty(overloads);
        Assert.All(overloads, m => Assert.Contains(m.GetParameters(), p => p.ParameterType == typeof(TaskScheduler)));
    }

    [Fact]
    public void A_null_scheduler_throws_ArgumentNullException()
    {
        using var transport = new RecordingSyncTransport();

        Assert.Equal("scheduler", Assert.Throws<ArgumentNullException>(() => transport.AsAsync(null!)).ParamName);
    }

    [Fact]
    public void A_null_client_throws_ArgumentNullException()
    {
        IHttpClient client = null!;

        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => client.AsAsync(TaskScheduler.Default)).ParamName);
    }

    [Fact]
    public async Task The_call_runs_on_the_given_scheduler()
    {
        using var scheduler = new RecordingTaskScheduler();
        var threadId = 0;
        using var transport = new RecordingSyncTransport(_ =>
        {
            threadId = Environment.CurrentManagedThreadId;
            return TestResponses.Create(Status.Ok);
        });
        await using var bridge = transport.AsAsync(scheduler);

        using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(1, scheduler.QueueCount);
        Assert.Contains(threadId, scheduler.ThreadIds);
        Assert.NotEqual(Environment.CurrentManagedThreadId, threadId);
    }

    [Fact]
    public async Task TaskScheduler_Default_is_accepted()
    {
        // Ruling 2: accepted, and LongRunning gives the blocking call a dedicated, non-pool thread (design fact 4).
        var onPoolThread = true;
        using var transport = new RecordingSyncTransport(_ =>
        {
            onPoolThread = Thread.CurrentThread.IsThreadPoolThread;
            return TestResponses.Create(Status.Ok);
        });
        await using var bridge = transport.AsAsync(TaskScheduler.Default);

        using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.False(onPoolThread);
    }

    [Fact]
    public async Task The_exact_options_and_token_instances_reach_Execute()
    {
        using var scheduler = new RecordingTaskScheduler();
        using var transport = new RecordingSyncTransport();
        await using var bridge = transport.AsAsync(scheduler);
        using var cts = new CancellationTokenSource();
        var request = NewRequest();
        var options = new RequestOptions { MaxRetries = 4 };

        using var response = await bridge.ExecuteAsync(request, options, cts.Token);

        var call = Assert.IsType<RecordedCall>(transport.LastCall);
        Assert.Same(request, call.Request);
        Assert.Same(options, call.Options);
        Assert.Equal(cts.Token, call.CancellationToken);
    }

    [Fact]
    public async Task The_callers_AsyncLocal_and_Activity_reach_the_worker_thread()
    {
        // SEAM-24: ExecutionContext flows through StartNew on any scheduler, so no code is needed to carry it.
        using var scheduler = new RecordingTaskScheduler();
        var local = new AsyncLocal<string?> { Value = "caller-value" };
        using var activity = new Activity("seam-24");
        activity.Start();
        string? seenLocal = null;
        Activity? seenActivity = null;
        using var transport = new RecordingSyncTransport(_ =>
        {
            seenLocal = local.Value;
            seenActivity = Activity.Current;
            return TestResponses.Create(Status.Ok);
        });
        await using var bridge = transport.AsAsync(scheduler);

        using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);

        Assert.Equal("caller-value", seenLocal);
        Assert.Same(activity, seenActivity);
    }

    [Fact]
    public async Task A_transport_exception_faults_the_task_with_the_original_exception()
    {
        var failure = new InvalidOperationException("transport failed");
        using var scheduler = new RecordingTaskScheduler();
        using var transport = new RecordingSyncTransport(_ => throw failure);
        await using var bridge = transport.AsAsync(scheduler);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task A_null_result_from_the_wrapped_client_faults_with_InvalidOperationException()
    {
        using var scheduler = new RecordingTaskScheduler();
        using var transport = new FuncSyncTransport((_, _, _) => null!);
        await using var bridge = transport.AsAsync(scheduler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Argument_errors_are_delivered_through_the_returned_task_not_thrown_synchronously()
    {
        // ASYNC-2.
        using var scheduler = new RecordingTaskScheduler();
        using var transport = new RecordingSyncTransport();
        await using var bridge = transport.AsAsync(scheduler);

        var nullRequest = bridge.ExecuteAsync(null!, RequestOptions.Empty, TestContext.Current.CancellationToken);
        var nullOptions = bridge.ExecuteAsync(NewRequest(), null!, TestContext.Current.CancellationToken);

        Assert.Equal("request", (await Assert.ThrowsAsync<ArgumentNullException>(() => nullRequest)).ParamName);
        Assert.Equal("options", (await Assert.ThrowsAsync<ArgumentNullException>(() => nullOptions)).ParamName);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task A_token_cancelled_before_the_send_never_runs_it()
    {
        // SEAM-30.
        using var scheduler = new RecordingTaskScheduler();
        using var transport = new RecordingSyncTransport();
        await using var bridge = transport.AsAsync(scheduler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var task = bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(task.IsCanceled);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task A_response_produced_after_cancellation_is_disposed_exactly_once_and_never_surfaces()
    {
        // Ruling 3, SEAM-30's conformance clause: the delegate starts, the test cancels, the delegate returns a response.
        using var scheduler = new RecordingTaskScheduler();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var body = new DisposalCountingBody();
        using var transport = new RecordingSyncTransport(request =>
        {
            started.Set();
            release.Wait();
            return TestResponses.Create(Status.Ok, request, body: body);
        });
        await using var bridge = transport.AsAsync(scheduler);
        using var cts = new CancellationTokenSource();

        var task = bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, cts.Token);
        Assert.True(started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        await cts.CancelAsync();

        // Design §5.3's "cancel without interruption": the caller stops awaiting while the worker runs on. Allowed in tests.
#pragma warning disable RS0030
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(cts.Token));
#pragma warning restore RS0030
        Assert.Equal(0, body.DisposeCount);

        release.Set();
        await CompletesOrTimesOut(task);

        Assert.True(task.IsCanceled);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_throwing_dispose_after_cancellation_cancels_the_task_and_reports_the_failure()
    {
        // P3b-16: replaces the 2b pin that faulted the task; the dispose is quiet and its failure is reported.
        using var source = new ActivitySource("Dexpace.Sdk.Core.Tests.Bridge");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var scope = source.StartActivity("call");
        using var scheduler = new RecordingTaskScheduler();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var transport = new RecordingSyncTransport(request =>
        {
            started.Set();
            release.Wait();
            return TestResponses.Create(Status.Ok, request, body: new ThrowingDisposeBody());
        });
        await using var bridge = transport.AsAsync(scheduler);
        using var cts = new CancellationTokenSource();

        var task = bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, cts.Token);
        Assert.True(started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        await cts.CancelAsync();
        release.Set();
        await CompletesOrTimesOut(task);

        Assert.True(task.IsCanceled);
        var reported = Assert.Single(scope!.Events);
        Assert.Equal("exception", reported.Name);
        var tags = reported.Tags.ToDictionary(t => t.Key, t => t.Value);
        Assert.Equal(true, tags["dexpace.dispose.suppressed"]);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["exception.type"]);
    }

    [Fact]
    public async Task Cancelling_after_delivery_leaves_the_response_readable()
    {
        // SEAM-16: the token is signalled only after the task completed; the response is intact and not disposed.
        using var scheduler = new RecordingTaskScheduler();
        using var body = new DisposalCountingBody();
        using var transport = new RecordingSyncTransport(request => TestResponses.Create(Status.Ok, request, body: body));
        await using var bridge = transport.AsAsync(scheduler);
        using var cts = new CancellationTokenSource();

        using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, cts.Token);
        await cts.CancelAsync();

        Assert.Equal(0, body.DisposeCount);
        await using var stream = await response.Body.OpenReadAsync(TestContext.Current.CancellationToken);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task AsAsync_aborts_a_token_honouring_client_and_a_token_ignoring_one_runs_on_but_its_call_still_completes_cancelled()
    {
        // ASYNC-7, SEAM-30: cancellation reaches a blocking call only as far as the blocking transport honours its token
        // (the AsAsync remark: "A response produced after the call's token is signalled is disposed and the call completes
        // cancelled"). A honouring client stops; an ignoring one runs to its end, observably, and its call still ends cancelled.
        using var scheduler = new RecordingTaskScheduler();
        using var honouringStarted = new ManualResetEventSlim();
        using var ignoringStarted = new ManualResetEventSlim();
        using var ignoringMayFinish = new ManualResetEventSlim();
        var honouringRanToTheEnd = false;
        var ignoringRanToTheEnd = false;
        using var honouring = new FuncSyncTransport((request, _, token) =>
        {
            honouringStarted.Set();
            token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
            token.ThrowIfCancellationRequested();
            honouringRanToTheEnd = true;
            return TestResponses.Create(Status.Ok, request);
        });
        using var body = new DisposalCountingBody();
        using var ignoring = new FuncSyncTransport((request, _, _) =>
        {
            ignoringStarted.Set();
            ignoringMayFinish.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
            ignoringRanToTheEnd = true;
            return TestResponses.Create(Status.Ok, request, body: body);
        });
        await using var honouringBridge = honouring.AsAsync(scheduler);
        await using var ignoringBridge = ignoring.AsAsync(scheduler);
        using var honouringCts = new CancellationTokenSource();
        using var ignoringCts = new CancellationTokenSource();

        var honouringTask = honouringBridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, honouringCts.Token);
        var ignoringTask = ignoringBridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, ignoringCts.Token);
        Assert.True(honouringStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(ignoringStarted.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        await honouringCts.CancelAsync();
        await ignoringCts.CancelAsync();
        ignoringMayFinish.Set();
        await CompletesOrTimesOut(honouringTask);
        await CompletesOrTimesOut(ignoringTask);

        Assert.True(honouringTask.IsCanceled);
        Assert.False(honouringRanToTheEnd);
        Assert.True(ignoringTask.IsCanceled);
        Assert.True(ignoringRanToTheEnd);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Disposing_the_bridge_disposes_neither_the_scheduler_nor_the_client()
    {
        // SEAM-25, SEAM-14.
        using var scheduler = new RecordingTaskScheduler();
        using var transport = new RecordingSyncTransport();
        var bridge = transport.AsAsync(scheduler);

        await bridge.DisposeAsync();
        await bridge.DisposeAsync();

        Assert.False(scheduler.IsDisposed);
        Assert.False(transport.IsDisposed);
    }

    [Fact]
    public async Task An_options_ignoring_client_behaves_identically_with_and_without_options()
    {
        // SEAM-11's conformance clause, over the bridge.
        using var scheduler = new RecordingTaskScheduler();
        using var transport = new RecordingSyncTransport(request => TestResponses.Create(Status.Accepted, request));
        await using var bridge = transport.AsAsync(scheduler);

        using var withEmpty = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
        using var withOptions = await bridge.ExecuteAsync(NewRequest(), new RequestOptions { Timeout = TimeSpan.FromSeconds(1) }, TestContext.Current.CancellationToken);

        Assert.Equal(withEmpty.Status, withOptions.Status);
    }
}
