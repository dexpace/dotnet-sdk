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
    // Repeats ResponseBody.ClosedMessage verbatim (Core's constant is internal); a test asserts the two stay equal.
    private const string ClosedMessage = "This response body was disposed, so its stream is no longer available.";

    private readonly HttpResponseMessage _message;
    private Stream? _stream;
    private SemaphoreSlim? _opening;
    private int _closed;

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
        if (Opened() is { } opened)
        {
            return opened;
        }

        var gate = OpeningGate();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Opened() ?? Publish(await _message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            gate.Release();
        }
    }

    // The synchronous twin (P3a-5): it shares the one handle with OpenReadAsync, so an open in either order returns the same
    // stream, and it reads the live content stream without a thread-pool hop. Phase 7b needed it for the blocking
    // server-sent-events views over this transport.
    public override Stream OpenRead(CancellationToken cancellationToken = default)
    {
        if (Opened() is { } opened)
        {
            return opened;
        }

        var gate = OpeningGate();
        gate.Wait(cancellationToken);
        try
        {
            return Opened() ?? Publish(_message.Content.ReadAsStream(cancellationToken));
        }
        finally
        {
            gate.Release();
        }
    }

    // BODY-14, HTTP-41: the first open obtains the content stream under this gate, so two opens racing agree. A response content can
    // be consumed once (SocketsHttpHandler's throws InvalidOperationException to a second caller that reaches it before the first
    // has recorded its stream), and HttpContent refuses a synchronous read of a stream first obtained asynchronously; only the
    // pairs that include a synchronous open race inside it, since two asynchronous opens share one in-flight task. The gate is one
    // semaphore taken by both forms, with the caller's token on the wait: a queued open gives up alone, and an opener that fails
    // releases the gate, so the next open makes its own attempt. It is created by the first open of a body that is opened at all
    // and never disposed (it holds no handle unless AvailableWaitHandle is read, which nothing does); every open after the first
    // is one volatile read.
    private SemaphoreSlim OpeningGate() => LazyInitializer.EnsureInitialized(ref _opening, static () => new SemaphoreSlim(1, 1));

    // The handle once it exists; throws when the body was disposed, so a queued open reports the dispose, not a stale stream.
    private Stream? Opened()
    {
        ThrowIfClosed();
        return Volatile.Read(ref _stream);
    }

    // The gate is held, so there is one publisher. A body disposed while its first open was in flight has disposed the message under
    // it: the obtained stream is dropped and the open reports the dispose.
    private DisposalAwareStream Publish(Stream obtained)
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            obtained.Dispose();
            ThrowIfClosed();
        }

        var handle = new DisposalAwareStream(obtained);
        Volatile.Write(ref _stream, handle);
        return handle;
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
