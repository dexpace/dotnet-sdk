// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-28's MUST half (phase 8b, P8b-15): a file-range body is replayable, so a retry, a redirect or a native re-send carries
/// the same bytes of the range, and the transport never needs to capture it. The zero-copy SHOULD is declined: the native client
/// writes content into its own connection stream, which may be an <c>SslStream</c>, so no sendfile path is reachable through it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FileRequestBodyReplayTests : IDisposable
{
    private readonly string _path = Path.GetTempFileName();
    private readonly byte[] _content = [.. Enumerable.Range(0, 256).Select(i => (byte)i)];

    public FileRequestBodyReplayTests() => File.WriteAllBytes(_path, _content);

    public void Dispose() => File.Delete(_path);

    // Drains the request content into a list on each send, async and sync alike.
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<byte[]> Wires { get; } = [];

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var sink = new MemoryStream();
            request.Content?.CopyTo(sink, context: null, cancellationToken);
            Wires.Add(sink.ToArray());
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var sink = new MemoryStream();
            if (request.Content is { } content)
            {
                await content.CopyToAsync(sink, cancellationToken);
            }

            Wires.Add(sink.ToArray());
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public void A_file_range_is_replayable_and_reports_its_length()
    {
        var body = RequestBody.FromFile(_path, null, offset: 3, count: 10);

        Assert.True(body.IsReplayable);
        Assert.Equal(10, body.ContentLength);
    }

    [Theory]
    [InlineData("async")]
    [InlineData("sync")]
    public async Task The_same_ten_bytes_reach_the_wire_on_every_send_and_nothing_is_captured(string face)
    {
        using var handler = new RecordingHandler();
        await using var transport = new SystemNetHttpClient(handler);
        var request = Request.Post("https://example.test/file", RequestBody.FromFile(_path, null, offset: 3, count: 10));

        for (var send = 0; send < 2; send++)
        {
            using var response = face == "async"
                ? await transport.ExecuteAsync(request, RequestOptions.Empty, TestContext.Current.CancellationToken)
                : await Task.Run(() => transport.Execute(request, RequestOptions.Empty, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        }

        Assert.Equal(2, handler.Wires.Count);
        Assert.All(handler.Wires, wire => Assert.Equal(_content[3..13], wire));
        Assert.Equal(0, transport.CapturesCreated);
    }

    [Fact]
    public async Task A_native_resend_of_a_file_range_carries_the_same_bytes_without_a_capture()
    {
        // The shape of the kit's CreateWithNativeResend hook: the handler sends the same message twice through the inner handler.
        using var inner = new RecordingHandler();
        using var resending = new ResendingHandler(inner);
        using var borrowed = new SystemHttpClient(resending);
        await using var transport = new SystemNetHttpClient(borrowed);

        using var response = await transport.ExecuteAsync(
            Request.Post("https://example.test/file", RequestBody.FromFile(_path, null, offset: 3, count: 10)),
            RequestOptions.Empty,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.Wires.Count);
        Assert.All(inner.Wires, wire => Assert.Equal(_content[3..13], wire));
        Assert.Equal(0, transport.CapturesCreated);
    }

    private sealed class ResendingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var first = await base.SendAsync(request, cancellationToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
