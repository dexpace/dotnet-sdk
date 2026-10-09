// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Net.Sockets;
using System.Text;
using Dexpace.Sdk.Conformance.Tests.Support;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.RawSocket;

/// <summary>
/// The test-only raw-socket client that is D3's second wire stack (P8a-2). It is held to the contract the kit asserts, so
/// these tests are its own: framing and header mapping on the way out, leniency and laziness on the way in, and the
/// failure taxonomy.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RawSocketHttpClientTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Url(LoopbackServer server, string path = "/p?q=1") => server.Url(path).AbsoluteUri;

    private static RawSocketHttpClient NewClient() => new();

    [Fact]
    public async Task A_get_writes_a_request_line_a_host_the_callers_headers_and_connection_close_and_no_body()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("pong"));
        await using var client = NewClient();

        using var response = await client.ExecuteAsync(Request.Get(Url(server)).WithHeader("X-Probe", "1"), Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Equal("GET /p?q=1 HTTP/1.1", recorded.RequestLine);
        Assert.Equal($"127.0.0.1:{server.BaseUri.Port}", recorded.Header("Host"));
        Assert.Equal("1", recorded.Header("X-Probe"));
        Assert.Equal("close", recorded.Header("Connection"));
        Assert.Null(recorded.Header("Content-Length"));
        Assert.Null(recorded.Header("Transfer-Encoding"));
        Assert.Empty(recorded.Body.ToArray());
        Assert.Equal("pong", await response.Body.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_bytes_body_is_sent_with_its_content_length_and_content_type()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        await using var client = NewClient();

        using var response = await client.ExecuteAsync(Request.Post(Url(server), RequestBody.FromString("hello", CommonMediaTypes.TextPlain)), Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Equal("5", recorded.Header("Content-Length"));
        Assert.StartsWith("text/plain", recorded.Header("Content-Type"), StringComparison.Ordinal);
        Assert.Equal("hello", recorded.BodyText);
    }

    [Fact]
    public async Task An_unknown_length_stream_body_goes_out_chunked_with_correct_framing()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        await using var client = NewClient();
        using var source = new CountingStream(new MemoryStream(Encoding.UTF8.GetBytes("streamed payload")));

        using var response = await client.ExecuteAsync(Request.Post(Url(server), RequestBody.FromStream(source)), Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Equal("chunked", recorded.Header("Transfer-Encoding"));
        Assert.Null(recorded.Header("Content-Length"));
        Assert.Equal("streamed payload", recorded.BodyText);
        Assert.Empty(server.Faults);
    }

    [Theory]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("PATCH", true)]
    [InlineData("DELETE", false)]
    [InlineData("OPTIONS", false)]
    public async Task A_body_less_request_carries_a_zero_length_body_only_where_the_method_expects_one(string method, bool expectsLength)
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        await using var client = NewClient();

        using var response = await client.ExecuteAsync(Request.Create(Method.Of(method), Url(server)), Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.Equal(expectsLength ? "0" : null, recorded.Header("Content-Length"));
        Assert.Empty(recorded.Body.ToArray());
    }

    [Fact]
    public async Task The_framing_headers_the_caller_sets_are_not_written()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok());
        await using var client = NewClient();
        var request = Request.Post(Url(server), RequestBody.FromString("abcde", CommonMediaTypes.TextPlain))
            .WithHeader("Host", "evil.example")
            .WithHeader("Content-Length", "9999")
            .WithHeader("Transfer-Encoding", "chunked")
            .WithHeader("Connection", "keep-alive")
            .WithHeader("Keep-Alive", "timeout=5")
            .WithHeader("Upgrade", "h2c")
            .WithHeader("TE", "trailers")
            .WithHeader("Expect", "100-continue")
            .WithHeader("X-Pass", "kept");

        using var response = await client.ExecuteAsync(request, Ct);

        var recorded = Assert.Single(server.Requests);
        Assert.DoesNotContain("evil.example", recorded.RawText, StringComparison.Ordinal);
        Assert.Equal($"127.0.0.1:{server.BaseUri.Port}", recorded.Header("Host"));
        Assert.Equal("5", recorded.Header("Content-Length"));
        Assert.Equal("close", recorded.Header("Connection"));
        Assert.Null(recorded.Header("Transfer-Encoding"));
        Assert.Null(recorded.Header("Keep-Alive"));
        Assert.Null(recorded.Header("Upgrade"));
        Assert.Null(recorded.Header("TE"));
        Assert.Null(recorded.Header("Expect"));
        Assert.Equal("kept", recorded.Header("X-Pass"));
    }

    [Fact]
    public async Task The_callers_content_type_wins_and_the_bodys_is_used_only_when_absent()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok(), LoopbackResponse.Ok());
        await using var client = NewClient();
        var body = RequestBody.FromString("{}", CommonMediaTypes.ApplicationJson);

        using var callers = await client.ExecuteAsync(Request.Post(Url(server), body).WithHeader("Content-Type", "application/vnd.x+json"), Ct);
        using var bodys = await client.ExecuteAsync(Request.Post(Url(server), body), Ct);

        Assert.Equal("application/vnd.x+json", server.Requests[0].Header("Content-Type"));
        Assert.StartsWith("application/json", server.Requests[1].Header("Content-Type"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_response_carries_its_status_reason_protocol_headers_and_a_content_length_body()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Status(201, "Made It", [new("X-Trace", "abc"), new("Content-Type", "text/plain; charset=utf-8")], "body"));
        await using var client = NewClient();

        using var response = await client.ExecuteAsync(Request.Get(Url(server)), Ct);

        Assert.Equal(201, response.Status.Code);
        Assert.Equal("Made It", response.ReasonPhrase);
        Assert.Equal(Protocol.Http11, response.Protocol);
        Assert.Equal("abc", response.Headers.Get("x-trace"));
        Assert.Equal(4, response.Body.ContentLength);
        Assert.Equal("text/plain", response.Body.ContentType?.ToString().Split(';')[0]);
        Assert.Equal("body", await response.Body.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task A_chunked_body_and_a_close_delimited_body_are_read_to_their_end()
    {
        static async IAsyncEnumerable<byte[]> Two()
        {
            await Task.CompletedTask;
            yield return "one"u8.ToArray();
            yield return "two"u8.ToArray();
        }

        await using var server = LoopbackServer.Start(
            LoopbackResponse.Streamed([], Two()),
            LoopbackResponse.Raw("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nuntil-close"));
        await using var client = NewClient();

        using var chunked = await client.ExecuteAsync(Request.Get(Url(server)), Ct);
        using var closeDelimited = await client.ExecuteAsync(Request.Get(Url(server)), Ct);

        Assert.Equal("onetwo", await chunked.Body.ReadAsStringAsync(Ct));
        Assert.Equal(-1, chunked.Body.ContentLength);
        Assert.Equal("until-close", await closeDelimited.Body.ReadAsStringAsync(Ct));
        Assert.Equal(-1, closeDelimited.Body.ContentLength);
    }

    [Theory]
    [InlineData("HEAD", 200)]
    [InlineData("GET", 204)]
    [InlineData("GET", 304)]
    public async Task A_head_a_204_and_a_304_have_no_body_to_read(string method, int status)
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw($"HTTP/1.1 {status} X\r\nContent-Length: 5\r\nConnection: close\r\n\r\n"));
        await using var client = NewClient();

        using var response = await client.ExecuteAsync(Request.Create(Method.Of(method), Url(server)), Ct);

        Assert.Empty(await response.Body.ReadAsBytesAsync(Ct));
        await Waits.CompletesAsync(server.WaitForConnectionReleasedAsync(0, Ct), Ct);
    }

    [Fact]
    public async Task Malformed_inbound_fields_are_downgraded_and_the_response_survives()
    {
        const string Reply = "HTTP/1.1 200 OK\r\nContent-Type: text/plain; foo\r\nContent-Length: abc\r\nBad Name: x\r\nX-Control: a\u0001b\r\nX-Obs: café\r\nX-Fine: ok\r\nConnection: close\r\n\r\nhello";
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw(Reply));
        await using var client = NewClient();

        using var response = await client.ExecuteAsync(Request.Get(Url(server)), Ct);

        Assert.Null(response.Body.ContentType);
        Assert.Equal(-1, response.Body.ContentLength);
        Assert.False(response.Headers.Contains("Bad Name"));
        Assert.False(response.Headers.Contains("X-Control"));
        Assert.Equal("café", response.Headers.Get("X-Obs"));
        Assert.Equal("ok", response.Headers.Get("X-Fine"));
        Assert.Equal("hello", await response.Body.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task The_call_returns_after_the_head_and_the_body_is_lazy()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<byte[]> Gated()
        {
            yield return "first"u8.ToArray();
            await gate.Task;
            yield return "second"u8.ToArray();
        }

        await using var server = LoopbackServer.Start(LoopbackResponse.Streamed([], Gated()));
        await using var client = NewClient();

        using var response = await client.ExecuteAsync(Request.Get(Url(server)), Ct);
        await using var stream = await response.Body.OpenReadAsync(Ct);
        var buffer = new byte[5];
        var read = await stream.ReadAsync(buffer, Ct).AsTask().WaitAsync(Waits.Bound, Ct);

        Assert.Equal("first", Encoding.ASCII.GetString(buffer, 0, read));
        Assert.False(gate.Task.IsCompleted);
        gate.SetResult();
    }

    [Fact]
    public async Task Disposing_the_response_closes_the_socket()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("x", keepAlive: true));
        await using var client = NewClient();
        var response = await client.ExecuteAsync(Request.Get(Url(server)), Ct);
        var released = server.WaitForConnectionReleasedAsync(0, Ct);
        Assert.True(await Waits.StaysPendingAsync(released, Ct));

        response.Dispose();

        await Waits.CompletesAsync(released, Ct);
        Assert.False(server.ServerClosedFirst(0));
    }

    [Fact]
    public async Task A_refused_port_a_reset_and_a_close_before_the_status_line_are_retryable_service_request_failures()
    {
        await using var client = NewClient();
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Abort());
        var closed = LoopbackServer.Start(_ => LoopbackResponse.Raw(string.Empty));
        await using var closedScope = closed;
        var port = FreePort.Release();

        var refused = await Assert.ThrowsAsync<ServiceRequestException>(() => client.ExecuteAsync(Request.Get($"http://127.0.0.1:{port}/"), Ct));
        var reset = await Assert.ThrowsAsync<ServiceRequestException>(() => client.ExecuteAsync(Request.Get(Url(server)), Ct));
        var early = await Assert.ThrowsAsync<ServiceRequestException>(() => client.ExecuteAsync(Request.Get(Url(closed)), Ct));

        Assert.All(new[] { refused, reset, early }, error => Assert.True(error.IsRetryable));
        Assert.IsType<SocketException>(refused.InnerException);
        Assert.True(reset.InnerException is IOException or SocketException, reset.InnerException?.GetType().Name);
        Assert.IsType<IOException>(early.InnerException);
    }

    [Fact]
    public async Task A_connection_that_closes_mid_body_fails_the_body_read_with_an_IOException()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw("HTTP/1.1 200 OK\r\nContent-Length: 100\r\nConnection: close\r\n\r\nshort"));
        await using var client = NewClient();

        using var response = await client.ExecuteAsync(Request.Get(Url(server)), Ct);

        await Assert.ThrowsAsync<IOException>(() => response.Body.ReadAsBytesAsync(Ct));
    }

    [Fact]
    public async Task A_cancelled_caller_token_gives_cancellation_carrying_that_token_and_closes_the_socket()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Hang());
        await using var client = NewClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var call = client.ExecuteAsync(Request.Get(Url(server)), cts.Token);
        await server.WaitForRequestAsync(0, Ct).WaitAsync(Waits.Bound, Ct);

        await cts.CancelAsync();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(Waits.Bound, Ct));
        Assert.Equal(cts.Token, error.CancellationToken);
        await Waits.CompletesAsync(server.WaitForConnectionReleasedAsync(0, Ct), Ct);
        Assert.False(server.ServerClosedFirst(0));
    }

    [Fact]
    public async Task A_token_signalled_before_the_call_cancels_it()
    {
        await using var client = NewClient();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ExecuteAsync(Request.Get("http://127.0.0.1:1/"), cts.Token));

        Assert.Equal(cts.Token, error.CancellationToken);
    }

    [Fact]
    public async Task A_per_call_timeout_that_elapses_is_a_retryable_timeout_and_not_a_cancellation()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Hang());
        await using var client = NewClient();

        var error = await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() =>
            client.ExecuteAsync(Request.Get(Url(server)), new RequestOptions { Timeout = TimeSpan.FromMilliseconds(200) }, Ct).WaitAsync(Waits.Bound, Ct));

        Assert.True(error.IsRetryable);
        Assert.False(Ct.IsCancellationRequested);
    }

    [Fact]
    public async Task A_sub_resolution_timeout_still_times_out_instead_of_hanging()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Hang());
        await using var client = NewClient();

        await Assert.ThrowsAsync<ServiceRequestTimeoutException>(() =>
            client.ExecuteAsync(Request.Get(Url(server)), new RequestOptions { Timeout = TimeSpan.FromTicks(1000) }, Ct).WaitAsync(Waits.Bound, Ct));
    }

    [Fact]
    public async Task An_invalid_status_line_is_a_service_response_failure()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw("NOT HTTP\r\n\r\n"));
        await using var client = NewClient();

        await Assert.ThrowsAsync<ServiceResponseException>(() => client.ExecuteAsync(Request.Get(Url(server)), Ct));
    }

    [Fact]
    public async Task A_redirect_is_returned_not_followed_and_a_failed_send_never_re_sends_its_body()
    {
        await using var other = LoopbackServer.Start(LoopbackResponse.Ok());
        await using var server = LoopbackServer.Start(request => request.Method == "GET"
            ? LoopbackResponse.Redirect(302, other.Url("/t").AbsoluteUri)
            : LoopbackResponse.Abort());
        await using var client = NewClient();
        using var source = new CountingStream(new MemoryStream(new byte[1024]));

        using var redirect = await client.ExecuteAsync(Request.Get(Url(server)), Ct);
        await Assert.ThrowsAsync<ServiceRequestException>(() => client.ExecuteAsync(Request.Post(Url(server), RequestBody.FromStream(source, contentLength: 1024)), Ct));

        Assert.Equal(302, redirect.Status.Code);
        Assert.Empty(other.Requests);
        Assert.Equal(1024, source.BytesRead);
    }

    [Fact]
    public async Task Disposal_is_idempotent_and_the_client_keeps_working_after_it()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Ok());
        var client = NewClient();

        await client.DisposeAsync();
        await client.DisposeAsync();
        using var response = await client.ExecuteAsync(Request.Get(Url(server)), Ct);

        Assert.Equal(200, response.Status.Code);
    }

    [Fact]
    public async Task Sixty_four_parallel_calls_through_one_instance_do_not_cross()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.EchoId);
        await using var client = NewClient();

        var bodies = await Task.WhenAll(Enumerable.Range(0, 64).Select(async i =>
        {
            using var response = await client.ExecuteAsync(Request.Get(Url(server)).WithHeader("X-Conformance-Id", $"id-{i}"), Ct);
            return (Expected: $"id-{i}", Actual: await response.Body.ReadAsStringAsync(Ct));
        }));

        Assert.All(bodies, pair => Assert.Equal(pair.Expected, pair.Actual));
    }

    [Fact]
    public async Task Argument_errors_are_delivered_through_the_task()
    {
        await using var client = NewClient();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExecuteAsync(null!, RequestOptions.Empty, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ExecuteAsync(Request.Get("http://127.0.0.1:1/"), null!, Ct));
    }
}
