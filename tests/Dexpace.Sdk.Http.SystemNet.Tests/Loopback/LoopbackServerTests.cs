// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Loopback;

/// <summary>
/// Proves the fixture itself: that what it records is what the transport put on the socket, and that what it is
/// scripted to send is what arrives. Phase 1's wire-level Security tests (S1, S2, S3, S9) stand on these.
/// </summary>
[Trait("Category", "Integration")]
public sealed class LoopbackServerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // A caller-supplied client: no proxy (the socket must be the loopback server's), and no redirect following, so
    // the fixture's scripted 3xx reaches the adapter as is. The adapter never disposes a borrowed client.
    private static SystemHttpClient DirectClient() =>
        new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    [Fact]
    public async Task Records_the_request_line_and_header_bytes_exactly_as_sent()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("pong"));
        using var client = DirectClient();
        await using var transport = new SystemNetHttpClient(client);

        // Two spaces, a tab and a trailing-space-free tail: whitespace an HTTP stack could collapse.
        var request = Request.Get(server.Url("/probe?q=a%20b&x=1").ToString())
            .WithHeader("X-Odd-Spacing", "a  b\t c");
        await using var response = await transport.ExecuteAsync(request, Ct);

        Assert.Equal(Status.Ok, response.Status);
        var recorded = Assert.Single(server.Requests);
        Assert.Equal("GET /probe?q=a%20b&x=1 HTTP/1.1", recorded.RequestLine);
        // The value is byte-for-byte what the caller set, and the name arrives in the casing the caller used (HTTP-21:
        // the Headers model keeps the original casing; the fixture reports, it does not judge).
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
        await using var transport = new SystemNetHttpClient(client);

        var request = Request.Post(
            server.Url("/items").ToString(),
            RequestBody.FromString("{\"name\":\"widget\"}", CommonMediaTypes.ApplicationJson));
        await using var response = await transport.ExecuteAsync(request, Ct);

        Assert.Equal(Status.Created, response.Status);
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
        await using var transport = new SystemNetHttpClient(client);

        // A stream body of unknown length goes out chunked.
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("streamed payload"));
        var request = Request.Post(server.Url("/upload").ToString(), RequestBody.FromStream(source));
        await using var response = await transport.ExecuteAsync(request, Ct);

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
        await using var transport = new SystemNetHttpClient(client);

        await using var first = await transport.ExecuteAsync(Request.Get(server.Url("/start").ToString()), Ct);
        await using var second = await transport.ExecuteAsync(Request.Get(server.Url("/next").ToString()), Ct);

        Assert.Equal(302, first.Status.Code);
        Assert.Equal("/next", first.Headers.Get("Location"));
        Assert.Equal("arrived", await second.Body.ReadAsStringAsync(Ct));
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
        await using var transport = new SystemNetHttpClient(client);

        await using var response = await transport.ExecuteAsync(Request.Get(server.Url("/extra").ToString()), Ct);

        Assert.Equal(500, response.Status.Code);
        var fault = Assert.Single(server.Faults);
        Assert.Contains("GET /extra HTTP/1.1", fault.Message, StringComparison.Ordinal);
    }
}
