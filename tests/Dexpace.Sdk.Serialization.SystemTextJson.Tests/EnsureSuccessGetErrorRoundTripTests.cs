// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

/// <summary>
/// End-to-end tests that chain <c>Response.EnsureSuccessAsync</c> with
/// <c>HttpResponseException.GetErrorAsync&lt;T&gt;</c> to verify the buffered body is
/// deserializable after the exception is thrown.
/// </summary>
public sealed class EnsureSuccessGetErrorRoundTripTests
{
    private static SystemTextJsonSerde Serde() => new(TestJsonContext.Default);

    [Fact]
    public async Task EnsureSuccessAsync_ThenGetErrorAsync_DeserializesBufferedBody()
    {
        // Arrange: a 422 response carrying a JSON error body
        var json = Encoding.UTF8.GetBytes("""{"Code":"validation_failed","Message":"Name is required"}""");
        var body = ResponseBody.FromBytes(json, CommonMediaTypes.ApplicationJsonUtf8);
        var response = new Response(Status.FromCode(422), body: body);

        // Act: EnsureSuccessAsync should throw with the body buffered
        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            () => response.EnsureSuccessAsync().AsTask());

        // Assert: GetErrorAsync can deserialize the body that was buffered by EnsureSuccessAsync
        var error = await ex.GetErrorAsync<ApiError>(Serde());
        Assert.Equal(new ApiError("validation_failed", "Name is required"), error);
    }

    [Fact]
    public async Task EnsureSuccessAsync_ThenGetErrorAsync_CanBeCalledOnceOnStreamBody()
    {
        // Arrange: a response whose body is backed by a single-use stream (not pre-buffered)
        var json = Encoding.UTF8.GetBytes("""{"Code":"server_error","Message":"Unexpected error"}""");
        using var stream = new MemoryStream(json);
        var body = ResponseBody.FromStream(stream, CommonMediaTypes.ApplicationJsonUtf8, json.Length);
        var response = new Response(Status.FromCode(500), body: body);

        // Act
        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            () => response.EnsureSuccessAsync().AsTask());

        // Assert: the stream was drained and buffered; GetErrorAsync can read it
        var error = await ex.GetErrorAsync<ApiError>(Serde());
        Assert.Equal(new ApiError("server_error", "Unexpected error"), error);
    }

    [Fact]
    public async Task EnsureSuccessAsync_WithEmptyBody_ThrowsWithNoContent()
    {
        // Arrange: error response with an empty body
        var response = new Response(Status.FromCode(404));

        // Act
        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            () => response.EnsureSuccessAsync().AsTask());

        // Assert: empty body is readable (zero bytes)
        var bytes = await ex.Response.Body.ReadAsBytesAsync();
        Assert.Empty(bytes);
    }

    [Fact]
    public async Task EnsureSuccessAsync_SuccessResponse_ReturnsWithoutThrowing()
    {
        var response = new Response(Status.FromCode(200));
        await response.EnsureSuccessAsync();  // must not throw
    }
}
