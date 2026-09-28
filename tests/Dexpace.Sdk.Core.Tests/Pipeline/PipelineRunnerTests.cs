// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
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
