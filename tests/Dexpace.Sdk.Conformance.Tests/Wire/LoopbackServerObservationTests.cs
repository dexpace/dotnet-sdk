// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Conformance.Wire;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Wire;

/// <summary>
/// P8a-21: the fixture lets a test wait on a condition instead of sleeping. "Released" means the client closed the
/// connection or sent a further request on it, observed from the server side; request arrival is a condition too.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LoopbackServerObservationTests
{
    private const string Get = "GET /a HTTP/1.1\r\nHost: x\r\n\r\n";
    private const string KeepAliveReply = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async IAsyncEnumerable<byte[]> ManyChunks()
    {
        var chunk = new byte[64 * 1024];
        for (var i = 0; i < 2000; i++)
        {
            yield return chunk;
        }
    }

    [Fact]
    public async Task A_request_wait_completes_with_the_recorded_request_after_it_is_sent()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw(KeepAliveReply, closeConnection: false));
        using var peer = await RawTcp.ConnectAsync(server, Ct);

        var wait = server.WaitForRequestAsync(0, Ct);
        Assert.True(await Waits.StaysPendingAsync(wait, Ct));
        await peer.SendAsync(Get, Ct);
        var request = await wait.WaitAsync(Waits.Bound, Ct);

        Assert.Same(server.Requests[0], request);
        Assert.Equal("GET /a HTTP/1.1", request.RequestLine);
    }

    [Fact]
    public async Task A_request_wait_started_after_arrival_completes_at_once()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw(KeepAliveReply, closeConnection: false));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await peer.ReadUntilAsync("ok", Ct);

        var request = await server.WaitForRequestAsync(0, Ct).WaitAsync(Waits.Bound, Ct);

        Assert.Same(server.Requests[0], request);
    }

    [Fact]
    public async Task A_request_wait_honours_cancellation()
    {
        await using var server = LoopbackServer.Start();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var wait = server.WaitForRequestAsync(0, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait.WaitAsync(Waits.Bound, Ct));
    }

    [Fact]
    public async Task A_request_wait_for_an_index_never_reached_ends_when_the_server_is_disposed()
    {
        var server = LoopbackServer.Start();
        var wait = server.WaitForRequestAsync(5, Ct);
        Assert.True(await Waits.StaysPendingAsync(wait, Ct));

        await server.DisposeAsync();

        var error = await Record.ExceptionAsync(() => wait.WaitAsync(Waits.Bound, Ct));
        Assert.True(error is ObjectDisposedException or OperationCanceledException, error?.GetType().Name);
    }

    [Fact]
    public async Task A_negative_request_index_is_rejected_synchronously()
    {
        await using var server = LoopbackServer.Start();

        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = server.WaitForRequestAsync(-1, Ct); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = server.WaitForConnectionReleasedAsync(-1, Ct); });
    }

    [Fact]
    public async Task A_connection_is_released_when_the_client_closes_it_after_a_keep_alive_reply()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw(KeepAliveReply, closeConnection: false));
        var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await peer.ReadUntilAsync("ok", Ct);
        var released = server.WaitForConnectionReleasedAsync(0, Ct);
        Assert.True(await Waits.StaysPendingAsync(released, Ct), "an idle keep-alive connection is not released");

        peer.Dispose();

        await Waits.CompletesAsync(released, Ct);
        Assert.False(server.ServerClosedFirst(0));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_connection_is_released_when_the_client_sends_a_further_request_on_it()
    {
        await using var server = LoopbackServer.Start(
            LoopbackResponse.Raw(KeepAliveReply, closeConnection: false),
            LoopbackResponse.Raw(KeepAliveReply, closeConnection: false));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await peer.ReadUntilAsync("ok", Ct);
        var released = server.WaitForConnectionReleasedAsync(0, Ct);
        Assert.True(await Waits.StaysPendingAsync(released, Ct));

        await peer.SendAsync(Get, Ct);

        await Waits.CompletesAsync(released, Ct);
        Assert.False(server.ServerClosedFirst(0));
    }

    [Fact]
    public async Task A_wait_for_a_connection_not_yet_accepted_waits_for_it()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw(KeepAliveReply, closeConnection: false));
        var released = server.WaitForConnectionReleasedAsync(0, Ct);
        Assert.True(await Waits.StaysPendingAsync(released, Ct));

        var peer = await RawTcp.ConnectAsync(server, Ct);
        peer.Dispose();

        await Waits.CompletesAsync(released, Ct);
    }

    [Fact]
    public async Task A_connection_is_released_when_the_client_closes_it_mid_write_of_a_large_chunked_reply()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Streamed([new("Content-Type", "application/octet-stream")], ManyChunks()));
        var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await peer.ReadExactlyAsync(1024, Ct);

        peer.Dispose();

        await Waits.CompletesAsync(server.WaitForConnectionReleasedAsync(0, Ct), Ct);
        Assert.False(server.ServerClosedFirst(0));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_server_side_close_releases_the_connection_and_is_flagged_as_server_closed_first()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("bye"));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await peer.ReadUntilAsync("bye", Ct);

        await Waits.CompletesAsync(server.WaitForConnectionReleasedAsync(0, Ct), Ct);

        Assert.True(server.ServerClosedFirst(0));
    }

    [Fact]
    public async Task Disposing_the_server_releases_every_connection_it_held()
    {
        var server = LoopbackServer.Start(LoopbackResponse.Raw(KeepAliveReply, closeConnection: false));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await peer.ReadUntilAsync("ok", Ct);
        var released = server.WaitForConnectionReleasedAsync(0, Ct);

        await server.DisposeAsync();

        await Waits.CompletesAsync(released, Ct);
    }

    [Fact]
    public async Task ServerClosedFirst_rejects_a_connection_that_does_not_exist()
    {
        await using var server = LoopbackServer.Start();

        Assert.Throws<ArgumentOutOfRangeException>(() => server.ServerClosedFirst(0));
    }
}
