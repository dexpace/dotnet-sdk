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
/// TRANSPORT-2 (phase 8b, ruling P8b-T2; design §10 entry 33): <c>SocketsHttpHandler</c> has no switch for its own connection-failure
/// retry, and re-sends a request with no content whose connection ends before any response byte up to three more times (runtime
/// 10.0, <c>HttpConnection.SendAsync</c>: <c>_canRetry</c> is set only when the first response read returns zero bytes and the
/// request has no content, or an Expect: 100-continue). The transport therefore gives a body-less <c>POST</c>, <c>PUT</c>, <c>PATCH</c>
/// or extension method a zero-length content, which the native client already writes as <c>Content-Length: 0</c>, so the retry
/// cannot fire; <c>GET</c>, <c>HEAD</c>, <c>OPTIONS</c>, <c>DELETE</c> and <c>TRACE</c> are idempotent, and attaching content to them
/// would break TRANSPORT-26 or RFC 9110 section 8.6, so they are still re-sent, which this class pins so a future runtime change is
/// noticed. The server reads each request and then closes in order (an end of stream, never <c>Abort</c>, whose FIN and reset the
/// platforms read differently).
/// </summary>
[Trait("Category", "Integration")]
public sealed class BodylessRetryWireTests
{
    private const string Warm = "/warm";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LoopbackServer StartClosingServer() => LoopbackServer.Start(request => request.Target == Warm
        ? LoopbackResponse.Ok("warm", keepAlive: true)
        : LoopbackResponse.Raw(ReadOnlySpan<byte>.Empty, closeConnection: true));

    public static TheoryData<string, string> Resent() =>
        new() { { "GET", "async" }, { "GET", "sync" }, { "HEAD", "async" }, { "OPTIONS", "async" }, { "DELETE", "async" }, { "DELETE", "sync" }, { "TRACE", "async" } };

    public static TheoryData<string, string> NotResent() =>
        new()
        {
            { "POST", "async" }, { "POST", "sync" }, { "PUT", "async" }, { "PUT", "sync" }, { "PATCH", "async" }, { "PATCH", "sync" },
            { "XYZ", "async" }, { "XYZ", "sync" },
        };

    private static async Task<int> ArrivalsAsync(string method, string face, string path)
    {
        await using var server = StartClosingServer();
        await using var transport = new SystemNetHttpClient();
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
        return server.Requests.Count(recorded => recorded.Target == path);
    }

    [Theory]
    [MemberData(nameof(NotResent))]
    public async Task A_body_less_request_that_may_carry_content_reaches_the_server_exactly_once(string method, string face)
    {
        var arrivals = await ArrivalsAsync(method, face, "/once");

        Assert.Equal(1, arrivals);
    }

    [Theory]
    [MemberData(nameof(Resent))]
    public async Task The_residual_an_idempotent_body_less_request_is_still_re_sent_by_the_native_client(string method, string face)
    {
        var arrivals = await ArrivalsAsync(method, face, "/residual");

        Assert.True(
            arrivals > 1,
            $"A body-less {method} used to reach the server {arrivals} times after its connection ended unanswered; the native client's own retry is no longer observed here. "
            + "If the runtime changed, TRANSPORT-2 (design section 10 entry 33) may be met outright: delete the permanent waiver.");
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
