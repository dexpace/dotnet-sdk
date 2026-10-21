// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The responses and bodies under test are released by the assertions.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Serialization;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// BODY-14 and HTTP-41 over a real <c>SocketsHttpHandler</c> response (phase 11 review fix; design section 10 entry 6). A second open of a response body
/// returns the first stream, so a second <c>GetError</c> is handed the stream the first one disposed. Core's bodies hand out a stream that throws
/// <see cref="ObjectDisposedException"/> once disposed, which <c>HttpResponseException.GetError*</c> reads as "already consumed"; the stream a
/// <c>SocketsHttpHandler</c> content hands out reads 0 bytes after it is disposed instead, so the second call decoded an empty payload. The body now hands out a
/// stream that reports its own disposal.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResponseBodyReopenWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Faces() => ["async", "sync"];

    private static LoopbackServer StartErrorServer() =>
        LoopbackServer.Start(_ => LoopbackResponse.Status(500, "Internal Server Error", body: "ok:boom", keepAlive: true));

    private static async Task<Response> SendAsync(SystemNetHttpClient transport, LoopbackServer server) =>
        await transport.ExecuteAsync(Request.Get(server.Url("/e").AbsoluteUri), RequestOptions.Empty, Ct);

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_second_GetError_over_the_wire_throws_ResponseNotReadException(string face)
    {
        await using var server = StartErrorServer();
        await using var transport = new SystemNetHttpClient();
        using var response = await SendAsync(transport, server);
        var error = new HttpResponseException(response);
        var serde = new Utf8LiteralSerde();

        var first = face == "async" ? await error.GetErrorAsync<string>(serde, Ct) : error.GetError<string>(serde);
        var second = face == "async"
            ? await Assert.ThrowsAsync<ResponseNotReadException>(async () => await error.GetErrorAsync<string>(serde, Ct))
            : Assert.Throws<ResponseNotReadException>(() => error.GetError<string>(serde));

        Assert.Equal("boom", first);
        Assert.IsType<ObjectDisposedException>(second.InnerException);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task The_stream_a_second_open_returns_is_the_first_and_throws_once_disposed(string face)
    {
        await using var server = StartErrorServer();
        await using var transport = new SystemNetHttpClient();
        using var response = await SendAsync(transport, server);

        var first = face == "async" ? await response.Body.OpenReadAsync(Ct) : response.Body.OpenRead(Ct);
        first.Dispose();
        var second = face == "async" ? await response.Body.OpenReadAsync(Ct) : response.Body.OpenRead(Ct);

        Assert.Same(first, second);
        Assert.False(second.CanRead);
        Assert.Throws<ObjectDisposedException>(() => second.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => second.Read(new byte[4], 0, 4));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => _ = await second.ReadAsync(new byte[4], Ct));  // not ReadExactly: the read itself is under test
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task An_undisposed_stream_reads_the_whole_body_on_a_second_open(string face)
    {
        // BODY-14: the second open of a live stream continues the same handle, so a manual open then a convenience read sees the rest.
        await using var server = StartErrorServer();
        await using var transport = new SystemNetHttpClient();
        using var response = await SendAsync(transport, server);

        var first = face == "async" ? await response.Body.OpenReadAsync(Ct) : response.Body.OpenRead(Ct);
        var head = new byte[3];
        await first.ReadExactlyAsync(head, Ct);
        var second = face == "async" ? await response.Body.OpenReadAsync(Ct) : response.Body.OpenRead(Ct);
        using var rest = new MemoryStream();
        await second.CopyToAsync(rest, Ct);

        Assert.Same(first, second);
        Assert.Equal("ok:", System.Text.Encoding.ASCII.GetString(head));
        Assert.Equal("boom", System.Text.Encoding.ASCII.GetString(rest.ToArray()));
    }
}
