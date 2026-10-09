// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Threading;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private sealed class ScopeRecordingTransport(LoggerExternalScopeProvider provider, ILogger? logger = null) : IHttpClient
    {
        private readonly object _gate = new();

        public List<string[]> Seen { get; } = [];

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            // With a logger the call also opens a scope and writes an entry through it, as a transport with a logging backend does.
            using var scope = logger?.BeginScope("transport");
            logger?.Log(LogLevel.Information, new EventId(1), "sending", null, static (state, _) => state);
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

    // One dedicated worker thread, like a custom executor with its own ambient state: the thread sets an AsyncLocal once (and,
    // given a provider, opens a logging scope on it, which is the same kind of ambient state), runs each queued task, and
    // records what is ambient on it after every task, outside any task's captured context. Dispose joins the thread, so what it
    // recorded is complete once Dispose has returned: read the records after it, never while a call's await may still be returning.
    private sealed class WorkerWithAmbientScope : TaskScheduler, IDisposable
    {
        private readonly AsyncLocal<string?> _ambient = new();
        private readonly System.Collections.Concurrent.BlockingCollection<Task> _queue = [];
        private readonly Thread _thread;
        private readonly List<string?> _afterEachTask = [];
        private readonly List<string[]> _scopesAfterEachTask = [];

        public WorkerWithAmbientScope(LoggerExternalScopeProvider? provider = null)
        {
            _thread = new Thread(() =>
            {
                _ambient.Value = "worker-scope";
                _ = provider?.Push("worker-scope");
                foreach (var task in _queue.GetConsumingEnumerable())
                {
                    TryExecuteTask(task);
                    lock (_afterEachTask)
                    {
                        _afterEachTask.Add(_ambient.Value);
                        _scopesAfterEachTask.Add(provider is null ? [] : ScopesVisibleTo(provider));
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

        public IReadOnlyList<string[]> ScopesAfterEachTask
        {
            get
            {
                lock (_afterEachTask)
                {
                    return [.. _scopesAfterEachTask];
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

        worker.Dispose();
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
    public async Task A_caller_with_no_scope_clears_the_workers_own_scope_for_the_call_and_the_worker_gets_it_back()
    {
        // ASYNC-11: "reinstating an empty context clears the target thread's context rather than raising an error". The worker
        // thread has a scope of its own and the caller has none: inside the call the worker's scope must not be visible (the
        // caller's empty context replaced it), and once the call is over it must be back (cleared for the call, not for good).
        var provider = new LoggerExternalScopeProvider();
        using var worker = new WorkerWithAmbientScope(provider);
        var transport = new ScopeRecordingTransport(provider);
        await using var bridge = transport.AsAsync(worker);

        using var response = await bridge.ExecuteAsync(NewRequest(), RequestOptions.Empty, TestContext.Current.CancellationToken);
        worker.Dispose();

        Assert.Equal(Status.Ok, response.Status);
        Assert.Empty(Assert.Single(transport.Seen));
        Assert.Equal([["worker-scope"]], worker.ScopesAfterEachTask);
    }

    [Fact]
    public async Task With_no_scope_provider_a_round_trip_raises_nothing_and_sees_no_scope()
    {
        // ASYNC-11: no logging backend at all (a NullLogger: BeginScope has nothing to open, Log has nothing to write) and no
        // scope on the caller: the round trip completes, nothing is raised, and the worker sees nothing ambient.
        var provider = new LoggerExternalScopeProvider();
        var transport = new ScopeRecordingTransport(provider, NullLogger.Instance);
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
