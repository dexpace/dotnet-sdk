// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class PolicyContractTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    private static async Task<Response> SendAsync(HttpPipeline pipeline, bool useAsync) =>
        useAsync
            ? await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken)
            : pipeline.Send(MakeRequest(), TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_short_circuit_returns_its_synthetic_response_and_reaches_nothing_downstream(bool useAsync)
    {
        var log = new List<string>();
        var synthetic = TestResponses.Create(Status.Accepted);
        var downstream = new ProbePolicy("downstream", PipelineStage.PerAttempt, log);
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ShortCircuitPolicy(PipelineStage.Operation, synthetic))
            .Add(downstream)
            .Build(transport);

        var response = await SendAsync(pipeline, useAsync);

        Assert.Same(synthetic, response);
        Assert.Equal(0, downstream.Entered);
        Assert.Equal(0, transport.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_policy_may_substitute_the_response_on_the_way_out(bool useAsync)
    {
        var substitute = TestResponses.Create(Status.Created);
        var pipeline = new PipelineBuilder()
            .Add(new DelegatePolicy(
                PipelineStage.Operation,
                async (request, context, next) =>
                {
                    (await next.RunAsync(request, context).ConfigureAwait(false)).Dispose();
                    return substitute;
                },
                (request, context, next) =>
                {
                    next.Run(request, context).Dispose();
                    return substitute;
                }))
            .Build(new RecordingTransport());

        var response = await SendAsync(pipeline, useAsync);

        Assert.Same(substitute, response);
    }

    [Fact]
    public async Task A_substituted_request_reaches_every_downstream_policy_and_the_transport()
    {
        var original = MakeRequest();
        var substitute = original.WithHeaders(original.Headers.Set("X-Substituted", "yes"));
        var log = new List<string>();
        var perCall = new ProbePolicy("perCall", PipelineStage.PerCall, log);
        var auth = new ProbePolicy("auth", PipelineStage.Auth, log);
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new DelegatePolicy(PipelineStage.Operation, (_, context, next) => next.RunAsync(substitute, context)))
            .Add(perCall)
            .Add(auth)
            .Build(transport);

        using var response = await pipeline.SendAsync(original, TestContext.Current.CancellationToken);

        Assert.Same(substitute, perCall.LastRequest);
        Assert.Same(substitute, auth.LastRequest);
        Assert.Same(substitute, transport.LastRequest);
        Assert.NotSame(original, transport.LastRequest);
    }
}
