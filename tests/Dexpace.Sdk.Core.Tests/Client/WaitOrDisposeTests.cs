// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Concurrent;
using System.Globalization;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// SEAM-30 and ASYNC-3 (phase 11, task 2.4; design §11 item 43, V5): <c>WaitOrDisposeAsync</c> is the cancel-without-interruption mode the documentation
/// recommends. A caller that stops awaiting the bridge's task through <c>Task.WaitAsync(token)</c> can orphan an undisposed response when the call completes
/// at the instant the token is signalled (44 of 20 000 runs of the V5 probe). Through <c>WaitOrDisposeAsync</c> every caller either gets a live response or an
/// <see cref="OperationCanceledException"/>, and every response the transport produced is closed exactly once. The raw leak is not asserted: a race that "may"
/// leak is a flaky assertion, and V5 is the evidence.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WaitOrDisposeTests
{
    private const int Iterations = 20_000;
    private const int Workers = 8;
    private static readonly TimeSpan s_patience = TimeSpan.FromSeconds(30);

    private static Task<Response> NewResponseTask(out TaskCompletionSource<Response> source)
    {
        source = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        return source.Task;
    }

    [Fact]
    public async Task A_response_that_arrives_first_is_returned_live()
    {
        using var body = new DisposalCountingBody();
        using var response = TestResponses.Create(Status.Ok, body: body);
        using var cts = new CancellationTokenSource();

        var result = await Task.FromResult(response).WaitOrDisposeAsync(cts.Token);
        await cts.CancelAsync();
        await Task.Yield();

        Assert.Same(response, result);
        Assert.Equal(0, body.DisposeCount);
    }

    [Fact]
    public async Task A_response_that_arrives_after_the_caller_stopped_waiting_is_closed_exactly_once()
    {
        var task = NewResponseTask(out var source);
        using var cts = new CancellationTokenSource();
        var wait = task.WaitOrDisposeAsync(cts.Token);

        await cts.CancelAsync();
        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        using var body = new DisposalCountingBody();
        source.SetResult(TestResponses.Create(Status.Ok, body: body));

        Assert.True(SpinWait.SpinUntil(() => body.DisposeCount == 1, s_patience));
        Assert.Equal(cts.Token, cancelled.CancellationToken);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_response_that_is_already_complete_is_returned_even_when_the_token_is_signalled()
    {
        // Task.WaitAsync hands back a task that has completed, whatever the token: the response is the caller's, not the helper's, to close.
        using var body = new DisposalCountingBody();
        using var response = TestResponses.Create(Status.Ok, body: body);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await Task.FromResult(response).WaitOrDisposeAsync(cts.Token);
        await Task.Yield();

        Assert.Same(response, result);
        Assert.Equal(0, body.DisposeCount);
    }

    [Fact]
    public async Task A_pre_cancelled_token_abandons_a_task_that_is_still_running_and_closes_its_response()
    {
        var task = NewResponseTask(out var source);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitOrDisposeAsync(cts.Token));
        using var body = new DisposalCountingBody();
        source.SetResult(TestResponses.Create(Status.Ok, body: body));

        Assert.True(SpinWait.SpinUntil(() => body.DisposeCount == 1, s_patience));
    }

    [Fact]
    public async Task A_failure_after_the_caller_stopped_waiting_is_observed_and_not_thrown()
    {
        var task = NewResponseTask(out var source);
        using var cts = new CancellationTokenSource();
        var wait = task.WaitOrDisposeAsync(cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);

        source.SetException(new InvalidOperationException("late failure"));

        // The fault is observed by the helper's awaiter, so it can never surface as an unobserved task exception.
        Assert.True(SpinWait.SpinUntil(() => task.IsFaulted, s_patience));
        Assert.Equal("late failure", task.Exception!.InnerException!.Message);
    }

    [Fact]
    public void A_null_task_is_rejected()
    {
        Task<Response> task = null!;

        Assert.Equal("task", Assert.Throws<ArgumentNullException>(() => { _ = task.WaitOrDisposeAsync(CancellationToken.None); }).ParamName);
    }

    [Fact]
    public async Task Every_caller_gets_a_live_response_or_a_cancellation_and_every_produced_response_is_closed_once()
    {
        // V5's race through the bridge: the blocking call completes while another thread signals the caller's token, 20 000 times, jittered.
        var random = new Random(30);
        var workSpin = Enumerable.Range(0, Iterations).Select(_ => random.Next(0, 4000)).ToArray();
        var cancelSpin = Enumerable.Range(0, Iterations).Select(_ => random.Next(0, 4000)).ToArray();
        var bodies = Enumerable.Range(0, Iterations).Select(static _ => new DisposalCountingBody()).ToArray();
        var produced = new bool[Iterations];
        var delivered = new bool[Iterations];
        var failures = new ConcurrentQueue<string>();
        using var transport = new SpinningTransport(workSpin, bodies, produced);
        await using var bridge = transport.AsAsync(TaskScheduler.Default);

        await Task.WhenAll(Enumerable.Range(0, Workers).Select(worker => Task.Run(
            async () =>
            {
                for (var i = worker; i < Iterations; i += Workers)
                {
                    await RunOneAsync(bridge, i, cancelSpin[i], bodies[i], delivered, failures);
                }
            },
            TestContext.Current.CancellationToken)));

        // A response abandoned by its caller is closed when the bridge's task completes, asynchronously: wait until every produced response is closed.
        var allClosed = SpinWait.SpinUntil(() => Enumerable.Range(0, Iterations).All(i => !Volatile.Read(ref produced[i]) || bodies[i].DisposeCount == 1), s_patience);

        Assert.Empty(failures);
        Assert.True(allClosed, "a produced response was left undisposed: " + Describe(produced, delivered, bodies));
        Assert.All(bodies.Where((_, i) => !produced[i]), static body => Assert.Equal(0, body.DisposeCount));
        Assert.Contains(true, delivered);
        Assert.Contains(false, delivered.Where((_, i) => produced[i]));
    }

    private static async Task RunOneAsync(IAsyncHttpClient bridge, int index, int cancelSpin, DisposalCountingBody body, bool[] delivered, ConcurrentQueue<string> failures)
    {
        using var cts = new CancellationTokenSource();
        var task = bridge.ExecuteAsync(Request.Get(string.Create(CultureInfo.InvariantCulture, $"https://example.test/{index}")), RequestOptions.Empty, cts.Token);
        var canceller = Task.Run(() =>
        {
            Thread.SpinWait(cancelSpin);
            cts.Cancel();
        });

        try
        {
            using var response = await task.WaitOrDisposeAsync(cts.Token);
            delivered[index] = true;
            if (body.DisposeCount != 0)
            {
                failures.Enqueue($"{index}: a delivered response had already been closed");
            }
        }
        catch (OperationCanceledException)
        {
            // The caller stopped waiting: whoever holds the response closes it, and the test checks that at the end.
        }

        await canceller;
    }

    private static string Describe(bool[] produced, bool[] delivered, DisposalCountingBody[] bodies)
    {
        var open = Enumerable.Range(0, Iterations).Where(i => produced[i] && bodies[i].DisposeCount != 1).Take(5).ToArray();
        return $"{open.Length} (first: {string.Join(", ", open.Select(i => string.Create(CultureInfo.InvariantCulture, $"#{i} delivered={delivered[i]} closed={bodies[i].DisposeCount}")))})";
    }

    private sealed class SpinningTransport(int[] spin, DisposalCountingBody[] bodies, bool[] produced) : IHttpClient
    {
        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            var index = int.Parse(request.Url.AbsolutePath.AsSpan(1), CultureInfo.InvariantCulture);
            Thread.SpinWait(spin[index]);
            var response = TestResponses.Create(Status.Ok, request, body: bodies[index]);
            Volatile.Write(ref produced[index], true);
            return response;
        }

        public void Dispose()
        {
        }
    }
}
