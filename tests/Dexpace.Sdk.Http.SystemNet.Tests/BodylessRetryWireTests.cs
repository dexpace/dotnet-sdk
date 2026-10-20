// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-2 (phase 8b, ruling P8b-T2; phase 11, P11-12, design section 10 entry 33): <c>SocketsHttpHandler</c> has no switch for its
/// own connection-failure retry, and re-sends a request with no content whose connection ends before any response byte up to three more
/// times (runtime 10.0, <c>HttpConnection.SendAsync</c>: <c>_canRetry</c> is set only when the first response read returns zero bytes and the
/// request has no content, or an Expect: 100-continue). Two measures close it on a handler the SDK builds
/// (<see cref="SystemNetHttpClient.CreatePrimaryHandler"/>, which the owned construction and the DI named client both use). A body-less
/// <c>POST</c>, <c>PUT</c>, <c>PATCH</c> or extension method is given a zero-length content, which the native client already writes as
/// <c>Content-Length: 0</c>, so the retry cannot fire. <c>GET</c>, <c>HEAD</c>, <c>OPTIONS</c>, <c>DELETE</c> and <c>TRACE</c> are idempotent,
/// and attaching content to them would break TRANSPORT-26 or RFC 9110 section 8.6, so the handler's plaintext stream filter turns an end of
/// stream that arrives while a request awaits its first response byte into the <see cref="IOException"/> the runtime raises otherwise; the
/// retry is never offered. A caller's own handler or <c>HttpClient</c> keeps its native client's retry: it is the caller's
/// (design section 11 item 71), which this class pins so the scope is a decision and not an accident. The server reads each request and then
/// closes in order (an end of stream, never <c>Abort</c>, whose FIN and reset the platforms read differently).
/// </summary>
[Trait("Category", "Integration")]
public sealed class BodylessRetryWireTests
{
    private const string Warm = "/warm";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LoopbackServer StartClosingServer() => LoopbackServer.Start(request => request.Target == Warm
        ? LoopbackResponse.Ok("warm", keepAlive: true)
        : LoopbackResponse.Raw(ReadOnlySpan<byte>.Empty, closeConnection: true));

    // The five methods that carry no content in the SDK's model and that the transport cannot give a zero-length content.
    public static TheoryData<string, string, string> SdkBuilt()
    {
        var rows = new TheoryData<string, string, string>();
        foreach (var construction in new[] { "owned", "primary-handler" })
        {
            foreach (var method in new[] { "GET", "HEAD", "OPTIONS", "DELETE", "TRACE" })
            {
                foreach (var face in new[] { "async", "sync" })
                {
                    rows.Add(method, face, construction);
                }
            }
        }

        return rows;
    }

    public static TheoryData<string, string> CallerBuilt() =>
        new() { { "GET", "async" }, { "GET", "sync" }, { "HEAD", "async" }, { "OPTIONS", "async" }, { "DELETE", "async" }, { "DELETE", "sync" }, { "TRACE", "async" } };

    public static TheoryData<string, string> NotResent() =>
        new()
        {
            { "POST", "async" }, { "POST", "sync" }, { "PUT", "async" }, { "PUT", "sync" }, { "PATCH", "async" }, { "PATCH", "sync" },
            { "XYZ", "async" }, { "XYZ", "sync" },
        };

