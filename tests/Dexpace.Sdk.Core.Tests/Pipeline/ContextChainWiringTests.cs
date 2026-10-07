// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>
/// Position I (P4c-21): the pipeline wires 4a's context chain: one dispatch per call, a promotion per transmission, the
/// exchange closed by the response's dispose, and the furthest link closed on a non-fatal failure. The pipeline never
/// calls the store.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ContextChainWiringTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    private sealed class KeyCapture
    {
        public CallKey Key { get; set; }

        public DelegatePolicy Policy(PipelineStage stage = PipelineStage.Operation, Action<CallKey>? onEntry = null) =>
            new DelegatePolicy(stage, (request, context, next) =>
            {
                Key = context.CallKey;
                onEntry?.Invoke(context.CallKey);
                return next.RunAsync(request, context);
            });
    }

    [Fact]
    public async Task Nothing_is_registered_before_the_first_transmission()
    {
        // CTX-17: a dispatch registers nothing; the terminal's promotion does.
        var registeredBefore = true;
        var capture = new KeyCapture();
        var pipeline = new PipelineBuilder()
            .Add(capture.Policy(onEntry: key => registeredBefore = DexpaceCallContexts.TryGet(key, out _)))
            .Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.False(registeredBefore);
        Assert.True(DexpaceCallContexts.TryGet(capture.Key, out _));
    }

    [Fact]
    public async Task One_store_entry_per_call_across_a_retried_and_redirected_call()
    {
        var capture = new KeyCapture();
        var transport = new ScriptedTransport(
            TestResponses.Redirect(307, "https://api.example.com/moved"),
            TestResponses.Create(Status.ServiceUnavailable),
            TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder()
            .Add(capture.Policy())
            .Add(new RedirectPolicy())
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Build(transport);

        using var response = await pipeline.SendAsync(MakeRequest(), new DexpaceClientOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(3, transport.CallCount);
        Assert.True(DexpaceCallContexts.TryGet(capture.Key, out var occupant));
        var exchange = Assert.IsType<ExchangeContext>(occupant);
        Assert.Same(response, exchange.Response);
        Assert.Same(transport.Requests[2], exchange.Request);
    }

    [Fact]
    public async Task The_returned_responses_dispose_closes_the_occupant()
    {
        var capture = new KeyCapture();
        var pipeline = new PipelineBuilder().Add(capture.Policy()).Build(new RecordingTransport());

        var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);
        Assert.True(DexpaceCallContexts.TryGet(capture.Key, out _));

        response.Dispose();

        Assert.False(DexpaceCallContexts.TryGet(capture.Key, out _));
    }

    [Fact]
    public async Task A_failure_closes_the_furthest_link_before_the_exception_surfaces()
    {
        var capture = new KeyCapture();
        var failure = new InvalidOperationException("boom");
        var pipeline = new PipelineBuilder().Add(capture.Policy()).Build(new ScriptedTransport(failure));
        var registeredInCatch = true;

        try
        {
            using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            registeredInCatch = DexpaceCallContexts.TryGet(capture.Key, out _);
            Assert.Same(failure, ex);
        }

        Assert.False(registeredInCatch);
    }

    [Fact]
    public async Task A_fatal_exception_skips_the_close()
    {
        // Position I item 3: the store's bound is the backstop for a fatal exception.
        var capture = new KeyCapture();
#pragma warning disable CA2201 // The point: the runtime-reserved fatal type.
        var fatal = new OutOfMemoryException("simulated");
#pragma warning restore CA2201
        var pipeline = new PipelineBuilder().Add(capture.Policy()).Build(new ScriptedTransport(fatal));

        await Assert.ThrowsAsync<OutOfMemoryException>(() => pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken).AsTask());

        Assert.True(DexpaceCallContexts.TryGet(capture.Key, out var left));
        left.Close();
    }

    [Fact]
    public async Task A_nested_pipeline_call_leaves_no_store_entry_after_the_response_is_disposed()
    {
        // Position I item 5: two exchange links on one response; disposing it closes both.
        var inner = new KeyCapture();
        var outer = new KeyCapture();
        using var innerPipeline = new PipelineBuilder().Add(inner.Policy()).Build(new RecordingTransport());
        using var outerPipeline = PipelineBuilder.Nest(innerPipeline).Add(outer.Policy()).Build();

        var response = await outerPipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);
        Assert.NotEqual(inner.Key, outer.Key);
        Assert.True(DexpaceCallContexts.TryGet(inner.Key, out _));
        Assert.True(DexpaceCallContexts.TryGet(outer.Key, out _));

        response.Dispose();

        Assert.False(DexpaceCallContexts.TryGet(inner.Key, out _));
        Assert.False(DexpaceCallContexts.TryGet(outer.Key, out _));
    }
}
