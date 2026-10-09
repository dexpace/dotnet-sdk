// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Request bodies that are written more than once, or must not be: a single-use body goes out once (<c>TRANSPORT-17</c>), a
/// native re-send replays identical bytes or fails cleanly (<c>TRANSPORT-18</c>), and a file range is replayable
/// (<c>TRANSPORT-28</c>). The rows belong to phase 8b; the assertions are 8a's.
/// </summary>
internal static class ResendAssertions
{
    private const int SingleUseBytes = 1024;
    private const int ResentBytes = 1024 * 1024;

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-17.single-use-written-once", ["TRANSPORT-17"], RequirementLevel.Must, Both, SingleUseWrittenOnceAsync);
        yield return Define("transport-18.native-resend-identical", ["TRANSPORT-18"], RequirementLevel.Must, AsyncOnly, NativeResendIdenticalAsync);
        yield return Define("transport-28.file-range-replayable", ["TRANSPORT-28"], RequirementLevel.Should, Both, FileRangeReplayableAsync);
    }

    // TRANSPORT-17: a single-use body is read once and reaches the server once.
    private static async Task SingleUseWrittenOnceAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.CreateTransport();
        var payload = Pattern(SingleUseBytes);
        using var source = CountingStream.OfBytes(payload);

        using var response = await transport.ExpectResponseAsync(Request.Post(server.Url("/once").AbsoluteUri, RequestBody.FromStream(source, contentLength: SingleUseBytes)), "a POST of a single-use body", cancellationToken).ConfigureAwait(false);

        Check.Equal(source.BytesRead, (long)SingleUseBytes, "the bytes read from the single-use source");
        var recorded = server.Requests;
        Check.Equal(recorded.Count, 1, "the requests the server received");
        Check.True(recorded[0].Body.Span.SequenceEqual(payload), "the server must have received the single-use body exactly once, byte for byte", $"{SingleUseBytes} identical bytes", $"{recorded[0].Body.Length} bytes");
        Check.NoFaults(server);
    }

    // TRANSPORT-18: when the native layer re-sends a request, every serialisation of a single-use body carries the same bytes;
    // when buffering the body fails mid-write the send fails with the transport-failure type instead of shipping a truncated copy.
    private static async Task NativeResendIdenticalAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var payload = Pattern(ResentBytes);
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.RequireNativeResend();

        using (var source = CountingStream.OfBytes(payload))
        {
            var call = transport.SendAsync(Request.Post(server.Url("/resend").AbsoluteUri, RequestBody.FromStream(source, contentLength: ResentBytes)), cancellationToken);
            var failure = await Outcome.OfAsync(call, context.AssertionBound, "the native re-send of a single-use body to end", cancellationToken).ConfigureAwait(false);

            Check.True(failure is null, "a native re-send of a single-use body must replay identical bytes, not fail", "a successful send of two identical bodies", Outcome.Name(failure));
            var bodies = server.Requests.Where(request => request.Method == "POST").Select(request => request.Body).ToArray();
            Check.True(bodies.Length == 2, "the native layer re-sent the request, so the server must have received it twice", "2 requests", bodies.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Check.True(bodies.All(body => body.Span.SequenceEqual(payload)), "every serialisation of the single-use body must carry the same bytes (TRANSPORT-18)", "two identical 1 MiB bodies", "a body that differs");
        }

        using var failing = new FailingMidWriteStream(payload, failAfter: ResentBytes / 2);
        var broken = transport.SendAsync(Request.Post(server.Url("/broken").AbsoluteUri, RequestBody.FromStream(failing, contentLength: ResentBytes)), cancellationToken);
        var brokenFailure = await Outcome.OfAsync(broken, context.AssertionBound, "the send whose body source fails mid-write to end", cancellationToken).ConfigureAwait(false);

        Check.True(brokenFailure is SdkException, "a body source that fails mid-write must fail the send with the transport-failure type, never ship a truncated or empty body (TRANSPORT-18)", "an SdkException", Outcome.Name(brokenFailure));
    }

    // TRANSPORT-28: a byte range of a file is sent, twice if need be, with the same bytes each time. (Zero-copy is not observable on the wire.)
    private static async Task FileRangeReplayableAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.CreateTransport();
        var path = Path.GetTempFileName();
        try
        {
            var content = Pattern(256);
            await File.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
            var body = RequestBody.FromFile(path, null, offset: 3, count: 10);
            Check.True(body.IsReplayable, "a file body must be replayable (TRANSPORT-28)", "IsReplayable == true", "false");

            for (var send = 1; send <= 2; send++)
            {
                using var response = await transport.ExpectResponseAsync(Request.Post(server.Url("/file").AbsoluteUri, body), $"send {send} of a file range", cancellationToken).ConfigureAwait(false);
            }

            var recorded = server.Requests;
            Check.Equal(recorded.Count, 2, "the requests the server received");
            Check.True(recorded.All(request => request.Body.Span.SequenceEqual(content.AsSpan(3, 10))), "each send of the file range must carry exactly bytes 3 to 12 of the file", "the same 10 bytes twice", "a different body");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] Pattern(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = (byte)((i * 31) + 7);
        }

        return bytes;
    }

    // A single-use source that serves some of its bytes and then fails: the buffering failure TRANSPORT-18 names.
    private sealed class FailingMidWriteStream(byte[] content, int failAfter) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(Take(buffer.Span));

        public override int Read(byte[] buffer, int offset, int count) => Take(buffer.AsSpan(offset, count));

        private int Take(Span<byte> destination)
        {
            if (_position >= failAfter)
            {
                throw new IOException("The body source failed mid-write.");
            }

            var count = Math.Min(Math.Min(destination.Length, 16 * 1024), failAfter - _position);
            content.AsSpan(_position, count).CopyTo(destination);
            _position += count;
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
