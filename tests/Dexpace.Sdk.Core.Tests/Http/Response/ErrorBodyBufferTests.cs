// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

#pragma warning disable CA2000 // The capture under test disposes the original response and its body.

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

[Trait("Category", "Unit")]
public sealed class ErrorBodyBufferTests
{
    private static async Task<Response> Capture(Response response, bool async, CancellationToken? token = null)
    {
        var ct = token ?? TestContext.Current.CancellationToken;
        return async ? await ErrorBodyBuffer.CaptureAsync(response, ct) : ErrorBodyBuffer.Capture(response, ct);
    }

    private static Response ErrorResponse(ResponseBody body, Request? request = null, Headers? headers = null) =>
        TestResponses.Create(Status.InternalServerError, request, headers, body);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_body_below_the_cap_survives_whole(bool async)
    {
        var payload = new byte[1000];
        Random.Shared.NextBytes(payload);

        using var buffered = await Capture(ErrorResponse(new ProbeResponseBody(payload)), async);

        Assert.Equal(payload, await buffered.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_body_of_cap_plus_one_truncates_to_the_constant(bool async)
    {
        var payload = new byte[Response.MaxBufferedErrorBytes + 1];
        payload.AsSpan().Fill(7);

        using var buffered = await Capture(ErrorResponse(new ProbeResponseBody(payload)), async);

        var bytes = await buffered.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Response.MaxBufferedErrorBytes, bytes.Length);
        Assert.All(bytes, b => Assert.Equal(7, b));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_buffered_copy_is_replayable(bool async)
    {
        using var buffered = await Capture(ErrorResponse(new ProbeResponseBody([1, 2, 3])), async);

        var first = await buffered.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken);
        var second = await buffered.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        Assert.Equal<byte>([1, 2, 3], first);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_original_is_disposed_after_a_successful_drain(bool async)
    {
        var body = new ProbeResponseBody([1]);

        using var buffered = await Capture(ErrorResponse(body), async);

        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_original_is_disposed_after_a_failing_drain_and_the_drains_exception_is_primary(bool async)
    {
        var drainFailure = new IOException("drain failed");
        var body = new ProbeResponseBody(readFailure: drainFailure);

        var thrown = await Assert.ThrowsAsync<IOException>(() => Capture(ErrorResponse(body), async));

        Assert.Same(drainFailure, thrown);
        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_dispose_failure_after_a_failed_drain_lands_on_the_drains_trail_and_does_not_replace_it(bool async)
    {
        var drainFailure = new IOException("drain failed");
        var closeFailure = new InvalidOperationException("close failed");
        var body = new ProbeResponseBody(readFailure: drainFailure, disposeFailure: closeFailure);

        var thrown = await Assert.ThrowsAsync<IOException>(() => Capture(ErrorResponse(body), async));

        Assert.Same(drainFailure, thrown);
        Assert.Same(closeFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_dispose_failure_after_a_successful_drain_propagates(bool async)
    {
        var closeFailure = new InvalidOperationException("close failed");
        var body = new ProbeResponseBody([1, 2], disposeFailure: closeFailure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Capture(ErrorResponse(body), async));

        Assert.Same(closeFailure, thrown);
        Assert.Empty(ExceptionTrail.GetSuppressed(thrown));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_result_keeps_status_headers_and_request(bool async)
    {
        var request = Request.Post("https://example.test/orders", RequestBody.FromBytes(ReadOnlyMemory<byte>.Empty));
        var headers = new Headers.Builder().Set("X-Trace", "abc").Build();
        using var original = ErrorResponse(new ProbeResponseBody([1]), request, headers);

        using var buffered = await Capture(original, async);

        Assert.Same(request, buffered.Request);
        Assert.Equal(Status.InternalServerError, buffered.Status);
        Assert.Equal("abc", buffered.Headers.Get("X-Trace"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_cancelled_token_stops_the_drain(bool async)
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var body = new ProbeResponseBody([1, 2, 3]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Capture(ErrorResponse(body), async, cts.Token));

        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_content_type_survives_the_copy(bool async)
    {
        using var buffered = await Capture(
            ErrorResponse(new ProbeResponseBody([1], CommonMediaTypes.ApplicationJsonUtf8)),
            async);

        Assert.Equal(CommonMediaTypes.ApplicationJsonUtf8, buffered.Body.ContentType);
    }
}
