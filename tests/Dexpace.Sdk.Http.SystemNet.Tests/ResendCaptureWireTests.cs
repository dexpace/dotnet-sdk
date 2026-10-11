// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-18 (phase 8b, P8b-10; G5 to G7) on a real socket: the loopback fixture is a forward proxy that answers <c>407
/// Proxy-Authenticate: Basic</c> until it sees <c>Proxy-Authorization</c>, which makes the native client serialise a request body
/// a second time, the case the replay capture exists for. The proxy's own bytes are the evidence: what it received twice must be
/// identical, and the caller's single-use source must have been read once.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResendCaptureWireTests
{
    private const int HundredKiB = 100 * 1024;
    private const string Origin = "http://origin.invalid/upload";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static byte[] Pattern(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i * 31) + 7);
        }

        return bytes;
    }

    // A non-seekable single-use source that counts what is read from it.
    private sealed class CountingSource(byte[] content) : Stream
    {
        private int _position;

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Take(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer) => Take(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Take(buffer.Span));

        private int Take(Span<byte> destination)
        {
            var count = Math.Min(Math.Min(destination.Length, 16 * 1024), content.Length - _position);
            content.AsSpan(_position, count).CopyTo(destination);
            _position += count;
            BytesRead += count;
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // Challenges with Basic until the request carries Proxy-Authorization, then accepts.
    private static LoopbackServer StartChallengingProxy() => LoopbackServer.Start(request =>
        request.HeaderValues("Proxy-Authorization").Count > 0
            ? LoopbackResponse.Ok("accepted", keepAlive: true)
            : LoopbackResponse.Status(407, "Proxy Authentication Required", [new("Proxy-Authenticate", "Basic realm=\"p\"")], keepAlive: true));

    private static ProxyOptions CredentialedProxyAt(LoopbackServer server) =>
        new() { Host = "127.0.0.1", Port = server.BaseUri.Port, UserName = "u", Password = "p" };

    public static TheoryData<string> Faces() => ["async", "sync"];

    private static async Task<Response> SendAsync(string face, SystemNetHttpClient transport, Request request) =>
        face == "async"
            ? await transport.ExecuteAsync(request, RequestOptions.Empty, Ct)
            : await Task.Run(() => transport.Execute(request, RequestOptions.Empty, Ct), Ct);

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_proxys_407_makes_the_native_client_send_a_single_use_http_upload_twice_with_identical_bytes(string face)
    {
        var payload = Pattern(HundredKiB);
        await using var proxy = StartChallengingProxy();
        await using var transport = new SystemNetHttpClient(new SystemNetHttpClientOptions { Proxy = CredentialedProxyAt(proxy) });
        using var source = new CountingSource(payload);

        using var response = await SendAsync(face, transport, Request.Post(Origin, RequestBody.FromStream(source, contentLength: -1)));

        Assert.Equal(200, response.Status.Code);
        var posts = proxy.Requests.Where(request => request.Method == "POST").ToArray();
        Assert.Equal(2, posts.Length);
        Assert.Empty(posts[0].HeaderValues("Proxy-Authorization"));
        Assert.NotEmpty(posts[1].HeaderValues("Proxy-Authorization"));
        Assert.Equal(payload, posts[0].Body.ToArray());
        Assert.Equal(payload, posts[1].Body.ToArray());
        Assert.Equal(HundredKiB, source.BytesRead);
        Assert.Equal(1, transport.CapturesCreated);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_body_over_a_lowered_limit_fails_the_send_with_the_transport_failure_type_naming_the_limit(string face)
    {
        await using var proxy = StartChallengingProxy();
        await using var transport = new SystemNetHttpClient(new SystemNetHttpClientOptions { Proxy = CredentialedProxyAt(proxy), ResendBufferLimit = 10 * 1024 });
        using var source = new CountingSource(Pattern(HundredKiB));

        var failure = await Assert.ThrowsAsync<ServiceRequestException>(() => SendAsync(face, transport, Request.Post(Origin, RequestBody.FromStream(source, contentLength: -1))));

        Assert.True(failure.IsRetryable);
        var wrapped = Assert.IsType<HttpRequestException>(failure.InnerException);
        var refused = Assert.IsType<IOException>(wrapped.InnerException);
        Assert.Contains("ResendBufferLimit", refused.Message, StringComparison.Ordinal);
        Assert.Contains("ToReplayableAsync", refused.Message, StringComparison.Ordinal);
        Assert.IsType<StreamConsumedException>(refused.InnerException);

        // Never a truncated or empty replacement: the proxy saw the first send only, whole, and the source was read once.
        var posts = proxy.Requests.Where(request => request.Method == "POST").ToArray();
        Assert.Single(posts);
        Assert.Equal(HundredKiB, posts[0].Body.Length);
        Assert.Equal(HundredKiB, source.BytesRead);
    }

    [Fact]
    public async Task An_owned_client_with_no_proxy_credentials_never_captures()
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        await using var plain = new SystemNetHttpClient();
        await using var proxy = LoopbackServer.Start(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        await using var proxied = new SystemNetHttpClient(new SystemNetHttpClientOptions { Proxy = new ProxyOptions { Host = "127.0.0.1", Port = proxy.BaseUri.Port } });
        using var source1 = new CountingSource(Pattern(1_000));
        using var source2 = new CountingSource(Pattern(1_000));

        using (var response = await plain.ExecuteAsync(Request.Post(server.Url("/u").AbsoluteUri, RequestBody.FromStream(source1, contentLength: -1)), RequestOptions.Empty, Ct))
        {
            Assert.Equal(200, response.Status.Code);
        }

        using (var response = await proxied.ExecuteAsync(Request.Post(Origin, RequestBody.FromStream(source2, contentLength: -1)), RequestOptions.Empty, Ct))
        {
            Assert.Equal(200, response.Status.Code);
        }

        Assert.Equal(0, plain.CapturesCreated);
        Assert.Equal(0, proxied.CapturesCreated);
        Assert.Equal(1_000, source1.BytesRead);
    }

    [Fact]
    public async Task G7_an_https_target_is_challenged_at_the_tunnel_before_a_byte_of_the_body_is_read()
    {
        // The proxy answers 407 to every CONNECT, so no tunnel ever opens: the request content is never serialised, which is why
        // a small capture bound costs little (the re-send of G5 and G6 is confined to http:// targets).
        await using var proxy = LoopbackServer.Start(_ => LoopbackResponse.Status(407, "Proxy Authentication Required", [new("Proxy-Authenticate", "Basic realm=\"p\"")], keepAlive: true));
        await using var transport = new SystemNetHttpClient(new SystemNetHttpClientOptions { Proxy = CredentialedProxyAt(proxy) });
        using var source = new CountingSource(Pattern(HundredKiB));

        await Assert.ThrowsAsync<ServiceRequestException>(() => transport.ExecuteAsync(Request.Post("https://origin.invalid/upload", RequestBody.FromStream(source, contentLength: -1)), RequestOptions.Empty, Ct));

        Assert.All(proxy.Requests, request => Assert.Equal("CONNECT", request.Method));
        Assert.Equal(0, source.BytesRead);
    }
}
