// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The responses under test are released by the assertions.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>
/// The error body an <see cref="HttpResponseException"/> carries (BODY-33; also HTTP-52, BODY-30): every read is a fresh,
/// non-consuming view, and an absent body reads as an empty body, never <see langword="null"/> (design §10 entry 30).
/// All pins over phase 1's S8 behaviour.
/// </summary>
[Trait("Category", "Unit")]
public class ErrorBodyPreviewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<HttpResponseException> FailWith(string json)
    {
        var response = TestResponses.Create(
            Status.BadRequest,
            body: ResponseBody.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(json)), CommonMediaTypes.ApplicationJsonUtf8));
        return await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(Token).AsTask());
    }

    [Fact]
    public async Task Reading_the_error_body_twice_yields_the_same_bytes_and_GetErrorAsync_still_works()
    {
        var ex = await FailWith("{\"error\":\"bad\"}");

        var first = await ex.Response.Body.ReadAsBytesAsync(Token);
        var second = await ex.Response.Body.ReadAsBytesAsync(Token);
        var text = await ex.Response.Body.ReadAsStringAsync(Token);

        Assert.Equal(first, second);
        Assert.Equal("{\"error\":\"bad\"}", text);
        Assert.Equal(first, await ex.Response.Body.ReadAsBytesAsync(Token));
        Assert.Equal("typed", await ex.GetErrorAsync<string>(new ScriptedSerde<string>("typed", "typed-again"), Token));
        Assert.Equal("typed-again", await ex.GetErrorAsync<string>(new ScriptedSerde<string>("typed-again"), Token));
    }

    [Fact]
    public async Task The_error_body_is_still_readable_after_the_response_is_disposed()
    {
        var ex = await FailWith("{\"error\":\"bad\"}");

        await ex.Response.DisposeAsync();

        Assert.Equal("{\"error\":\"bad\"}", await ex.Response.Body.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task An_empty_error_body_reads_as_an_empty_body_never_null()
    {
        var response = TestResponses.Create(Status.InternalServerError);
        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(Token).AsTask());

        Assert.NotNull(ex.Response.Body);
        Assert.Empty(await ex.Response.Body.ReadAsBytesAsync(Token));
        Assert.Empty(await ex.Response.Body.ReadAsBytesAsync(Token));
    }

    [Fact]
    public async Task Disposing_the_exception_response_twice_is_harmless()
    {
        var ex = await FailWith("{}");

        ex.Response.Dispose();
        await ex.Response.DisposeAsync();
        ex.Response.Dispose();

        Assert.Equal("{}", await ex.Response.Body.ReadAsStringAsync(Token));
    }
}
