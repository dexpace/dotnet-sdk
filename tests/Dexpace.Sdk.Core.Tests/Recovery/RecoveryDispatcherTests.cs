// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/recovery/orchestrator.test.ts.
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

#pragma warning disable CA2201 // OutOfMemoryException is the fatal exception under test (design §5.2).
#pragma warning disable CA2000 // The fakes hold nothing to release; the dispatcher must not dispose them (P4b-25).

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class RecoveryDispatcherTests
{
    private static readonly Request s_request = Request.Get("https://example.test/");

    private static RecoveryDispatcher Dispatcher(
        IEnumerable<IRequestStep>? requestSteps = null,
        IEnumerable<IResponseStep>? responseSteps = null,
        IEnumerable<IRecoveryStep>? recoverySteps = null) =>
        new(new RequestRecoveryChain(requestSteps ?? []), new ResponseRecoveryChain(responseSteps ?? [], recoverySteps ?? []));

    /// <summary>Runs one dispatch over the transport form the flag selects; the responder serves both forms.</summary>
    private static async Task<(Task<Response> Result, Func<IReadOnlyList<RecordedCall>> Calls, Func<bool> Disposed)> Dispatch(
        RecoveryDispatcher dispatcher,
        bool async,
        Func<Request, Response>? respond = null,
        RequestOptions? options = null,
        CancellationToken? token = null)
    {
        options ??= RequestOptions.Empty;
        var ct = token ?? TestContext.Current.CancellationToken;
        if (async)
        {
            var transport = new RecordingTransport(respond);
            var task = dispatcher.DispatchAsync(transport, s_request, options, ct).AsTask();
            await Task.WhenAny(task);
            return (task, () => transport.Calls, () => transport.IsDisposed);
        }

        var sync = new RecordingSyncTransport(respond);
        var syncTask = Task.Run(() => dispatcher.Dispatch(sync, s_request, options, ct), TestContext.Current.CancellationToken);
        await Task.WhenAny(syncTask);
        return (syncTask, () => sync.Calls, () => sync.IsDisposed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_throwing_request_step_reaches_a_recording_recovery_step_as_a_failure(bool async)
    {
        var boom = new InvalidOperationException("request step");
        Outcome? seen = null;
        var dispatcher = Dispatcher(
            [new DelegateRequestStep((_, _) => throw boom)],
            recoverySteps: [new DelegateRecoveryStep((o, _) => seen = o)]);

        var (result, calls, _) = await Dispatch(dispatcher, async);

        Assert.Same(boom, Assert.IsType<Outcome.Failure>(seen).Error);
        Assert.Same(boom, await Assert.ThrowsAsync<InvalidOperationException>(() => result));
        Assert.Empty(calls());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_throwing_transport_reaches_a_recording_recovery_step_as_a_failure(bool async)
    {
        var boom = new IOException("transport");
        Outcome? seen = null;
        var dispatcher = Dispatcher(recoverySteps: [new DelegateRecoveryStep((o, _) => seen = o)]);

        var (result, _, _) = await Dispatch(dispatcher, async, _ => throw boom);

        Assert.Same(boom, Assert.IsType<Outcome.Failure>(seen).Error);
        Assert.Same(boom, await Assert.ThrowsAsync<IOException>(() => result));
    }

    [Fact]
    public async Task A_null_response_from_a_transport_is_a_failure_with_InvalidOperationException()
    {
        var dispatcher = Dispatcher();
        var transport = new NullTransport();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(transport, s_request, RequestOptions.Empty, TestContext.Current.CancellationToken).AsTask());

        Assert.Contains("null", thrown.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(
            () => dispatcher.Dispatch(transport, s_request, RequestOptions.Empty, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_success_returns_the_response_by_reference(bool async)
    {
        using var response = TestResponses.Create(Status.Ok);

        var (result, _, _) = await Dispatch(Dispatcher(), async, _ => response);

        Assert.Same(response, await result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_failure_from_a_recovery_step_is_rethrown_as_the_same_constructed_never_thrown_instance(bool async)
    {
        var constructed = new TimeoutException("constructed, never thrown");
        var dispatcher = Dispatcher(recoverySteps: [new DelegateRecoveryStep((_, _) => new Outcome.Failure(constructed))]);

        var (result, _, _) = await Dispatch(dispatcher, async);

        Assert.Same(constructed, await Assert.ThrowsAsync<TimeoutException>(() => result));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_failure_from_the_transport_is_rethrown_as_the_same_thrown_instance(bool async)
    {
        var thrown = new IOException("thrown by the transport");

        var (result, _, _) = await Dispatch(Dispatcher(), async, _ => throw thrown);

        Assert.Same(thrown, await Assert.ThrowsAsync<IOException>(() => result));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_recovery_step_may_substitute_a_success_for_a_failure(bool async)
    {
        using var substitute = TestResponses.Create(Status.Accepted);
        var dispatcher = Dispatcher(recoverySteps: [new DelegateRecoveryStep((_, _) => new Outcome.Success(substitute))]);

        var (result, _, _) = await Dispatch(dispatcher, async, _ => throw new IOException("replaced"));

        Assert.Same(substitute, await result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_survives_the_failure_conversion(bool async)
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var cancellation = new OperationCanceledException(cts.Token);
        Outcome? seen = null;
        var dispatcher = Dispatcher(recoverySteps: [new DelegateRecoveryStep((o, _) => seen = o)]);

        var (result, _, _) = await Dispatch(dispatcher, async, _ => throw cancellation, token: cts.Token);

        Assert.Same(cancellation, Assert.IsType<Outcome.Failure>(seen).Error);
        Assert.True(cts.Token.IsCancellationRequested);
        Assert.Same(cancellation, await Assert.ThrowsAsync<OperationCanceledException>(() => result));
        Assert.True(cts.Token.IsCancellationRequested);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_dispatcher_does_not_dispose_the_transport(bool async)
    {
        using var response = TestResponses.Create(Status.Ok);

        var (result, _, disposed) = await Dispatch(Dispatcher(), async, _ => response);
        await result;

        Assert.False(disposed());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_request_options_and_token_reach_the_transport(bool async)
    {
        using var cts = new CancellationTokenSource();
        var options = new RequestOptions();
        using var response = TestResponses.Create(Status.Ok);

        var (result, calls, _) = await Dispatch(Dispatcher(), async, _ => response, options, cts.Token);
        await result;

        var call = Assert.Single(calls());
        Assert.Same(options, call.Options);
        Assert.Equal(cts.Token, call.CancellationToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_fatal_exception_propagates_without_conversion(bool async)
    {
        var seenByRecovery = new DelegateRecoveryStep((o, _) => o);
        var dispatcher = Dispatcher(recoverySteps: [seenByRecovery]);

        var (result, _, _) = await Dispatch(dispatcher, async, _ => throw new OutOfMemoryException());

        await Assert.ThrowsAsync<OutOfMemoryException>(() => result);
        Assert.Equal(0, seenByRecovery.CallCount);
    }

    [Fact]
    public async Task One_dispatcher_serves_both_transport_forms()
    {
        var dispatcher = Dispatcher();
        using var first = TestResponses.Create(Status.Ok);
        using var second = TestResponses.Create(Status.Accepted);

        var asyncResult = await dispatcher.DispatchAsync(
            new RecordingTransport(_ => first),
            s_request,
            RequestOptions.Empty,
            TestContext.Current.CancellationToken);
        var syncResult = dispatcher.Dispatch(
            new RecordingSyncTransport(_ => second),
            s_request,
            RequestOptions.Empty,
            TestContext.Current.CancellationToken);

        Assert.Same(first, asyncResult);
        Assert.Same(second, syncResult);
    }

    [Fact]
    public async Task Constructors_and_entry_points_reject_null()
    {
        Assert.Throws<ArgumentNullException>(() => new RecoveryDispatcher(null!, ResponseRecoveryChain.Empty));
        Assert.Throws<ArgumentNullException>(() => new RecoveryDispatcher(RequestRecoveryChain.Empty, null!));
        var dispatcher = Dispatcher();
        Assert.Throws<ArgumentNullException>(
            () => dispatcher.Dispatch(null!, s_request, RequestOptions.Empty, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await dispatcher.DispatchAsync(null!, s_request, RequestOptions.Empty, TestContext.Current.CancellationToken));
    }

    private sealed class NullTransport : IAsyncHttpClient, IHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            Task.FromResult<Response>(null!);

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) => null!;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
