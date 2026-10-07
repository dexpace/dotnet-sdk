// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>
/// The same six behaviours as <c>EnsureSuccessErrorMappingTests</c> (S8, <c>Security</c>, unedited) over both entry
/// points: <c>EnsureSuccessAsync</c> and the synchronous <c>EnsureSuccess</c> (RECOV-16, BODY-30, BODY-31, HTTP-52).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResponseEnsureSuccessSyncTests
{
    private static readonly Request s_request = Request.Get("https://api.example.com/v1/items");

    private static async Task EnsureAsync(Core.Http.Response.Response response, bool useAsync)
    {
        if (useAsync)
        {
            await response.EnsureSuccessAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            response.EnsureSuccess(TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(304, false)]
    [InlineData(399, true)]
    [InlineData(600, false)]
    [InlineData(999, true)]
    public async Task A_non_error_status_is_returned_with_its_body_intact(int code, bool useAsync)
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log);
        using var response = TestResponses.Create(Status.FromCode(code), s_request, body: body);

        await EnsureAsync(response, useAsync);

        Assert.Equal(0, body.DisposeCount);
        Assert.Equal(0, body.OpenCount);
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(404, false)]
    [InlineData(499, true)]
    [InlineData(500, false)]
    [InlineData(599, true)]
    public async Task An_error_status_throws(int code, bool useAsync)
    {
        using var response = TestResponses.Create(Status.FromCode(code), s_request, body: ResponseBody.FromStream(new MemoryStream([1])));

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => EnsureAsync(response, useAsync));

        Assert.Equal(code, ex.Status.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_buffered_error_body_is_replayable(bool useAsync)
    {
        var payload = Encoding.UTF8.GetBytes("{\"error\":\"boom\"}");
        using var response = TestResponses.Create(Status.InternalServerError, s_request, body: ResponseBody.FromStream(new MemoryStream(payload), CommonMediaTypes.ApplicationJsonUtf8));

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => EnsureAsync(response, useAsync));

        Assert.Equal(payload, await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(payload, await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_original_response_is_disposed_when_the_error_is_raised(bool useAsync)
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log);
        using var response = TestResponses.Create(Status.ServiceUnavailable, s_request, body: body);

        await Assert.ThrowsAsync<HttpResponseException>(() => EnsureAsync(response, useAsync));

        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_buffered_copy_is_capped_at_one_mebibyte(bool useAsync)
    {
        var payload = new byte[Core.Http.Response.Response.MaxBufferedErrorBytes + 4096];
        Array.Fill(payload, (byte)'x');
        using var response = TestResponses.Create(Status.BadGateway, s_request, body: ResponseBody.FromStream(new MemoryStream(payload)));

        var ex = await Assert.ThrowsAsync<HttpResponseException>(() => EnsureAsync(response, useAsync));

        Assert.Equal(Core.Http.Response.Response.MaxBufferedErrorBytes, (await ex.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken)).Length);
    }
}
