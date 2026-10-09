// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Globalization;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Serialization;

/// <summary>
/// SERDE-27 and SERDE-28 (P7a-16 to P7a-19, P7a-23): the two response handlers, over the in-test codec and a body whose
/// materialising readers throw. The Location redaction rule is pinned as a Security test in
/// <c>StatusAwareHandlerLocationRedactionTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResponseHandlersTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CountingPayloadBody Body(string payload, long length = -1, Exception? disposeFailure = null) =>
        new(System.Text.Encoding.UTF8.GetBytes(payload), length, disposeFailure);

    private static Response Resp(Status status, ResponseBody body, Headers? headers = null, Request? request = null, string? reason = null) =>
        TestResponses.Create(status, request, headers, body, reasonPhrase: reason);

    // ---- SERDE-27: Deserialize<T> ----------------------------------------------------------------------------------

    [Fact]
    public async Task Valid_body_decodes_and_disposes_the_response_once()
    {
        using var body = Body("ok:abc");
        var response = Resp(Status.Ok, body);

        var value = await ResponseHandlers.Deserialize<string>(new Utf8LiteralSerde()).HandleAsync(response, Token);
        await response.DisposeAsync();

        Assert.Equal("abc", value);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_204_response_fails_naming_T_and_disposes()
    {
        using var body = Body(string.Empty, length: 0);

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.Deserialize<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.NoContent, body), Token));

        Assert.Contains("'System.String'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_malformed_body_is_a_SerdeException_with_an_inner_exception_and_disposes()
    {
        using var body = Body("bad");

        var ex = await Assert.ThrowsAnyAsync<SerdeException>(
            async () => await ResponseHandlers.Deserialize<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.Ok, body), Token));

        Assert.NotNull(ex.InnerException);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_mid_stream_IOException_propagates_unwrapped_and_disposes()
    {
        using var body = Body("io...");

        var ex = await Record.ExceptionAsync(
            async () => await ResponseHandlers.Deserialize<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.Ok, body), Token));

        Assert.IsType<IOException>(ex);
        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData("ok:abc", null)]
    [InlineData("", typeof(DeserializationException))]
    [InlineData("bad", typeof(DeserializationException))]
    [InlineData("io...", typeof(IOException))]
    public async Task The_response_is_disposed_in_every_case_and_a_dispose_failure_never_replaces_the_failure(string payload, Type? expectedFailure)
    {
        var releaseFailure = new InvalidOperationException("release failed");
        using var body = Body(payload, disposeFailure: releaseFailure);

        var thrown = await Record.ExceptionAsync(
            async () => await ResponseHandlers.Deserialize<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.Ok, body), Token));

        Assert.Equal(1, body.DisposeCount);
        if (expectedFailure is null)
        {
            Assert.Null(thrown);
        }
        else
        {
            Assert.IsType(expectedFailure, thrown);
            Assert.NotSame(releaseFailure, thrown);
        }
    }

    [Fact]
    public async Task A_root_null_names_T()
    {
        using var body = Body("null");

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.Deserialize<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.Ok, body), Token));

        Assert.Contains("'System.String'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task The_cancellation_token_reaches_the_codec()
    {
        using var cts = new CancellationTokenSource();
        var serde = new Utf8LiteralSerde();

        await ResponseHandlers.Deserialize<string>(serde).HandleAsync(Resp(Status.Ok, Body("ok:abc")), cts.Token);

        Assert.Equal(cts.Token, serde.LastToken);
    }

    [Fact]
    public async Task SERDE_3_and_the_handler_dispose_bind_different_objects()
    {
        // The codec leaves the stream it is handed open (SERDE-3); the handler closes the response it owns (SERDE-27). The
        // codec here stops after one byte, so it never reaches the end of the stream, which would close the tail on its own.
        using var body = Body("ok:abc");
        var probe = new ProbeSerde(body);

        await ResponseHandlers.Deserialize<string>(probe).HandleAsync(Resp(Status.Ok, body), Token);

        Assert.Equal(0, probe.StreamClosesSeenInsideTheCodec);
        Assert.Equal(1, body.StreamDisposeCount);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Handler_validates_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => ResponseHandlers.Deserialize<string>(null!));
        Assert.Throws<ArgumentNullException>(() => ResponseHandlers.DeserializeOnSuccess<string>(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ResponseHandlers.Deserialize<string>(new Utf8LiteralSerde()).HandleAsync(null!, Token));
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde()).HandleAsync(null!, Token));
    }

    // ---- SERDE-28: DeserializeOnSuccess<T> -------------------------------------------------------------------------

    [Fact]
    public async Task A_200_decodes()
    {
        var value = await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde())
            .HandleAsync(Resp(Status.Ok, Body("ok:abc")), Token);

        Assert.Equal("abc", value);
    }

    [Fact]
    public async Task A_204_is_a_missing_body_naming_T()
    {
        using var body = Body(string.Empty, length: 0);

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.NoContent, body), Token));

        Assert.Contains("no body to deserialize as 'System.String'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_200_root_null_names_T()
    {
        using var body = Body("null");

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.Ok, body), Token));

        Assert.Contains("'System.String'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_500_throws_HttpResponseException_with_the_status_and_a_buffered_body_readable_after_the_live_response_is_disposed()
    {
        using var live = Body("ok:upstream down");

        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.InternalServerError, live), Token));

        Assert.Equal(500, ex.Status.Code);
        Assert.Equal(1, live.DisposeCount);
        Assert.Equal("upstream down", await ex.GetErrorAsync<string>(new Utf8LiteralSerde(), Token));
        Assert.Equal("upstream down", await ex.GetErrorAsync<string>(new Utf8LiteralSerde(), Token));
    }

    [Fact]
    public async Task A_599_non_canonical_status_is_still_an_error_response()
    {
        using var live = Body("ok:x");

        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.FromCode(599), live), Token));

        Assert.Equal(599, ex.Status.Code);
        Assert.Equal(1, live.DisposeCount);
    }

    [Fact]
    public async Task A_400_buffers_at_most_MaxBufferedErrorBytes()
    {
        using var oversized = new CountingPayloadBody(new byte[Response.MaxBufferedErrorBytes + 10]);

        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.BadRequest, oversized), Token));

        var buffered = await ex.Response.Body.ReadAsBytesAsync(Token);
        Assert.Equal(Response.MaxBufferedErrorBytes, buffered.Length);
    }

    [Fact]
    public async Task The_live_response_is_disposed_exactly_once_on_the_error_branch()
    {
        using var live = Body("ok:x");

        await Assert.ThrowsAsync<HttpResponseException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde()).HandleAsync(Resp(Status.NotFound, live), Token));

        Assert.Equal(1, live.DisposeCount);
    }

    [Theory]
    [InlineData(304, "Not Modified")]
    [InlineData(103, null)]
    [InlineData(302, "Found")]
    public async Task An_other_status_fails_with_a_DeserializationException_leading_with_the_code_and_one_dispose(int code, string? reason)
    {
        using var body = Body(string.Empty);

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde())
                .HandleAsync(Resp(Status.FromCode(code), body, reason: reason), Token));

        Assert.StartsWith(code.ToString(CultureInfo.InvariantCulture), ex.Message, StringComparison.Ordinal);
        Assert.Contains("expected a 2xx response to deserialize as 'System.String'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.DisposeCount);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task The_third_branch_message_carries_the_raw_ETag()
    {
        foreach (var etag in new[] { "\"abc\"", "W/\"x\"" })
        {
            var headers = new Headers.Builder().AddInbound("ETag", etag).Build();

            var ex = await Assert.ThrowsAsync<DeserializationException>(
                async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde())
                    .HandleAsync(Resp(Status.NotModified, Body(string.Empty), headers), Token));

            Assert.Contains(" ETag: " + etag + ".", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_third_branch_message_omits_absent_parts()
    {
        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde())
                .HandleAsync(Resp(Status.NotModified, Body(string.Empty)), Token));

        Assert.DoesNotContain("ETag", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Location", ex.Message, StringComparison.Ordinal);
        Assert.Equal("304 Not Modified: expected a 2xx response to deserialize as 'System.String'.", ex.Message);
    }

    [Fact]
    public async Task A_relative_Location_is_resolved_against_the_request_uri_then_redacted()
    {
        var headers = new Headers.Builder().AddInbound("Location", "/p?access_token=s3cr3t&api-version=2").Build();
        var request = Request.Get("https://h.example/a/b?x=1");

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde())
                .HandleAsync(Resp(Status.Found, Body(string.Empty), headers, request), Token));

        var expected = new UrlRedactor().Redact(new Uri("https://h.example/p?access_token=s3cr3t&api-version=2"));
        Assert.Contains(" Location: " + expected + ".", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", ex.Message, StringComparison.Ordinal);
        Assert.Contains("https://h.example/p", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unparseable_Location_is_replaced_by_the_placeholder()
    {
        var headers = new Headers.Builder().AddInbound("Location", "http://[not-a-host/p?token=s3cr3t").Build();

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseHandlers.DeserializeOnSuccess<string>(new Utf8LiteralSerde())
                .HandleAsync(Resp(Status.Found, Body(string.Empty), headers), Token));

        Assert.Contains(" Location: <unparseable>.", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMessage_replaces_control_characters_so_the_message_is_one_line()
    {
        // Headers can never deliver CR or LF (AddInbound rejects them), so this is defensive code, covered through the seam.
        var message = SuccessDeserializingHandler<string>.BuildMessage(
            302, "Fo\r\nund", "\"a\r\nb\"", "/p?x=1\r\ninjected: yes", new Uri("https://h.example/"));

        Assert.DoesNotContain('\r', message);
        Assert.DoesNotContain('\n', message);
        Assert.StartsWith("302 Fo??und: ", message, StringComparison.Ordinal);
        Assert.Contains("ETag: \"a??b\".", message, StringComparison.Ordinal);
    }

    private sealed class ProbeSerde(CountingPayloadBody body) : ISerde
    {
        public int StreamClosesSeenInsideTheCodec { get; private set; } = -1;

        public MediaType DefaultMediaType => MediaType.Of("application", "json");

        public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
        {
            var one = new byte[1];
            await source.ReadExactlyAsync(one, cancellationToken);
            StreamClosesSeenInsideTheCodec = body.StreamDisposeCount;
            return (T)(object)"probed";
        }

        public void Serialize<T>(IBufferWriter<byte> destination, T value)
        {
        }

        public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => default;
    }
}
