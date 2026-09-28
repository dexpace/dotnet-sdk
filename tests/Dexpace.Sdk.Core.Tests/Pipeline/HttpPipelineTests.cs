// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public class HttpPipelineTests
{
    private static Request MakeRequest() =>
        Request.Get("https://api.example.com/v1/resource");

    private static DexpaceClientOptions MakeOptions() => new();

    [Fact]
    public async Task SendAsync_ReturnsTransportResponse()
    {
        var expected = new Response(Status.Ok);
        var pipeline = new PipelineBuilder().Build(new RecordingTransport(_ => expected));

        var actual = await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Send_ReturnsTransportResponse()
    {
        var expected = new Response(Status.Ok);
        var pipeline = new PipelineBuilder().Build(new RecordingTransport(_ => expected));

        var actual = pipeline.Send(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task SendAsync_WithPolicies_PoliciesInvokedAndResponseReturned()
    {
        var log = new List<string>();
        var expected = new Response(Status.Ok);

        var pipeline = new PipelineBuilder()
            .Add(new LoggingPolicy("a", PipelineStage.Operation, log))
            .Add(new LoggingPolicy("b", PipelineStage.PerAttempt, log))
            .Build(new RecordingTransport(_ => expected));

        var actual = await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
        Assert.Equal(["a:in", "b:in", "b:out", "a:out"], log);
    }

    private sealed class LoggingPolicy(string name, PipelineStage stage, List<string> log)
        : HttpPipelinePolicy
    {
        public override PipelineStage Stage => stage;

        public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
        {
            log.Add($"{name}:in");
            await continuation.RunAsync(context);
            log.Add($"{name}:out");
        }
    }
}
