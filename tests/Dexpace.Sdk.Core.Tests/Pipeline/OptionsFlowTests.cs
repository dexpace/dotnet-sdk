// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class OptionsFlowTests
{
    [Fact]
    public async Task The_same_request_options_instance_reaches_every_policy_every_fork_and_the_transport()
    {
        // PIPE-17: Assert.Same, not Assert.Equal: equality would pass against a per-fork copy.
        var options = new RequestOptions { MaxRetries = 3 }.WithTag("k", "v");
        var seen = new List<RequestOptions>();
        DelegatePolicy Probe(PipelineStage stage) => new(stage, (request, context, next) =>
        {
            seen.Add(context.RequestOptions);
            return next.RunAsync(request, context);
        });
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(Probe(PipelineStage.PerCall))
            .Add(new ForkingProbe(PipelineStage.Retry, drives: 2))
            .Add(Probe(PipelineStage.PerAttempt))
            .Build(transport);

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/x"), options, TestContext.Current.CancellationToken);

        Assert.Equal(3, seen.Count);
        Assert.All(seen, observed => Assert.Same(options, observed));
        Assert.Equal(2, transport.CallCount);
        Assert.All(transport.Calls, call => Assert.Same(options, call.Options));
    }
}
