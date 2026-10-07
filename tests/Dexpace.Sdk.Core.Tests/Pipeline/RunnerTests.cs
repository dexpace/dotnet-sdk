// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/{cursor,runtime}.test.ts (R8).

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public class RunnerTests
{
    private static Request MakeRequest() =>
        Request.Get("https://api.example.com/v1/resource");

    [Fact]
    public async Task ExecutionOrder_PoliciesRunInStageOrderInAndReversedOut_TransportInvokedOnce()
    {
        var log = new List<string>();
        var transport = new RecordingTransport();

        var runner = TestContexts.RunnerOver(
            transport,
            new ProbePolicy("a", PipelineStage.Operation, log),
            new ProbePolicy("b", PipelineStage.PerAttempt, log));

        using var response = await runner.RunAsync(MakeRequest(), TestContexts.For());

        Assert.Equal(["a:in", "b:in", "b:out", "a:out"], log);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task Reentrancy_PolicyCallingNextTwice_TransportInvokedTwice()
    {
        var transport = new RecordingTransport();
        var runner = TestContexts.RunnerOver(transport, new ForkingProbe(PipelineStage.PerAttempt, drives: 2));

        using var response = await runner.RunAsync(MakeRequest(), TestContexts.For());

        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task Next_dispatches_to_the_transport_after_the_last_policy_with_the_callers_options()
    {
        // PIPE-13, PIPE-17: replaces The_transport_receives_RequestOptions_Empty (the 2b hand-off).
        var transport = new RecordingTransport();
        var options = new RequestOptions { MaxRetries = 2 };
        var request = MakeRequest();

        using var response = await TestContexts.RunnerOver(transport)
            .RunAsync(request, TestContexts.For(requestOptions: options));

        Assert.Same(options, transport.LastCall!.Options);
        Assert.Same(request, transport.LastCall!.Request);
    }

    [Fact]
    public void Run_dispatches_to_the_sync_transport()
    {
        using var transport = new RecordingSyncTransport();
        var terminalAsync = new PipelineTerminal(new SyncAdapterOverSync(transport));

        using var response = new PipelineRunner([], 0, terminalAsync).Run(MakeRequest(), TestContexts.For());

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task The_transport_receives_the_contexts_token()
    {
        using var cts = new CancellationTokenSource();
        var transport = new RecordingTransport();
        var context = TestContexts.For().WithCancellationToken(cts.Token);

        using var response = await TestContexts.RunnerOver(transport).RunAsync(MakeRequest(), context);

        Assert.Equal(cts.Token, transport.LastCall!.CancellationToken);
    }

    [Fact]
    public async Task A_null_from_the_transport_fails_with_PipelineAbortedException()
    {
        // SEAM-16: nullability is compile-time only, so the terminal runner asserts the transport's result before any
        // policy sees it.
        var seen = new List<Response?>();
        var observing = new DelegatePolicy(
            PipelineStage.Operation,
            async (request, context, next) =>
            {
                var response = await next.RunAsync(request, context).ConfigureAwait(false);
                seen.Add(response);
                return response;
            });

        var runner = TestContexts.RunnerOver(new NullTransport(), observing);

        await Assert.ThrowsAsync<PipelineAbortedException>(() => runner.RunAsync(MakeRequest(), TestContexts.For()).AsTask());
        Assert.Empty(seen);
    }

    [Fact]
    public async Task A_null_from_a_policy_fails_with_PipelineAbortedException()
    {
        var returnsNull = new DelegatePolicy(PipelineStage.PerAttempt, (_, _, _) => ValueTask.FromResult<Response>(null!));
        var runner = TestContexts.RunnerOver(new RecordingTransport(), returnsNull);

        // The runner at index 0 invokes the policy and checks its result.
        await Assert.ThrowsAsync<PipelineAbortedException>(() => runner.RunAsync(MakeRequest(), TestContexts.For()).AsTask());
    }

    [Fact]
    public void PipelineRunner_has_no_mutable_field()
    {
        // PIPE-13: the runner is the fork primitive; an immutable struct has nothing to advance.
        var fields = typeof(PipelineRunner).GetFields(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        Assert.All(fields, field => Assert.True(field.IsInitOnly, $"{field.Name} must be readonly"));
        Assert.True(typeof(PipelineRunner).IsValueType);
    }

    [Fact]
    public async Task A_default_runner_is_not_attached_and_fails_loudly()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => default(PipelineRunner).RunAsync(MakeRequest(), TestContexts.For()).AsTask());
    }

    private sealed class NullTransport : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            Task.FromResult<Response>(null!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // Presents a sync-only fake as the dual-interface transport the terminal prefers: Execute reaches the fake itself.
    private sealed class SyncAdapterOverSync(RecordingSyncTransport inner) : IAsyncHttpClient, IHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The async member must not be reached.");

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            inner.Execute(request, options, cancellationToken);

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
