// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public class PipelineRunnerTests
{
    private static Request MakeRequest() =>
        Request.Get("https://api.example.com/v1/resource");

    private static PipelineContext MakeContext() =>
        new(MakeRequest(), new DexpaceClientOptions());

    // ---------------------------------------------------------------------------
    // Test fakes
    // ---------------------------------------------------------------------------

    private sealed class RecordingPolicy(string name, PipelineStage stage, List<string> log)
        : HttpPipelinePolicy
    {
        public override PipelineStage Stage => stage;

        public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
        {
            log.Add($"{name}:in");
            await continuation.RunAsync(context).ConfigureAwait(false);
            log.Add($"{name}:out");
        }
    }

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecutionOrder_PoliciesRunInStageOrderInAndReversedOut_TransportInvokedOnce()
    {
        var log = new List<string>();
        var transport = new RecordingTransport();

        // a = Operation (100), b = PerAttempt (400) — stage ordering: a before b
        var policies = new HttpPipelinePolicy[]
        {
            new RecordingPolicy("a", PipelineStage.Operation, log),
            new RecordingPolicy("b", PipelineStage.PerAttempt, log),
        };

        var runner = new PipelineRunner(policies, 0, transport);
        var context = MakeContext();
        await runner.RunAsync(context);

        Assert.Equal(["a:in", "b:in", "b:out", "a:out"], log);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task Reentrancy_PolicyCallingNextTwice_TransportInvokedTwice()
    {
        var transport = new RecordingTransport();
        var doubleCallPolicy = new DoubleDipPolicy();
        var policies = new HttpPipelinePolicy[] { doubleCallPolicy };

        var runner = new PipelineRunner(policies, 0, transport);
        var context = MakeContext();
        await runner.RunAsync(context);

        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task The_transport_receives_RequestOptions_Empty()
    {
        // Position D: until phase 4c carries a caller's options on the context, the runner passes Empty. 4c replaces this test.
        var transport = new RecordingTransport();

        await new PipelineRunner([], 0, transport).RunAsync(MakeContext());

        Assert.Same(RequestOptions.Empty, transport.LastCall!.Options);
    }

    [Fact]
    public async Task The_transport_receives_the_contexts_token()
    {
        using var cts = new CancellationTokenSource();
        var transport = new RecordingTransport();
        var context = new PipelineContext(MakeRequest(), new DexpaceClientOptions(), cts.Token);

        await new PipelineRunner([], 0, transport).RunAsync(context);

        Assert.Equal(cts.Token, transport.LastCall!.CancellationToken);
    }

    [Fact]
    public async Task A_null_response_from_the_transport_fails_at_the_runner_before_any_policy_sees_it()
    {
        // SEAM-16: nullability is compile-time only, so the terminal runner asserts the transport's result.
        var observed = new List<Response?>();
        var policy = new ObservingPolicy(observed);
        var context = MakeContext();

        var runner = new PipelineRunner([policy], 0, new NullTransport());

        await Assert.ThrowsAsync<PipelineAbortedException>(() => runner.RunAsync(context).AsTask());
        Assert.Empty(observed);
    }

    private sealed class NullTransport : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            Task.FromResult<Response>(null!);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ObservingPolicy(List<Response?> observed) : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Operation;

        public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
        {
            await continuation.RunAsync(context).ConfigureAwait(false);
            observed.Add(context.Response);
        }
    }

    private sealed class DoubleDipPolicy : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.PerAttempt;

        public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
        {
            await continuation.RunAsync(context).ConfigureAwait(false);
            await continuation.RunAsync(context).ConfigureAwait(false);
        }
    }
}
