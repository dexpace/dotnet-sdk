// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Wire;

/// <summary>
/// Proves the fixture itself: that what it records is what a client put on the socket, and that what it is scripted to
/// send is what arrives. Phase 1's wire-level Security tests (S1, S2, S3, S9) stand on these. The client here is a plain
/// <c>HttpClient</c>: the fixture's claims are about the wire, so no SDK transport is needed (moved from the SystemNet
/// suite when phase 8a promoted the fixture into the conformance kit).
/// </summary>
[Trait("Category", "Integration")]
public sealed class LoopbackServerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // No proxy (the socket must be the loopback server's), and no redirect following, so the fixture's scripted 3xx
    // reaches the test as is.
    private static HttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    [Fact]
    public async Task Records_the_request_line_and_header_bytes_exactly_as_sent()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("pong"));
        using var client = DirectClient();

        // Two spaces, a tab and a trailing-space-free tail: whitespace an HTTP stack could collapse.
        using var request = new HttpRequestMessage(HttpMethod.Get, server.Url("/probe?q=a%20b&x=1"));
        request.Headers.TryAddWithoutValidation("X-Odd-Spacing", "a  b\t c");
        using var response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recorded = Assert.Single(server.Requests);
        Assert.Equal("GET /probe?q=a%20b&x=1 HTTP/1.1", recorded.RequestLine);
        // The value is byte-for-byte what the caller set, and the name arrives in the casing the caller used (the fixture
        // reports, it does not judge).
        Assert.Contains("X-Odd-Spacing: a  b\t c", recorded.HeaderLines);
        Assert.Equal($"127.0.0.1:{server.BaseUri.Port}", recorded.Header("Host"));

        // The raw record is the request line, every header line, and the empty line, CRLF-terminated, and nothing
        // else: the parsed views are derived from it, not the other way round.
        var expectedHead = string.Join("\r\n", [recorded.RequestLine, .. recorded.HeaderLines]) + "\r\n\r\n";
        Assert.Equal(expectedHead, recorded.RawText);
        Assert.Equal(Encoding.Latin1.GetBytes(expectedHead), recorded.Raw.ToArray());
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task Records_a_content_length_body_after_the_head()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Status(201, "Created"));
        using var client = DirectClient();

        using var content = new StringContent("{\"name\":\"widget\"}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(server.Url("/items"), content, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var recorded = Assert.Single(server.Requests);
        Assert.Equal("POST", recorded.Method);
        Assert.Equal("17", recorded.Header("Content-Length"));
        Assert.Equal("{\"name\":\"widget\"}", recorded.BodyText);
        Assert.EndsWith("\r\n\r\n{\"name\":\"widget\"}", recorded.RawText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removes_chunk_framing_from_the_body_but_keeps_it_in_the_raw_record()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        using var client = DirectClient();

        // Declaring the transfer coding makes the client chunk the body instead of computing a Content-Length.
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("streamed payload"));
        using var request = new HttpRequestMessage(HttpMethod.Post, server.Url("/upload")) { Content = new StreamContent(source) };
        request.Headers.TransferEncodingChunked = true;
        using var response = await client.SendAsync(request, Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Equal("chunked", recorded.Header("Transfer-Encoding"));
        Assert.Equal("streamed payload", recorded.BodyText);
        Assert.EndsWith("0\r\n\r\n", recorded.RawText, StringComparison.Ordinal);
        Assert.Contains("streamed payload", recorded.RawText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replays_a_scripted_redirect_and_then_the_next_reply()
    {
        await using var server = LoopbackServer.Start(
            LoopbackResponse.Redirect(302, "/next"),
            LoopbackResponse.Ok("arrived"));
        using var client = DirectClient();

        using var first = await client.GetAsync(server.Url("/start"), Ct);
        using var second = await client.GetAsync(server.Url("/next"), Ct);

        Assert.Equal(HttpStatusCode.Found, first.StatusCode);
        Assert.Equal("/next", first.Headers.Location?.OriginalString);
        Assert.Equal("arrived", await second.Content.ReadAsStringAsync(Ct));
        Assert.Collection(
            server.Requests,
            request => Assert.Equal("GET /start HTTP/1.1", request.RequestLine),
            request => Assert.Equal("GET /next HTTP/1.1", request.RequestLine));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task Writes_a_scripted_reply_byte_for_byte_even_when_it_is_malformed()
    {
        // A control byte in a header value, a malformed media type, and a bare-LF line: what a conforming server
        // refuses to emit (design §9.3, TRANSPORT-14), and what phase 1's S9 test needs to send.
        const string Reply =
            "HTTP/1.1 200 OK\r\nX-Control: a\u0001b\r\nContent-Type: text/plain; foo\nContent-Length: 2\r\n\r\nhi";
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw(Reply));
        using var socket = new TcpClient();
        await socket.ConnectAsync(IPAddress.Loopback, server.BaseUri.Port, Ct);
        var stream = socket.GetStream();

        const string RawRequest = "GET /raw HTTP/1.1\r\nHost: anything\r\nX-Spaced:   v  \r\n\r\n";
        await stream.WriteAsync(Encoding.Latin1.GetBytes(RawRequest), Ct);
        using var received = new MemoryStream();
        await stream.CopyToAsync(received, Ct);

        Assert.Equal(Encoding.Latin1.GetBytes(Reply), received.ToArray());
        var recorded = Assert.Single(server.Requests);
        Assert.Equal(RawRequest, recorded.RawText);
        Assert.Contains("X-Spaced:   v  ", recorded.HeaderLines);
        Assert.Equal("v", recorded.Header("X-Spaced"));
    }

    public static TheoryData<string, string> MalformedFraming => new()
    {
        // head sent, and the raw bytes the fixture must have kept when framing failed
        { "POST /a HTTP/1.1\r\nHost: x\r\nContent-Length: abc\r\n\r\n", "Content-Length: abc\r\n\r\n" },
        { "POST /a HTTP/1.1\r\nHost: x\r\nContent-Length: 99999999999999999999\r\n\r\nbody", "99999999999999999999\r\n\r\n" },
        { "POST /a HTTP/1.1\r\nHost: x\r\nContent-Length: 1, 1\r\n\r\nx", "Content-Length: 1, 1\r\n\r\n" },
        { "POST /a HTTP/1.1\r\nHost: x\r\nContent-Length: 1\r\nContent-Length: 2\r\n\r\nxy", "Content-Length: 2\r\n\r\n" },
        { "POST /a HTTP/1.1\r\nHost: x\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\nhello\r\n0\r\n\r\n", "\r\n\r\nzz\r\n" },
        { "POST /a HTTP/1.1\r\nHost: x\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFF\r\n", "\r\n\r\nFFFFFFFF\r\n" },
    };

    [Theory]
    [MemberData(nameof(MalformedFraming))]
    public async Task Records_a_request_with_malformed_framing_answers_400_and_disposes_cleanly(
        string rawRequest,
        string rawTail)
    {
        var server = LoopbackServer.Start(LoopbackResponse.Ok("never sent"));
        string reply;
        using (var socket = new TcpClient())
        {
            await socket.ConnectAsync(IPAddress.Loopback, server.BaseUri.Port, Ct);
            var stream = socket.GetStream();
            await stream.WriteAsync(Encoding.Latin1.GetBytes(rawRequest), Ct);
            using var received = new MemoryStream();
            await stream.CopyToAsync(received, Ct);
            reply = Encoding.Latin1.GetString(received.ToArray());
        }

        var fault = Assert.IsType<MalformedRequestException>(Assert.Single(server.Faults));
        Assert.StartsWith("POST /a HTTP/1.1\r\nHost: x\r\n", fault.RawText, StringComparison.Ordinal);
        Assert.EndsWith(rawTail, fault.RawText, StringComparison.Ordinal);
        var recorded = Assert.Single(server.Requests);
        Assert.Equal("POST /a HTTP/1.1", recorded.RequestLine);
        Assert.Equal(fault.RawText, recorded.RawText);
        Assert.StartsWith("HTTP/1.1 400 ", reply, StringComparison.Ordinal);
        Assert.Null(await Record.ExceptionAsync(async () => await server.DisposeAsync()));
        Assert.Single(server.Faults);
    }

    [Fact]
    public async Task Records_a_fault_and_answers_500_when_the_script_runs_out()
    {
        await using var server = LoopbackServer.Start();
        using var client = DirectClient();

        using var response = await client.GetAsync(server.Url("/extra"), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var fault = Assert.Single(server.Faults);
        Assert.Contains("GET /extra HTTP/1.1", fault.Message, StringComparison.Ordinal);
    }

    // Yields the first chunk at once, the second only after the gate opens: a server that is still writing.
    private static async IAsyncEnumerable<byte[]> GatedChunks(Task gate, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "one"u8.ToArray();
        await gate.WaitAsync(cancellationToken);
        yield return "two"u8.ToArray();
    }

    private static async Task<string> ReadSomeAsync(Stream stream)
    {
        var buffer = new byte[64];
        var read = await stream.ReadAsync(buffer, Ct);
        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    [Fact]
    public async Task A_streamed_reply_delivers_each_chunk_as_the_test_releases_it()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = LoopbackServer.Start(
            LoopbackResponse.Streamed([new("Content-Type", "text/event-stream")], GatedChunks(gate.Task, Ct)));
        using var client = DirectClient();

        using var response = await client.GetAsync(server.Url("/stream"), HttpCompletionOption.ResponseHeadersRead, Ct);
        await using var body = await response.Content.ReadAsStreamAsync(Ct);

        // The first chunk arrives while the server is still gated on the second: the reply is genuinely incremental.
        Assert.Equal("one", await ReadSomeAsync(body).WaitAsync(TimeSpan.FromSeconds(10), Ct));
        Assert.False(gate.Task.IsCompleted);
        gate.SetResult();
        Assert.Equal("two", await ReadSomeAsync(body).WaitAsync(TimeSpan.FromSeconds(10), Ct));
        Assert.Equal(string.Empty, await ReadSomeAsync(body));
        Assert.Empty(server.Faults);
    }

    [Fact]
    public async Task A_streamed_reply_ends_with_the_chunked_terminator()
    {
        await using var server = LoopbackServer.Start(
            LoopbackResponse.Streamed([new("Content-Type", "text/event-stream")], GatedChunks(Task.CompletedTask, Ct)));
        using var client = DirectClient();

        using var response = await client.GetAsync(server.Url("/stream"), HttpCompletionOption.ResponseHeadersRead, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal("onetwo", text);
        Assert.True(response.Headers.TransferEncodingChunked);
        Assert.Empty(server.Faults);
    }
}
