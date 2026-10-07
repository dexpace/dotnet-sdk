// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// PIPE-33 and PIPE-34 are the 2b bridges (SyncToAsyncBridgeTests, AsyncToSyncBridgeTests); these add the end-to-end cases
// over a pipeline.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Threading;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class PipelineBridgeTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    [Fact]
    public async Task A_sync_pipeline_bridged_to_async_runs_as_one_unit_on_the_given_scheduler()
    {
        using var scheduler = new RecordingTaskScheduler();
        var log = new List<string>();
        using var transport = new RecordingSyncTransport();
        var builder = new PipelineBuilder();
        foreach (var stage in new[] { PipelineStage.Operation, PipelineStage.PerCall, PipelineStage.Redirect, PipelineStage.Retry, PipelineStage.PerAttempt })
        {
            builder.Add(new ProbePolicy(stage.ToString(), stage, log));
        }

        using var pipeline = builder.Build(new SyncOnlyAsAsync(transport));
        var options = new RequestOptions { MaxRetries = 1 };
        IHttpClient syncSeam = pipeline;
        var bridged = syncSeam.AsAsync(scheduler);

        using var response = await bridged.ExecuteAsync(MakeRequest(), options, TestContext.Current.CancellationToken);

        Assert.Equal(1, scheduler.QueueCount);
        Assert.Same(options, transport.LastCall!.Options);
        Assert.Equal(10, log.Count);
    }

    [Fact]
    public void An_async_pipeline_bridged_to_sync_preserves_options_and_surfaces_the_original_exception()
    {
        var failure = new ServiceRequestException("connection refused");
        var transport = new ScriptedTransport(TestResponses.Create(Dexpace.Sdk.Core.Http.Response.Status.Ok), failure);
        using var pipeline = new PipelineBuilder().Build(transport);
        IAsyncHttpClient asyncSeam = pipeline;
        var blocking = asyncSeam.AsBlocking();
        var options = new RequestOptions { MaxRetries = 4 };

        using var ok = blocking.Execute(MakeRequest(), options, TestContext.Current.CancellationToken);
        var thrown = Assert.Throws<ServiceRequestException>(
            () => blocking.Execute(MakeRequest(), options, TestContext.Current.CancellationToken));

        Assert.Same(options, transport.Calls[0].Options);
        Assert.Same(failure, thrown);
    }

    // Presents the sync-only fake to the pipeline builder, which takes the async seam.
    private sealed class SyncOnlyAsAsync(RecordingSyncTransport inner) : IAsyncHttpClient, IHttpClient
    {
        public Task<Dexpace.Sdk.Core.Http.Response.Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The async member must not be reached.");

        public Dexpace.Sdk.Core.Http.Response.Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            inner.Execute(request, options, cancellationToken);

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
