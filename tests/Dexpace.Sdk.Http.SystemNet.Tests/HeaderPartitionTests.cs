// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-10, -11, -12, -13, -26, HTTP-21, XCUT-18 (phase 8b, P8b-11, design §E): where each request header ends up in the
/// native message, on both faces, over a handler that records the <see cref="HttpRequestMessage"/> it is given. One row per cell of
/// the design's table: the framing set, the wire re-check, content headers with and without a body, the caller's <c>Content-Type</c>,
/// the methods that may not carry content, and the headers a collection refuses. Every drop is logged by name, never by value.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HeaderPartitionTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Seen(
        IReadOnlyDictionary<string, string[]> Headers,
        IReadOnlyDictionary<string, string[]> ContentHeaders,
        bool HasContent,
        long? ContentLength);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Seen? Last { get; private set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => Record(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Record(request));

        private HttpResponseMessage Record(HttpRequestMessage request)
        {
            Last = new Seen(
                request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                request.Content?.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray(), StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                request.Content is not null,
                request.Content?.Headers.ContentLength);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    public static TheoryData<string> Faces() => ["async", "sync"];

    private static async Task<(Seen Seen, RecordingLogger Logger)> SendAsync(string face, Request request, HeaderDropLogging mode = HeaderDropLogging.FirstPerName)
    {
        var logger = new RecordingLogger();
        using var handler = new CapturingHandler();
        await using var transport = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Logger = logger, HeaderDropLogging = mode });

        using var response = face == "async"
            ? await transport.ExecuteAsync(request, RequestOptions.Empty, Ct)
            : await Task.Run(() => transport.Execute(request, RequestOptions.Empty, Ct), Ct);

        return (handler.Last!, logger);
    }

    private static Request WithBody(Method method, params (string Name, string Value)[] headers) =>
        Apply(Request.Create(method, "https://example.test/x", body: RequestBody.FromString("payload", CommonMediaTypes.ApplicationJson)), headers);

    private static Request Bodyless(Method method, params (string Name, string Value)[] headers) =>
        Apply(Request.Create(method, "https://example.test/x"), headers);

    private static Request Apply(Request request, (string Name, string Value)[] headers) =>
        headers.Aggregate(request, (current, header) => current.WithHeader(header.Name, header.Value));

    private static void AssertNoSecretInLog(RecordingLogger logger, params string[] secrets)
    {
        foreach (var entry in logger.Entries)
        {
            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, entry.Message, StringComparison.Ordinal);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // The framing set (TRANSPORT-11) and the wire re-check (XCUT-18).

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task Framing_headers_are_dropped_with_and_without_a_body_and_logged_at_debug_by_name(string face)
    {
        (string, string)[] framing =
        [
            ("Host", "evil.example"), ("content-length", "999"), ("Transfer-Encoding", "chunked"), ("CONNECTION", "close"),
            ("Keep-Alive", "timeout=5"), ("Upgrade", "websocket"), ("TE", "trailers"), ("Expect", "100-continue"),
        ];

        foreach (var request in new[] { WithBody(Method.Post, [.. framing, ("X-Pass", "kept")]), Bodyless(Method.Post, [.. framing, ("X-Pass", "kept")]) })
        {
            var (seen, logger) = await SendAsync(face, request);

            foreach (var (name, _) in framing)
            {
                Assert.DoesNotContain(seen.Headers.Keys, key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase) && name != "Host");
            }

            Assert.Equal(["kept"], seen.Headers["X-Pass"]);
            var drops = logger.Entries.Where(entry => entry.EventId.Id == DexpaceLogEvents.TransportFramingHeaderDroppedId).ToArray();
            Assert.Equal(framing.Length, drops.Length);
            Assert.All(drops, entry => Assert.Equal(LogLevel.Debug, entry.Level));
            AssertNoSecretInLog(logger, "evil.example", "999", "chunked", "websocket", "kept");
        }
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_header_that_fails_the_wire_recheck_is_dropped_alone_under_the_policy_and_the_rest_are_sent(string face)
    {
        var headers = new Headers.Builder().AddInbound("X-Echoed", "café-secret").Add("X-Pass", "kept").Build();
        var request = Request.Create(Method.Get, "https://example.test/x", headers);

        var (seen, logger) = await SendAsync(face, request);

        Assert.DoesNotContain("X-Echoed", seen.Headers.Keys);
        Assert.Equal(["kept"], seen.Headers["X-Pass"]);
        var drop = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, drop.Level);
        Assert.Equal(DexpaceLogEvents.TransportHeaderDroppedId, drop.EventId.Id);
        Assert.Contains("X-Echoed", drop.Message, StringComparison.Ordinal);
        AssertNoSecretInLog(logger, "secret", "caf");
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // Content headers, with a body and without.

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task Content_headers_with_a_body_land_on_the_content_and_nowhere_else(string face)
    {
        var request = WithBody(
            Method.Post,
            ("Allow", "GET, POST"),
            ("Content-Language", "en"),
            ("Content-Encoding", "identity"),
            ("Content-Location", "/copy"),
            ("Expires", "Wed, 21 Oct 2026 07:28:00 GMT"),
            ("Last-Modified", "Tue, 20 Oct 2026 07:28:00 GMT"),
            ("X-Plain", "request-header"));

        var (seen, logger) = await SendAsync(face, request);

        foreach (var name in new[] { "Allow", "Content-Language", "Content-Encoding", "Content-Location", "Expires", "Last-Modified" })
        {
            Assert.Contains(name, seen.ContentHeaders.Keys);
            Assert.DoesNotContain(name, seen.Headers.Keys);
        }

        Assert.Equal(["request-header"], seen.Headers["X-Plain"]);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_body_less_post_carries_a_content_header_on_an_empty_content_with_content_length_zero(string face)
    {
        var (seen, logger) = await SendAsync(face, Bodyless(Method.Post, ("Content-Language", "en"), ("Content-Type", "application/vnd.y")));

        Assert.True(seen.HasContent);
        Assert.Equal(0, seen.ContentLength);
        Assert.Equal(["en"], seen.ContentHeaders["Content-Language"]);
        Assert.Equal(["application/vnd.y"], seen.ContentHeaders["Content-Type"]);
        Assert.Empty(logger.Entries);
    }

    [Theory]
    [InlineData("GET", "async")]
    [InlineData("GET", "sync")]
    [InlineData("HEAD", "async")]
    [InlineData("TRACE", "async")]
    [InlineData("TRACE", "sync")]
    public async Task A_content_header_on_a_method_that_carries_no_content_is_dropped_and_logged_and_no_content_is_attached(string method, string face)
    {
        var (seen, logger) = await SendAsync(face, Bodyless(Method.Of(method), ("Content-Language", "en"), ("X-Pass", "kept")));

        Assert.False(seen.HasContent);
        Assert.DoesNotContain("Content-Language", seen.Headers.Keys);
        Assert.Equal(["kept"], seen.Headers["X-Pass"]);
        var drop = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, drop.Level);
        Assert.Equal(DexpaceLogEvents.TransportHeaderDroppedId, drop.EventId.Id);
        Assert.Contains("Content-Language", drop.Message, StringComparison.Ordinal);
        AssertNoSecretInLog(logger, "'en'", "kept");
    }

    [Theory]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task A_body_less_delete_or_options_carries_a_content_header_on_an_empty_content_and_otherwise_none(string method)
    {
        var (withHeader, logger) = await SendAsync("async", Bodyless(Method.Of(method), ("Content-Language", "en")));
        var (without, _) = await SendAsync("async", Bodyless(Method.Of(method), ("X-Pass", "kept")));

        Assert.True(withHeader.HasContent);
        Assert.Equal(["en"], withHeader.ContentHeaders["Content-Language"]);
        Assert.Empty(logger.Entries);
        Assert.False(without.HasContent);
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // The caller's Content-Type is authoritative (TRANSPORT-10).

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task The_callers_content_type_wins_over_the_bodys_and_appears_once(string face)
    {
        var (seen, _) = await SendAsync(face, WithBody(Method.Post, ("Content-Type", "application/vnd.x+json")));

        Assert.Equal(["application/vnd.x+json"], seen.ContentHeaders["Content-Type"]);
        Assert.DoesNotContain("Content-Type", seen.Headers.Keys);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task The_bodys_media_type_is_used_when_the_caller_set_none(string face)
    {
        var (seen, _) = await SendAsync(face, WithBody(Method.Post));

        Assert.StartsWith("application/json", Assert.Single(seen.ContentHeaders["Content-Type"]), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("content-TYPE")]
    [InlineData("CONTENT-TYPE")]
    public async Task The_caller_type_is_found_by_a_case_insensitive_name_match(string name)
    {
        var (seen, _) = await SendAsync("async", WithBody(Method.Post, (name, "application/vnd.z")));

        Assert.Equal(["application/vnd.z"], seen.ContentHeaders["Content-Type"]);
    }

    [Fact]
    public async Task A_multipart_body_with_a_caller_type_that_lacks_the_boundary_sends_the_callers_value()
    {
        var multipart = RequestBody.Multipart([new MultipartPart("field", RequestBody.FromString("v"))]);
        var request = Request.Create(Method.Post, "https://example.test/x", body: multipart).WithHeader("Content-Type", "multipart/form-data");

        var (seen, _) = await SendAsync("async", request);

        Assert.Equal(["multipart/form-data"], seen.ContentHeaders["Content-Type"]);
    }

    [Fact]
    public async Task A_multipart_body_with_no_caller_type_keeps_its_own_with_the_boundary()
    {
        var multipart = RequestBody.Multipart([new MultipartPart("field", RequestBody.FromString("v"))]);
        var request = Request.Create(Method.Post, "https://example.test/x", body: multipart);

        var (seen, _) = await SendAsync("async", request);

        Assert.Contains("boundary=", Assert.Single(seen.ContentHeaders["Content-Type"]), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // Headers a collection refuses are a logged drop, never a silent loss (TRANSPORT-12). The only header kind the native request
    // collection refuses is a content-kind one (a response-kind header is treated as a custom header there), so the reachable refusal
    // is a content header with no content to carry it, which is the body-less GET, HEAD and TRACE rows above; the remaining refusal
    // paths in OutboundHeaderMapper are defensive.

    [Fact]
    public async Task The_policy_decides_the_level_of_a_repeated_drop_of_one_name()
    {
        var logger = new RecordingLogger();
        using var handler = new CapturingHandler();
        await using var transport = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Logger = logger });
        var request = Bodyless(Method.Get, ("Content-Language", "en"));

        for (var i = 0; i < 3; i++)
        {
            using var response = await transport.ExecuteAsync(request, RequestOptions.Empty, Ct);
        }

        Assert.Equal([LogLevel.Warning, LogLevel.Debug, LogLevel.Debug], logger.Entries.Select(entry => entry.Level));
    }

    [Fact]
    public async Task A_header_the_caller_set_that_the_native_client_accepts_is_passed_through_untouched()
    {
        var (seen, logger) = await SendAsync("async", Bodyless(Method.Get, ("X-Awkward_Name", "underscore"), ("X-Spaced", "a  b\tc"), ("X-Quoted", "\"quoted\"; v=1")));

        Assert.Equal(["underscore"], seen.Headers["X-Awkward_Name"]);
        Assert.Equal(["a  b\tc"], seen.Headers["X-Spaced"]);
        Assert.Empty(logger.Entries);
    }
}
