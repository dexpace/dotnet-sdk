// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>
/// PIPE-31: <c>SendAsync&lt;T&gt;</c> and <c>Send&lt;T&gt;</c> apply the handler and dispose the response in every outcome.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TerminalMappingTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    private static async Task<int> MapAsync(HttpPipeline pipeline, bool useAsync, Func<Response, int> handler) =>
        useAsync
            ? await pipeline.SendAsync(MakeRequest(), (response, _) => ValueTask.FromResult(handler(response)), RequestOptions.Empty, TestContext.Current.CancellationToken)
            : pipeline.Send(MakeRequest(), handler, RequestOptions.Empty, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_successful_handler_disposes_the_response_after_it_runs(bool useAsync)
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log);
        var pipeline = new PipelineBuilder().Build(new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: body)));

        var result = await MapAsync(pipeline, useAsync, _ => { log.Add("handler"); return 7; });

        Assert.Equal(7, result);
        Assert.Equal(["handler", "body:dispose"], log);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_throwing_handler_surfaces_its_own_instance_and_the_body_is_disposed(bool useAsync)
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log);
        var failure = new InvalidOperationException("handler failed");
        var pipeline = new PipelineBuilder().Build(new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: body)));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => MapAsync(pipeline, useAsync, _ => throw failure));

        Assert.Same(failure, thrown);
        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_throwing_dispose_after_a_throwing_handler_leaves_the_handlers_exception_primary(bool useAsync)
    {
        var log = new List<string>();
        var disposeFailure = new IOException("dispose failed");
        using var body = new TrackingResponseBody(log, disposeFailure: disposeFailure);
        var failure = new InvalidOperationException("handler failed");
        var pipeline = new PipelineBuilder().Build(new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: body)));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => MapAsync(pipeline, useAsync, _ => throw failure));

        Assert.Same(failure, thrown);
        Assert.Same(disposeFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_transport_failure_surfaces_unwrapped_not_as_AggregateException(bool useAsync)
    {
        var failure = new ServiceRequestException("connection refused");
        var pipeline = new PipelineBuilder().Build(new ScriptedTransport(failure));

        var thrown = await Assert.ThrowsAsync<ServiceRequestException>(() => MapAsync(pipeline, useAsync, _ => 0));

        Assert.Same(failure, thrown);
    }
}
