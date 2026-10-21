// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// A <see cref="ResponseBody"/> backed by an <see cref="HttpResponseMessage"/>'s content stream.
/// Disposing the body (at most once, by the base class's latch) disposes the underlying <see cref="HttpResponseMessage"/>,
/// releasing the connection back to the pool. The first open obtains the content stream and every later open, in either
/// form, returns that same stream (BODY-14, HTTP-41); a body opened after it was disposed throws
/// <see cref="StreamClosedException"/> (P3b-11), whether or not it was opened before. The handle is a
/// <see cref="DisposalAwareStream"/> over the content stream: once the stream itself is disposed, the same handle comes back
/// from a later open and fails every read with <see cref="ObjectDisposedException"/>, as the other bodies' streams do (the
/// content stream <c>SocketsHttpHandler</c> hands out would read 0 bytes there, an empty payload).
/// </summary>
internal sealed class HttpResponseMessageBody : ResponseBody
{
    private readonly HttpResponseMessage _message;
    private Stream? _stream;
    private int _closed;

    // Repeats ResponseBody.ClosedMessage verbatim (Core's constant is internal); a test asserts the two stay equal.
    private const string ClosedMessage = "This response body was disposed, so its stream is no longer available.";

    public HttpResponseMessageBody(HttpResponseMessage message)
    {
        _message = message;
        // TRANSPORT-27: an inbound Content-Type the model cannot parse (HttpClient accepts `text/plain; foo`) is
        // "no media type", never a failed response.
        ContentType = MediaType.TryParse(message.Content.Headers.ContentType?.ToString(), out var mediaType)
            ? mediaType
            : null;
        ContentLength = message.Content.Headers.ContentLength ?? -1;
    }

    public override MediaType? ContentType { get; }

    public override long ContentLength { get; }

    public override async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();
        if (Volatile.Read(ref _stream) is { } opened)
        {
            return opened;
        }

        return Publish(await _message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
    }

    // The synchronous twin (P3a-5): it shares the one handle with OpenReadAsync, so an open in either order returns the same
    // stream, and it reads the live content stream without a thread-pool hop. Phase 7b needed it for the blocking
    // server-sent-events views over this transport.
    public override Stream OpenRead(CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();
        return Volatile.Read(ref _stream) ?? Publish(_message.Content.ReadAsStream(cancellationToken));
    }

    // The content hands out one stream per message however often it is asked, so two first opens racing agree already; the
    // compare-and-set keeps the handle this body returns single even over a content type that does not.
    private Stream Publish(Stream obtained)
    {
        var handle = new DisposalAwareStream(obtained);
        return Interlocked.CompareExchange(ref _stream, handle, null) ?? handle;
    }

    private void ThrowIfClosed()
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            throw new StreamClosedException(ClosedMessage);
        }
    }

    protected override void Dispose(bool disposing)
    {
        Volatile.Write(ref _closed, 1);
        if (disposing)
        {
            _message.Dispose();
        }

        base.Dispose(disposing);
    }
}
