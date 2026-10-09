// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Threading;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// The logging-context rows of chapter 18 over <c>AsAsync</c> (ASYNC-8 to ASYNC-12; appendix B.7.3): the caller's logging scope
/// reaches the blocking call on whichever thread runs it, the worker's own scope survives the call, the scope is captured per
/// call and not when the bridge is built, and a caller with no scope sees none and nothing fails. On .NET the carrier is the
/// <c>ExecutionContext</c> (a <see cref="LoggerExternalScopeProvider"/> keeps its scopes in an <c>AsyncLocal</c>), which
/// <c>Task.Factory.StartNew</c> flows to the worker, so these pins hold the bridge to not breaking it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BridgeLoggingContextTests
{
    private static Request NewRequest() => Request.Get("https://api.example.com/v1/items");

    private static string[] ScopesVisibleTo(LoggerExternalScopeProvider provider)
    {
        var seen = new List<string>();
        provider.ForEachScope((scope, state) => state.Add((string)scope!), seen);
        return [.. seen];
    }

    // A blocking client that records the scopes visible on the thread it runs on.
    private sealed class ScopeRecordingTransport(LoggerExternalScopeProvider provider) : IHttpClient
    {
        private readonly object _gate = new();

        public List<string[]> Seen { get; } = [];

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            var visible = ScopesVisibleTo(provider);
            lock (_gate)
            {
                Seen.Add(visible);
            }

            return TestResponses.Create(Status.Ok, request);
        }

        public void Dispose()
        {
        }
    }

    // One dedicated worker thread, like a custom executor with its own ambient state: the thread sets an AsyncLocal once, runs
    // each queued task, and records what is ambient on it after every task, outside any task's captured context.
    private sealed class WorkerWithAmbientScope : TaskScheduler, IDisposable
    {
        private readonly AsyncLocal<string?> _ambient = new();
        private readonly System.Collections.Concurrent.BlockingCollection<Task> _queue = [];
        private readonly Thread _thread;
        private readonly List<string?> _afterEachTask = [];

        public WorkerWithAmbientScope()
        {
            _thread = new Thread(() =>
            {
                _ambient.Value = "worker-scope";
                foreach (var task in _queue.GetConsumingEnumerable())
                {
                    TryExecuteTask(task);
                    lock (_afterEachTask)
                    {
                        _afterEachTask.Add(_ambient.Value);
                    }
                }
            })
            {
                IsBackground = true,
            };
            _thread.Start();
        }

        public IReadOnlyList<string?> AmbientAfterEachTask
        {
            get
            {
                lock (_afterEachTask)
                {
                    return [.. _afterEachTask];
                }
            }
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            _thread.Join(TimeSpan.FromSeconds(10));
        }

        protected override void QueueTask(Task task) => _queue.Add(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        protected override IEnumerable<Task> GetScheduledTasks() => [];
    }

    [Fact]
    public async Task A_logging_scope_opened_on_the_caller_is_visible_inside_the_blocking_Execute()
    {
        // ASYNC-8, ASYNC-12: on the default scheduler (a dedicated LongRunning thread) and on a scheduler that starts a new
        // thread per task (the thread-creating case ASYNC-12 names), the caller's scope is visible to the blocking call.
        foreach (var useDefault in new[] { true, false })
        {
            var provider = new LoggerExternalScopeProvider();
            using var recording = new RecordingTaskScheduler();
            var transport = new ScopeRecordingTransport(provider);
            await using var bridge = transport.AsAsync(useDefault ? TaskScheduler.Default : recording);

            using (provider.Push("caller"))
            {
                using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
            }

            Assert.Equal(["caller"], Assert.Single(transport.Seen));
        }
    }

    [Fact]
    public async Task The_workers_own_scope_is_intact_after_a_call_returns_and_after_one_throws()
    {
        // ASYNC-9: save, install, restore, including when the call throws: what is ambient on the worker thread is what it was.
        using var worker = new WorkerWithAmbientScope();
        var provider = new LoggerExternalScopeProvider();
        var attempts = 0;
        using var transport = new FuncSyncTransport((request, _, _) =>
            Interlocked.Increment(ref attempts) == 1 ? TestResponses.Create(Status.Ok, request) : throw new InvalidOperationException("transport failed"));
        await using var bridge = transport.AsAsync(worker);

        using (provider.Push("caller"))
        {
            using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken));
        }

        Assert.Equal(["worker-scope", "worker-scope"], worker.AmbientAfterEachTask);
    }

    [Fact]
    public async Task Capture_happens_per_call_not_at_AsAsync_construction()
    {
        // ASYNC-10: one bridge, built under scope A, called under B and then under C: the blocking client sees B, then C.
        var provider = new LoggerExternalScopeProvider();
        var transport = new ScopeRecordingTransport(provider);
        IAsyncHttpClient bridge;
        using (provider.Push("A"))
        {
            bridge = transport.AsAsync(TaskScheduler.Default);
        }

        await using (bridge)
        {
            foreach (var scope in new[] { "B", "C" })
            {
                using (provider.Push(scope))
                {
                    using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
                }
            }
        }

        Assert.Equal([["B"], ["C"]], transport.Seen);
    }

    [Fact]
    public async Task With_no_scope_provider_a_round_trip_raises_nothing_and_sees_no_scope()
    {
        // ASYNC-11: no logging backend, no scope: the round trip completes and the worker sees nothing ambient.
        var provider = new LoggerExternalScopeProvider();
        var transport = new ScopeRecordingTransport(provider);
        await using var bridge = transport.AsAsync(TaskScheduler.Default);

        using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Empty(Assert.Single(transport.Seen));
    }

    private sealed class FuncSyncTransport(Func<Request, RequestOptions, CancellationToken, Response> execute) : IHttpClient
    {
        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            execute(request, options, cancellationToken);

        public void Dispose()
        {
        }
    }
}
