// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>
/// The synchronous <c>HttpResponseException.GetError&lt;T&gt;</c> (3a's hand-off, P7a-13): it mirrors the asynchronous
/// <c>GetErrorAsync&lt;T&gt;</c>, including its nullable contract ("possibly null": an error model is read
/// opportunistically) and its single-use mapping to <see cref="ResponseNotReadException"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpResponseExceptionGetErrorTests
{
    private static HttpResponseException Exception(ResponseBody body) =>
        new(TestResponses.Create(Status.BadRequest, headers: Headers.Empty, body: body));

    [Fact]
    public void GetError_decodes_the_error_body()
    {
        var ex = Exception(ResponseBody.FromBytes("ok:slow down"u8.ToArray()));

        Assert.Equal("slow down", ex.GetError<string>(new Utf8LiteralSerde()));
    }

    [Fact]
    public void GetError_returns_null_for_a_null_literal()
    {
        var ex = Exception(ResponseBody.FromBytes("null"u8.ToArray()));

        Assert.Null(ex.GetError<string>(new Utf8LiteralSerde()));
    }

    [Fact]
    public void GetError_throws_ResponseNotReadException_on_a_consumed_body()
    {
        var ex = Exception(ResponseBody.FromBytes("ok:x"u8.ToArray()));
        ex.GetError<string>(new Utf8LiteralSerde());

        var thrown = Assert.Throws<ResponseNotReadException>(() => ex.GetError<string>(new Utf8LiteralSerde()));

        Assert.IsType<StreamConsumedException>(thrown.InnerException);
    }

    [Fact]
    public async Task GetError_and_GetErrorAsync_agree_over_a_replayable_buffered_body()
    {
        // ErrorBodyBuffer's copy is replayable, so both twins can read it, one after the other.
        var live = TestResponses.Create(Status.BadRequest, headers: Headers.Empty, body: ResponseBody.FromBytes("ok:same"u8.ToArray()));
        var buffered = ErrorBodyBuffer.Capture(live, TestContext.Current.CancellationToken);
        var ex = new HttpResponseException(buffered);

        var sync = ex.GetError<string>(new Utf8LiteralSerde());
        var async = await ex.GetErrorAsync<string>(new Utf8LiteralSerde(), TestContext.Current.CancellationToken);

        Assert.Equal("same", sync);
        Assert.Equal(sync, async);
    }

    [Fact]
    public void GetError_does_not_dispose_the_exceptions_response()
    {
        var body = new CountingPayloadBody("ok:x"u8.ToArray());
        var ex = Exception(body);

        ex.GetError<string>(new Utf8LiteralSerde());

        Assert.Equal(0, body.DisposeCount);
        Assert.Equal(1, body.StreamDisposeCount);
    }

    [Fact]
    public void GetError_argument_validation()
    {
        var ex = Exception(ResponseBody.FromBytes("ok:x"u8.ToArray()));

        Assert.Throws<ArgumentNullException>(() => ex.GetError<string>(null!));
    }
}