    // "owned" is the SDK-managed construction; "primary-handler" is a handler-taking construction over the handler the SDK builds for it
    // (what the DI package does); "caller-handler" is a handler the caller built, which the transport only borrows.
    private static async Task<int> ArrivalsAsync(string method, string face, string path, string construction = "owned")
    {
        await using var server = StartClosingServer();
        SocketsHttpHandler? handler = construction switch
        {
            "owned" => null,
            "primary-handler" => SystemNetHttpClient.CreatePrimaryHandler(new SystemNetHttpClientOptions()),
            _ => new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false },
        };
        try
        {
            await using var transport = handler is null ? new SystemNetHttpClient() : new SystemNetHttpClient(handler, new SystemNetHttpClientOptions());
            var request = Request.Create(Method.Of(method), server.Url(path).AbsoluteUri);

            // A warmed keep-alive connection first: the retry fires on a pooled connection and on a new one alike.
            using (var warm = await transport.ExecuteAsync(Request.Get(server.Url(Warm).AbsoluteUri), RequestOptions.Empty, Ct))
            {
                _ = await warm.Body.ReadAsBytesAsync(Ct);
            }

            var failure = face == "async"
                ? await Assert.ThrowsAsync<ServiceRequestException>(() => transport.ExecuteAsync(request, RequestOptions.Empty, Ct))
                : await Assert.ThrowsAsync<ServiceRequestException>(() => Task.Run(() => transport.Execute(request, RequestOptions.Empty, Ct), Ct));
            Assert.True(failure.IsRetryable);

            // On a handler the SDK built the failure keeps the error the runtime gave it before the filter: the response ended, unanswered.
            Assert.Equal(
                construction != "caller-handler",
                failure.InnerException is HttpRequestException { HttpRequestError: HttpRequestError.ResponseEnded, InnerException: HttpIOException { Message: var message } }
                    && message.Contains("before any byte", StringComparison.Ordinal));
            return server.Requests.Count(recorded => recorded.Target == path);
        }
        finally
        {
            handler?.Dispose();
        }
    }

    [Theory]
    [MemberData(nameof(NotResent))]
    public async Task A_body_less_request_that_may_carry_content_reaches_the_server_exactly_once(string method, string face)
    {
        var arrivals = await ArrivalsAsync(method, face, "/once");

        Assert.Equal(1, arrivals);
    }

    [Theory]
    [MemberData(nameof(SdkBuilt))]
    public async Task A_bodyless_request_reaches_the_server_once_on_an_sdk_built_handler(string method, string face, string construction)
    {
        var arrivals = await ArrivalsAsync(method, face, "/once", construction);

        Assert.Equal(1, arrivals);
    }

    [Theory]
    [MemberData(nameof(CallerBuilt))]
    public async Task A_caller_handler_keeps_its_native_retry(string method, string face)
    {
        var arrivals = await ArrivalsAsync(method, face, "/residual", "caller-handler");

        Assert.True(
            arrivals > 1,
            $"A body-less {method} used to reach the server {arrivals} times after its connection ended unanswered on a handler the caller built; the native client's own retry is no longer observed here. "
            + "If the runtime changed, TRANSPORT-2 may be met for a caller's handler too: delete the handler subject's waiver (design section 11 item 71).");
    }

    // The filter must change nothing a working exchange sees. These rows are the paths on which a verdict could go wrong: a response that
    // ends with its connection, a reused keep-alive connection across methods and a request body, and a body read to its end.
    [Theory]
    [InlineData("async")]
    [InlineData("sync")]
    public async Task A_response_delimited_by_the_end_of_the_connection_is_read_to_its_end(string face)
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Raw("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nhello, connection", closeConnection: true));
        await using var transport = new SystemNetHttpClient();
        var request = Request.Get(server.Url("/delimited").AbsoluteUri);

        using var response = face == "async"
            ? await transport.ExecuteAsync(request, RequestOptions.Empty, Ct)
            : await Task.Run(() => transport.Execute(request, RequestOptions.Empty, Ct), Ct);

        Assert.Equal("hello, connection", face == "async" ? await response.Body.ReadAsStringAsync(Ct) : response.Body.ReadAsString(Ct));
    }

    [Theory]
    [InlineData("async")]
    [InlineData("sync")]
    public async Task One_keep_alive_connection_serves_every_method_and_a_request_body_in_turn(string face)
    {
        // A HEAD answer carries the length of the body it does not send: a body written after it would be left on the connection.
        await using var server = LoopbackServer.Start(request => request.Method == "HEAD"
            ? LoopbackResponse.Raw("HTTP/1.1 200 OK\r\nContent-Length: 7\r\n\r\n", closeConnection: false)
            : LoopbackResponse.Ok("ok:" + request.Method, keepAlive: true));
        await using var transport = new SystemNetHttpClient();
        var url = server.Url("/reuse").AbsoluteUri;
        var upload = RequestBody.FromString(new string('x', 200_000));
        var requests = new[]
        {
            Request.Get(url), Request.Create(Method.Head, url), Request.Create(Method.Delete, url), Request.Post(url, upload),
            Request.Create(Method.Post, url), Request.Get(url), Request.Create(Method.Options, url),
        };

        foreach (var request in requests)
        {
            using var response = face == "async"
                ? await transport.ExecuteAsync(request, RequestOptions.Empty, Ct)
                : await Task.Run(() => transport.Execute(request, RequestOptions.Empty, Ct), Ct);
            var text = face == "async" ? await response.Body.ReadAsStringAsync(Ct) : response.Body.ReadAsString(Ct);

            Assert.Equal(200, response.Status.Code);
            Assert.Equal(request.Method == Method.Head ? string.Empty : "ok:" + request.Method.Name, text);
        }

        Assert.Equal(requests.Length, server.Requests.Count);
        Assert.Equal(1, server.ConnectionCount);
    }

    [Fact]
    public async Task Concurrent_exchanges_over_pooled_connections_never_see_a_false_end_of_stream()
    {
        // Many callers share the pool, so connections are reused and opened in every interleaving; none may be told its response ended early.
        await using var server = LoopbackServer.Start(request => LoopbackResponse.Ok("ok:" + request.Method, keepAlive: true));
        await using var transport = new SystemNetHttpClient();
        var url = server.Url("/pool").AbsoluteUri;

        var results = await Task.WhenAll(Enumerable.Range(0, 24).Select(worker => Task.Run(
            async () =>
            {
                for (var i = 0; i < 12; i++)
                {
                    var request = (worker + i) % 3 == 0 ? Request.Post(url, RequestBody.FromString("payload " + i)) : Request.Get(url);
                    using var response = await transport.ExecuteAsync(request, RequestOptions.Empty, Ct);
                    if (await response.Body.ReadAsStringAsync(Ct) != "ok:" + request.Method.Name)
                    {
                        return false;
                    }
                }

                return true;
            },
            Ct)));

        Assert.All(results, Assert.True);
        Assert.Equal(24 * 12, server.Requests.Count);
    }

    [Fact]
    public async Task The_wire_bytes_of_a_body_less_post_are_what_the_native_client_writes_for_no_content()
    {
        await using var ours = LoopbackServer.Start(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        await using var native = LoopbackServer.Start(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        await using var transport = new SystemNetHttpClient();
        using var plain = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

        await using (await transport.ExecuteAsync(Request.Create(Method.Post, ours.Url("/x").AbsoluteUri), RequestOptions.Empty, Ct))
        {
        }

        using (await plain.PostAsync(native.Url("/x"), content: null, Ct))
        {
        }

        string Normalised(LoopbackServer server) =>
            Assert.Single(server.Requests).RawText.Replace(server.BaseUri.Authority, "HOST", StringComparison.Ordinal);

        Assert.Equal(Normalised(native), Normalised(ours));
        Assert.Contains("Content-Length: 0\r\n", Normalised(ours), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Expect_100_continue_on_a_body_less_post_is_dropped_and_logged_and_does_not_bring_the_retry_back()
    {
        var logger = new RecordingLogger();
        await using var server = StartClosingServer();
        await using var transport = new SystemNetHttpClient(logger);
        var request = Request.Create(Method.Post, server.Url("/expect").AbsoluteUri).WithHeader("Expect", "100-continue");

        await Assert.ThrowsAsync<ServiceRequestException>(() => transport.ExecuteAsync(request, RequestOptions.Empty, Ct));

        Assert.Equal(1, server.Requests.Count(recorded => recorded.Target == "/expect"));
        Assert.Empty(server.Requests[0].HeaderValues("Expect"));
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Debug
                && entry.EventId.Id == DexpaceLogEvents.TransportFramingHeaderDroppedId
                && entry.Message.Contains("'Expect'", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // Which methods get a zero-length content, observed at the handler.

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Seen { get; private set; }

        public long? ContentLength { get; private set; }

        public bool HadContent { get; private set; }

        public bool HadExpect { get; private set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => Record(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Record(request));

        private HttpResponseMessage Record(HttpRequestMessage request)
        {
            Seen = request;
            HadContent = request.Content is not null;
            HadExpect = request.Headers.Contains("Expect");
            ContentLength = request.Content?.Headers.ContentLength;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Theory]
    [InlineData("POST", true)]
    [InlineData("PUT", true)]
    [InlineData("PATCH", true)]
    [InlineData("XYZ", true)]
    [InlineData("GET", false)]
    [InlineData("HEAD", false)]
    [InlineData("OPTIONS", false)]
    [InlineData("DELETE", false)]
    [InlineData("TRACE", false)]
    public async Task Only_the_methods_the_native_client_frames_with_content_length_zero_get_a_zero_length_content(string method, bool zeroLengthContent)
    {
        using var handler = new CapturingHandler();
        await using var transport = new SystemNetHttpClient(handler);

        await using var response = await transport.ExecuteAsync(Request.Create(Method.Of(method), "https://example.test/x"), RequestOptions.Empty, Ct);

        Assert.Equal(zeroLengthContent, handler.HadContent);
        if (zeroLengthContent)
        {
            Assert.Equal(0, handler.ContentLength);
        }
    }

    [Fact]
    public async Task A_request_that_has_a_body_keeps_its_body_and_is_not_given_a_substitute()
    {
        using var handler = new CapturingHandler();
        await using var transport = new SystemNetHttpClient(handler);

        await using var response = await transport.ExecuteAsync(Request.Post("https://example.test/x", RequestBody.FromString("payload")), RequestOptions.Empty, Ct);

        Assert.True(handler.HadContent);
        Assert.Equal(7, handler.ContentLength);
    }

    // Lead ruling P8b-T2(a) says the Expect is "logged as a drop under the policy". Expect is a TRANSPORT-11 framing header (P8b-12) that the
    // transport drops from every request, with or without a body, so the drop it logs is the framing one: event 111 at Debug, whatever
    // HeaderDropLogging says (event 110 is the policy's, for a header the native client cannot send). The checklist records this reading;
    // this row pins it, so routing the Expect through the policy for a body-less POST alone (one header logging differently by whether the
    // request has a body) would have to change the row on purpose.
    [Theory]
    [InlineData(HeaderDropLogging.FirstPerName)]
    [InlineData(HeaderDropLogging.Every)]
    [InlineData(HeaderDropLogging.VerboseOnly)]
    public async Task The_Expect_drop_is_a_framing_drop_at_debug_under_every_header_drop_logging_mode(HeaderDropLogging mode)
    {
        var logger = new RecordingLogger();
        using var handler = new CapturingHandler();
        await using var transport = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Logger = logger, HeaderDropLogging = mode });
        var request = Request.Create(Method.Post, "https://example.test/x").WithHeader("Expect", "100-continue");

        await using var response = await transport.ExecuteAsync(request, RequestOptions.Empty, Ct);

        Assert.False(handler.HadExpect);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(DexpaceLogEvents.TransportFramingHeaderDroppedId, entry.EventId.Id);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.DoesNotContain(logger.Entries, recorded => recorded.EventId.Id == DexpaceLogEvents.TransportHeaderDroppedId);
    }
}
