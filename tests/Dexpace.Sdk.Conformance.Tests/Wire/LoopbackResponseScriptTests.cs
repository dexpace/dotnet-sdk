// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Conformance.Wire;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Wire;

/// <summary>
/// The scripted replies phase 8a added to the fixture (P8a-11, plan task 1.9): gated, headers-then-gate, abort, hang,
/// large, echo and keep-alive. Each is what a transport conformance assertion needs a server to be able to do.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LoopbackResponseScriptTests
{
    private const string Get = "GET /a HTTP/1.1\r\nHost: x\r\n\r\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static HttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task A_gated_reply_writes_nothing_until_its_gate_opens_and_then_writes_its_bytes_intact()
    {
        var gate = NewGate();
        await using var server = LoopbackServer.Start(LoopbackResponse.Gated(gate.Task, LoopbackResponse.Ok("hi", keepAlive: true)));
        using var peer = await RawTcp.ConnectAsync(server, Ct);

        await peer.SendAsync(Get, Ct);
        Assert.True(await peer.StaysSilentAsync(Waits.NegativeWindow, Ct), "bytes arrived before the gate opened");
        gate.SetResult();
        var reply = await peer.ReadUntilAsync("hi", Ct);

        Assert.Equal(Encoding.Latin1.GetString(LoopbackResponse.Ok("hi", keepAlive: true).Bytes.Span), reply);
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_faulted_gate_ends_the_connection_without_a_reply_and_is_recorded()
    {
        var gate = NewGate();
        await using var server = LoopbackServer.Start(LoopbackResponse.Gated(gate.Task, LoopbackResponse.Ok("never")));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);

        gate.SetException(new InvalidOperationException("script failure"));
        var reply = await peer.ReadUntilAsync("never", Ct);

        Assert.Equal(string.Empty, reply);
        await Waits.CompletesAsync(server.WaitForConnectionReleasedAsync(0, Ct), Ct);
        Assert.Contains(server.Faults, fault => fault is InvalidOperationException);
    }

    [Fact]
    public async Task A_cancelled_gate_ends_the_connection_without_a_reply_and_is_not_a_fault()
    {
        var gate = NewGate();
        await using var server = LoopbackServer.Start(LoopbackResponse.Gated(gate.Task, LoopbackResponse.Ok("never")));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);

        gate.SetCanceled(Ct);
        var reply = await peer.ReadUntilAsync("never", Ct);

        Assert.Equal(string.Empty, reply);
        await Waits.CompletesAsync(server.WaitForConnectionReleasedAsync(0, Ct), Ct);
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_client_that_leaves_a_gated_reply_releases_its_connection()
    {
        var gate = NewGate();
        await using var server = LoopbackServer.Start(LoopbackResponse.Gated(gate.Task, LoopbackResponse.Ok("never")));
        var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await server.WaitForRequestAsync(0, Ct).WaitAsync(Waits.Bound, Ct);
        var released = server.WaitForConnectionReleasedAsync(0, Ct);
        Assert.True(await Waits.StaysPendingAsync(released, Ct));

        peer.Dispose();

        await Waits.CompletesAsync(released, Ct);
        Assert.False(server.ServerClosedFirst(0));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_keep_alive_connection_serves_the_next_request_after_a_gated_reply()
    {
        var gate = NewGate();
        await using var server = LoopbackServer.Start(
            LoopbackResponse.Gated(gate.Task, LoopbackResponse.Ok("one", keepAlive: true)),
            LoopbackResponse.Ok("two", keepAlive: true));
        using var peer = await RawTcp.ConnectAsync(server, Ct);

        await peer.SendAsync(Get, Ct);
        await server.WaitForRequestAsync(0, Ct).WaitAsync(Waits.Bound, Ct);
        gate.SetResult();
        Assert.EndsWith("one", await peer.ReadUntilAsync("one", Ct), StringComparison.Ordinal);
        await peer.SendAsync(Get, Ct);

        Assert.EndsWith("two", await peer.ReadUntilAsync("two", Ct), StringComparison.Ordinal);
        Assert.Equal(1, server.ConnectionCount);
        // The gate's watch read must not have eaten a byte of the second request.
        Assert.All(server.Requests, request => Assert.Equal("GET /a HTTP/1.1", request.RequestLine));
        Assert.Equal(2, server.Requests.Count);
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task Headers_then_gate_sends_the_head_at_once_and_the_body_only_after_the_gate()
    {
        var gate = NewGate();
        await using var server = LoopbackServer.Start(
            LoopbackResponse.HeadersThenGate(200, [new("X-Probe", "1")], gate.Task, "dribble"u8.ToArray()));
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);

        var head = await peer.ReadUntilAsync("\r\n\r\n", Ct);
        Assert.StartsWith("HTTP/1.1 200 OK\r\n", head, StringComparison.Ordinal);
        Assert.Contains("X-Probe: 1\r\n", head, StringComparison.Ordinal);
        Assert.Contains("Transfer-Encoding: chunked", head, StringComparison.Ordinal);
        Assert.True(await peer.StaysSilentAsync(Waits.NegativeWindow, Ct), "a body byte arrived before the gate opened");

        gate.SetResult();
        var rest = await peer.ReadUntilAsync("0\r\n\r\n", Ct);
        Assert.Equal("7\r\ndribble\r\n0\r\n\r\n", rest);
    }

    [Fact]
    public async Task Headers_then_gate_rejects_an_empty_rest()
    {
        await using var server = LoopbackServer.Start();

        Assert.Throws<ArgumentOutOfRangeException>(() => LoopbackResponse.HeadersThenGate(200, [], Task.CompletedTask, ReadOnlyMemory<byte>.Empty));
        Assert.Empty(server.Requests);
    }

    [Fact]
    public async Task An_aborted_reply_records_the_request_then_resets_the_connection_with_no_response_byte()
    {
        // SocketsHttpHandler transparently retries a request whose connection died before any response byte, so every
        // attempt is aborted: the call fails, and each attempt arrived on a connection of its own.
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Abort());
        using var client = DirectClient();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(server.Url("/a"), Ct));

        Assert.True(error.InnerException is IOException or System.Net.Sockets.SocketException, error.InnerException?.GetType().Name);
        Assert.All(server.Requests, request => Assert.Equal("GET /a HTTP/1.1", request.RequestLine));
        Assert.NotEmpty(server.Requests);
        Assert.Equal(server.Requests.Count, server.ConnectionCount);
        await Waits.CompletesAsync(server.WaitForConnectionReleasedAsync(0, Ct), Ct);
        Assert.True(server.ServerClosedFirst(0));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task An_aborted_reply_writes_zero_bytes()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Abort());
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);

        var read = await Record.ExceptionAsync(async () => Assert.Empty(await peer.ReadExactlyAsync(1, Ct)));

        Assert.True(read is null or IOException, read?.GetType().Name);
    }

    [Fact]
    public async Task A_hanging_reply_never_writes_and_a_client_that_leaves_releases_the_connection()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Hang());
        var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await server.WaitForRequestAsync(0, Ct).WaitAsync(Waits.Bound, Ct);
        Assert.True(await peer.StaysSilentAsync(Waits.NegativeWindow, Ct));
        var released = server.WaitForConnectionReleasedAsync(0, Ct);
        Assert.True(await Waits.StaysPendingAsync(released, Ct));

        peer.Dispose();

        await Waits.CompletesAsync(released, Ct);
        Assert.False(server.ServerClosedFirst(0));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task Disposing_the_server_ends_a_hanging_reply()
    {
        var server = LoopbackServer.Start(LoopbackResponse.Hang());
        using var peer = await RawTcp.ConnectAsync(server, Ct);
        await peer.SendAsync(Get, Ct);
        await server.WaitForRequestAsync(0, Ct).WaitAsync(Waits.Bound, Ct);

        await Waits.CompletesAsync(server.DisposeAsync().AsTask(), Ct);

        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_large_reply_round_trips_with_a_matching_digest()
    {
        const int Bytes = 3 * 1024 * 1024;
        await using var server = LoopbackServer.Start(LoopbackResponse.Large(Bytes, seed: 7));
        using var client = DirectClient();

        using var response = await client.GetAsync(server.Url("/big"), Ct);
        var body = await response.Content.ReadAsByteArrayAsync(Ct);

        Assert.Equal(Bytes, body.Length);
        Assert.Equal(Bytes, response.Content.Headers.ContentLength);
        Assert.Equal(LargeBody.Sha256(Bytes, 7), SHA256.HashData(body));
    }

    [Fact]
    public void Large_is_deterministic_in_its_arguments_and_rejects_nonsense()
    {
        var first = LoopbackResponse.Large(1000, 3);

        Assert.Equal(first.Bytes.ToArray(), LoopbackResponse.Large(1000, 3).Bytes.ToArray());
        Assert.NotEqual(first.Bytes.ToArray(), LoopbackResponse.Large(1000, 4).Bytes.ToArray());
        Assert.NotEqual(LargeBody.Sha256(1000, 3), LargeBody.Sha256(1000, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoopbackResponse.Large(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoopbackResponse.Large(-5, 1));
    }

    [Fact]
    public async Task An_echo_reply_answers_with_the_request_id_header_value()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.EchoId);
        using var client = DirectClient();
        using var tagged = new HttpRequestMessage(HttpMethod.Get, server.Url("/a"));
        tagged.Headers.TryAddWithoutValidation("X-Conformance-Id", "id-42");

        using var withId = await client.SendAsync(tagged, Ct);
        using var without = await client.GetAsync(server.Url("/b"), Ct);

        Assert.Equal("id-42", await withId.Content.ReadAsStringAsync(Ct));
        Assert.Equal(string.Empty, await without.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_keep_alive_reply_leaves_the_connection_open_for_the_next_request()
    {
        await using var server = LoopbackServer.Start(
            LoopbackResponse.Status(204, "No Content", keepAlive: true),
            LoopbackResponse.Ok("x", keepAlive: true));
        using var peer = await RawTcp.ConnectAsync(server, Ct);

        await peer.SendAsync(Get, Ct);
        Assert.StartsWith("HTTP/1.1 204 ", await peer.ReadUntilAsync("\r\n\r\n", Ct), StringComparison.Ordinal);
        await peer.SendAsync(Get, Ct);

        Assert.EndsWith("x", await peer.ReadUntilAsync("x", Ct), StringComparison.Ordinal);
        Assert.Equal(1, server.ConnectionCount);
    }

    [Fact]
    public void Keep_alive_replies_carry_no_connection_close_and_the_default_bytes_are_unchanged()
    {
        Assert.Equal(
            "HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: 1\r\nConnection: close\r\n\r\nx",
            Encoding.Latin1.GetString(LoopbackResponse.Ok("x").Bytes.Span));
        Assert.Equal(
            "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            Encoding.Latin1.GetString(LoopbackResponse.Status(404, "Not Found").Bytes.Span));
        Assert.True(LoopbackResponse.Ok("x").CloseConnection);

        var keepAlive = LoopbackResponse.Ok("x", keepAlive: true);
        Assert.False(keepAlive.CloseConnection);
        Assert.DoesNotContain("Connection:", Encoding.Latin1.GetString(keepAlive.Bytes.Span), StringComparison.Ordinal);
        Assert.Equal(
            "HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: 1\r\n\r\nx",
            Encoding.Latin1.GetString(keepAlive.Bytes.Span));
    }

    [Fact]
    public async Task A_streamed_keep_alive_reply_keeps_its_bytes_and_leaves_the_connection_open()
    {
        static async IAsyncEnumerable<byte[]> One()
        {
            await Task.CompletedTask;
            yield return "a"u8.ToArray();
        }

        var closing = LoopbackResponse.Streamed([new("X", "1")], One());
        var keeping = LoopbackResponse.Streamed([new("X", "1")], One(), keepAlive: true);

        Assert.True(closing.CloseConnection);
        Assert.False(keeping.CloseConnection);
        Assert.Equal(closing.Bytes.ToArray(), keeping.Bytes.ToArray());
        await Task.CompletedTask;
    }
}
