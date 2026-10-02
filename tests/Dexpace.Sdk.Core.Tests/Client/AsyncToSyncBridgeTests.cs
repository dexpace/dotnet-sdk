// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Clients;

/// <summary>
/// <c>AsBlocking</c> (SEAM-13, SEAM-14, SEAM-16, SEAM-18; design §3.3, §5.3): blocks with <c>GetAwaiter().GetResult()</c>
/// so a failure surfaces as itself, threads options and token into the wrapped call, and never disposes what it wraps.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AsyncToSyncBridgeTests
{
    private static Request NewRequest() => Request.Get("https://api.example.com/v1/items");

    private sealed class FuncTransport(Func<Request, RequestOptions, CancellationToken, Task<Response>?> send) : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            send(request, options, cancellationToken)!;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void A_faulted_InvalidOperationException_surfaces_as_itself_not_as_an_AggregateException()
    {
        var failure = new InvalidOperationException("transport failed");
        var transport = new RecordingTransport(_ => throw failure);
        using var blocking = transport.AsBlocking();

        var thrown = Assert.Throws<InvalidOperationException>(() => blocking.Execute(NewRequest(), RequestOptions.Empty, CancellationToken.None));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void Options_and_token_reach_ExecuteAsync_by_reference()
    {
        using var cts = new CancellationTokenSource();
        var transport = new RecordingTransport();
        using var blocking = transport.AsBlocking();
        var request = NewRequest();
        var options = new RequestOptions { MaxRetries = 2 };

        using var response = blocking.Execute(request, options, cts.Token);

        var call = Assert.IsType<RecordedCall>(transport.LastCall);
        Assert.Same(request, call.Request);
        Assert.Same(options, call.Options);
        Assert.Equal(cts.Token, call.CancellationToken);
    }

    [Fact]
    public async Task Cancelling_the_token_cancels_the_in_flight_task_and_surfaces_OperationCanceledException()
    {
        // SEAM-13: a second thread cancels; the blocked Execute throws OperationCanceledException instead of hanging.
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var transport = DelegateHttpClient.Create(async (_, _, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return null!;
        });
        using var blocking = transport.AsBlocking();
        using var cts = new CancellationTokenSource();

        var blocked = Task.Run(
            () => blocking.Execute(NewRequest(), RequestOptions.Empty, cts.Token),
            TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blocked.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disposing_the_bridge_never_disposes_the_wrapped_client()
    {
        // SEAM-14, breaking item 5: the bridge used to dispose the client it wrapped.
        var transport = new RecordingTransport();
        var blocking = transport.AsBlocking();

        blocking.Dispose();
        Assert.False(transport.IsDisposed);

        blocking.Dispose();
        Assert.False(transport.IsDisposed);
        await transport.DisposeAsync();
        Assert.True(transport.IsDisposed);
    }

    [Fact]
    public void A_null_Task_or_a_null_Response_from_the_wrapped_client_fails_with_InvalidOperationException()
    {
        // SEAM-16: neither a NullReferenceException from GetAwaiter() nor a null handed to the caller.
        using var nullTask = new FuncTransport((_, _, _) => null).AsBlocking();
        using var nullResult = new FuncTransport((_, _, _) => Task.FromResult<Response>(null!)).AsBlocking();

        Assert.Throws<InvalidOperationException>(() => nullTask.Execute(NewRequest(), RequestOptions.Empty, CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() => nullResult.Execute(NewRequest(), RequestOptions.Empty, CancellationToken.None));
    }

    [Fact]
    public void A_null_request_or_null_options_throws_ArgumentNullException()
    {
        var transport = new RecordingTransport();
        using var blocking = transport.AsBlocking();

        Assert.Equal("request", Assert.Throws<ArgumentNullException>(() => blocking.Execute(null!, RequestOptions.Empty, CancellationToken.None)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => blocking.Execute(NewRequest(), null!, CancellationToken.None)).ParamName);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public void A_null_client_is_rejected()
    {
        IAsyncHttpClient client = null!;

        Assert.Equal("client", Assert.Throws<ArgumentNullException>(() => client.AsBlocking()).ParamName);
    }
}
