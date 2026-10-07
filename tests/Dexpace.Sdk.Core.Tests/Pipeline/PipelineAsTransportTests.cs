// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/runtime.test.ts (runtime as transport, close is a no-op toward
// the transport). A pipeline backing a paginator is exercised by PageableTests, whose Pageable.Create takes the pipeline.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class PipelineAsTransportTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    [Fact]
    public async Task A_pipeline_stands_in_for_a_transport_and_threads_options_through()
    {
        var inner = new RecordingTransport();
        using var innerPipeline = new PipelineBuilder().Build(inner);
        using var outerPipeline = new PipelineBuilder().Build(innerPipeline);
        var options = new RequestOptions { MaxRetries = 5 };

        using var response = await outerPipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken);

        Assert.Same(options, inner.LastCall!.Options);
    }

    [Fact]
    public async Task A_pipeline_is_both_seams_and_the_seam_entry_points_thread_options_and_token()
    {
        var transport = new RecordingTransport();
        using var pipeline = new PipelineBuilder().Build(transport);
        var options = new RequestOptions { MaxRetries = 2 };
        using var cts = new CancellationTokenSource();

        IAsyncHttpClient asyncSeam = pipeline;
        IHttpClient syncSeam = pipeline;
        using var viaAsync = await asyncSeam.ExecuteAsync(MakeRequest(), options, cts.Token);
        using var viaSync = syncSeam.Execute(MakeRequest(), options, cts.Token);

        Assert.Equal(2, transport.CallCount);
        Assert.All(transport.Calls, call =>
        {
            Assert.Same(options, call.Options);
            Assert.Equal(cts.Token, call.CancellationToken);
        });
    }

    [Fact]
    public async Task Disposing_the_pipeline_twice_never_disposes_the_transport()
    {
        var transport = new DisposeCountingTransport();
        var pipeline = new PipelineBuilder().Build(transport);

        pipeline.Dispose();
        pipeline.Dispose();
        await pipeline.DisposeAsync();
        await pipeline.DisposeAsync();
        pipeline.Dispose();
        await pipeline.DisposeAsync();

        Assert.Equal(0, transport.DisposeCount);

        // PIPE-27: there is no latch; a disposed pipeline stays usable.
        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);
        Assert.Equal(Status.Ok, response.Status);
    }
}
