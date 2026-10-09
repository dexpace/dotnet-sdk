// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The facade's ownership and release rules: SSE-23 to SSE-28, SSE-30, SSE-32, SSE-39 (P7b-9 to P7b-14).</summary>
[Trait("Category", "Unit")]
public class ServerSentEventStreamLifecycleTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> BodylessCases =>
    [
        "status-204",
        "status-205",
        "status-304",
        "head-request",
        "content-length-zero",
        "no-body",
    ];

    private static (Response Response, SseBody? Body) BodylessResponse(string kind)
    {
        SseBody Body(long length = -1) => new(SseResponses.Bytes("data: x\n\n")) { Length = length };

        switch (kind)
        {
            case "status-204":
            {
                var body = Body();
                return (SseResponses.Respond(body, Status.FromCode(204)), body);
            }

            case "status-205":
            {
                var body = Body();
                return (SseResponses.Respond(body, Status.FromCode(205)), body);
            }

            case "status-304":
            {
                var body = Body();
                return (SseResponses.Respond(body, Status.NotModified), body);
            }

            case "head-request":
            {
                var body = Body();
                return (SseResponses.Respond(body, Status.Ok, Method.Head), body);
            }

            case "content-length-zero":
            {
                var body = Body(length: 0);
                return (SseResponses.Respond(body), body);
            }

            case "no-body":
                return (new Response(Dexpace.Sdk.Core.Http.Request.Request.Get("https://sse.example.test/events"), Status.Ok, Protocol.Http11), null);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    [Theory]
    [MemberData(nameof(BodylessCases))]
    public void FromResponse_rejects_a_bodyless_response_and_disposes_it(string kind)
    {
        var (response, body) = BodylessResponse(kind);

        var ex = Assert.Throws<ArgumentException>(() => ServerSentEventStream.FromResponse(response));

        Assert.Equal("response", ex.ParamName);
        Assert.DoesNotContain("sse.example.test", ex.Message, StringComparison.Ordinal);
        if (body is not null)
        {
            Assert.Equal(1, body.DisposeCount);
            Assert.Equal(0, body.OpenCount);
        }
    }

    [Fact]
    public void FromResponse_disposes_the_response_when_the_cap_is_invalid()
    {
        var (response, body) = SseResponses.Respond("data: x\n\n");

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => ServerSentEventStream.FromResponse(response, maxLineBytes: 0));

        Assert.Equal("maxLineBytes", ex.ParamName);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public void FromResponse_rejects_a_null_response()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => ServerSentEventStream.FromResponse(null!));

        Assert.Equal("response", ex.ParamName);
    }

    [Fact]
    public void A_throw_while_disposing_a_rejected_response_is_attached_not_thrown()
    {
        var failure = new InvalidOperationException("close failed");
        var body = new SseBody(SseResponses.Bytes("data: x\n\n")) { DisposeFailure = failure };
        var response = SseResponses.Respond(body, Status.NoContent);

        var ex = Assert.Throws<ArgumentException>(() => ServerSentEventStream.FromResponse(response));

        Assert.Same(failure, Assert.Single(ExceptionTrail.GetSuppressed(ex)));
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Construction_does_no_io_and_a_never_enumerated_stream_only_releases_the_response()
    {
        var (response, body) = SseResponses.Respond("data: x\n\n");

        var stream = ServerSentEventStream.FromResponse(response);

        Assert.Equal(0, body.OpenCount);
        Assert.Equal(0, body.DisposeCount);

        await stream.DisposeAsync();

        Assert.Equal(0, body.OpenCount);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Dispose_and_DisposeAsync_in_any_mix_release_exactly_once()
    {
        var (response, body) = SseResponses.Respond("data: x\n\n");
        var stream = ServerSentEventStream.FromResponse(response);

        stream.Dispose();
        await stream.DisposeAsync();
        stream.Dispose();
        await stream.DisposeAsync();

        Assert.Equal(1, body.DisposeCount);
    }
}
