// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The messages and bodies under test are released by the assertions.

using System.Net;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// The transport's response body: BODY-15 (the message is released once however the body is disposed), BODY-14 (the
/// second-open message names the buffering route) and design P3b-11 (a read after dispose throws
/// <see cref="StreamClosedException"/>). In-process handler, no socket.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpResponseMessageBodyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class TrackingContent : HttpContent
    {
        private int _disposals;

        public int Disposals => Volatile.Read(ref _disposals);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(new byte[] { 1, 2, 3 }, Token).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 3;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposals);
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ContentHandler(TrackingContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private static async Task<(Response Response, TrackingContent Content)> SendAsync()
    {
        var content = new TrackingContent();
        var transport = new SystemNetHttpClient(new SystemHttpClient(new ContentHandler(content)));
        var response = await transport.ExecuteAsync(Request.Get("https://example.test/"), Token);
        return (response, content);
    }

    [Fact]
    public async Task The_message_is_disposed_once_across_Dispose_and_DisposeAsync()
    {
        var (response, content) = await SendAsync();

        response.Body.Dispose();
        await response.Body.DisposeAsync();
        response.Dispose();
        await response.DisposeAsync();

        Assert.Equal(1, content.Disposals);
    }

    [Fact]
    public async Task A_read_after_dispose_throws_StreamClosedException()
    {
        var (response, _) = await SendAsync();
        await response.Body.DisposeAsync();

        await Assert.ThrowsAsync<StreamClosedException>(() => response.Body.OpenReadAsync(Token));
    }

    [Fact]
    public async Task A_read_then_dispose_then_read_reports_consumed()
    {
        var (response, _) = await SendAsync();
        _ = await response.Body.ReadAsBytesAsync(Token);
        response.Dispose();

        var ex = await Assert.ThrowsAsync<StreamConsumedException>(() => response.Body.OpenReadAsync(Token));
        Assert.Contains("buffer", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_second_open_throws_and_names_the_buffering_route()
    {
        var (response, _) = await SendAsync();
        _ = await response.Body.OpenReadAsync(Token);

        var ex = await Assert.ThrowsAsync<StreamConsumedException>(() => response.Body.OpenReadAsync(Token));

        // The transport repeats the core body's message verbatim; Core owns the text.
        var core = ResponseBody.FromBytes(new byte[] { 1 });
        _ = await core.OpenReadAsync(Token);
        var coreEx = await Assert.ThrowsAsync<StreamConsumedException>(() => core.OpenReadAsync(Token));
        Assert.Equal(coreEx.Message, ex.Message);
        response.Dispose();
    }

    [Fact]
    public async Task A_never_read_body_releases_the_message()
    {
        var (response, content) = await SendAsync();

        await response.DisposeAsync();

        Assert.Equal(1, content.Disposals);
    }
}
