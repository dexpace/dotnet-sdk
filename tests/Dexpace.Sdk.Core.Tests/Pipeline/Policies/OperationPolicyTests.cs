// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

[Trait("Category", "Unit")]
public sealed class OperationPolicyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/items");

    private static DexpaceClientOptions OptionsWithTimeout(TimeSpan timeout) =>
        new() { OverallTimeout = timeout };

    private static DexpaceClientOptions OptionsNoTimeout() => new() { OverallTimeout = null };

    // A transport that delays indefinitely until its token is cancelled.
    private sealed class HangingTransport : IAsyncHttpClient
    {
        public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return TestResponses.Create(Status.Ok);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Stage
    // -------------------------------------------------------------------------

    [Fact]
    public void Stage_IsOperation()
    {
        var policy = new OperationPolicy();
        Assert.Equal(PipelineStage.Operation, policy.Stage);
    }

    // -------------------------------------------------------------------------
    // Timeout behaviour
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_WithShortTimeout_ThrowsWhenTransportHangs()
    {
        var pipeline = new PipelineBuilder()
            .Add(new OperationPolicy())
            .Build(new HangingTransport());

        var options = OptionsWithTimeout(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAsync<OperationTimeoutException>(
            () => pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task ProcessAsync_WithNoTimeout_CompletesNormally()
    {
        var pipeline = new PipelineBuilder()
            .Add(new OperationPolicy())
            .Build(new RecordingTransport());

        var response = await pipeline.SendAsync(MakeRequest(), OptionsNoTimeout(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
    }

    [Fact]
    public void A_zero_or_negative_timeout_is_rejected_where_it_is_set()
    {
        // 6a (P6a-24, Breaking 8): a non-positive OverallTimeout used to mean "none"; null means that now.
        Assert.Throws<ArgumentOutOfRangeException>(() => OptionsWithTimeout(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => OptionsWithTimeout(TimeSpan.FromSeconds(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => OptionsWithTimeout(Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public async Task ProcessAsync_CallerCancellation_Propagates_EvenWithTimeout()
    {
        // Caller cancels before the pipeline finishes — OperationCanceledException propagates.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var pipeline = new PipelineBuilder()
            .Add(new OperationPolicy())
            .Build(new HangingTransport());

        var options = OptionsWithTimeout(TimeSpan.FromSeconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pipeline.SendAsync(MakeRequest(), options, cts.Token).AsTask());
    }

    [Fact]
    public void Process_sync_with_short_timeout_throws_when_the_transport_hangs()
    {
        var pipeline = new PipelineBuilder()
            .Add(new OperationPolicy())
            .Build(new HangingTransport());

        var options = OptionsWithTimeout(TimeSpan.FromMilliseconds(30));

        Assert.Throws<OperationTimeoutException>(
            () => pipeline.Send(MakeRequest(), options, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_deadline_surfaces_OperationTimeoutException_not_cancellation()
    {
        var clock = new FakeTimeProvider();
        var pipeline = new PipelineBuilder().Add(new OperationPolicy(clock)).Build(new HangingTransport());
        var task = pipeline.SendAsync(MakeRequest(), OptionsWithTimeout(TimeSpan.FromSeconds(30)), TestContext.Current.CancellationToken).AsTask();

        clock.Advance(TimeSpan.FromSeconds(30));
        var thrown = await Assert.ThrowsAsync<OperationTimeoutException>(() => task);

        Assert.IsAssignableFrom<OperationCanceledException>(thrown.InnerException);
        Assert.False(thrown.IsRetryable);
        Assert.False(TestContext.Current.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task A_caller_cancellation_still_surfaces_OperationCanceledException()
    {
        var clock = new FakeTimeProvider();
        using var cts = new CancellationTokenSource();
        var pipeline = new PipelineBuilder().Add(new OperationPolicy(clock)).Build(new HangingTransport());
        var task = pipeline.SendAsync(MakeRequest(), OptionsWithTimeout(TimeSpan.FromSeconds(30)), cts.Token).AsTask();

        await cts.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.IsNotType<OperationTimeoutException>(thrown);
    }

    [Fact]
    public async Task The_inner_trail_is_copied_onto_the_timeout_exception()
    {
        var clock = new FakeTimeProvider();
        var first = new ServiceRequestException("first");
        var hanging = new HangingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new OperationPolicy(clock))
            .Add(new RetryPolicy(clock))
            .Build(new FailThenHangTransport(first, hanging));
        var options = new DexpaceClientOptions
        {
            OverallTimeout = TimeSpan.FromSeconds(30),
            Retry = new RetryOptions { BaseDelay = TimeSpan.Zero, Jitter = 0 },
        };

        var task = pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken).AsTask();
        while (!task.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(10));
            await Task.Yield();
        }

        var thrown = await Assert.ThrowsAsync<OperationTimeoutException>(() => task);
        Assert.Equal([first], ExceptionTrail.GetSuppressed(thrown));
    }

    private sealed class FailThenHangTransport(Exception first, IAsyncHttpClient hanging) : IAsyncHttpClient
    {
        private int _calls;

        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            Interlocked.Increment(ref _calls) == 1
                ? Task.FromException<Response>(first)
                : hanging.ExecuteAsync(request, options, cancellationToken);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void The_timeout_is_not_retried_by_an_inner_or_outer_classifier()
    {
        var timeout = new OperationTimeoutException("deadline", new OperationCanceledException());

        Assert.False(Dexpace.Sdk.Core.Resilience.RetryFacts.IsRetryableFailure(
            timeout,
            Dexpace.Sdk.Core.Resilience.RetryFacts.DefaultRetryableStatusCodes,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public void OperationPolicy_parameterless_construction_still_compiles_via_the_optional_parameter()
    {
        var policy = new OperationPolicy();

        Assert.Equal(PipelineStage.Operation, policy.Stage);
    }

    [Fact]
    public async Task Sync_and_async_agree_without_a_timeout()
    {
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder().Add(new OperationPolicy()).Build(transport);

        using var sync = pipeline.Send(MakeRequest(), OptionsNoTimeout(), TestContext.Current.CancellationToken);
        using var async = await pipeline.SendAsync(MakeRequest(), OptionsNoTimeout(), TestContext.Current.CancellationToken);

        Assert.Equal(2, transport.CallCount);
    }
}
