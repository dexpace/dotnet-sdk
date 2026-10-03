// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// A <see cref="ResponseBody"/> backed by an <see cref="HttpResponseMessage"/>'s content stream.
/// Disposing the body (at most once, by the base class's latch) disposes the underlying <see cref="HttpResponseMessage"/>,
/// releasing the connection back to the pool. A body opened after it was disposed throws
/// <see cref="StreamClosedException"/> (P3b-11); a body already opened reports <see cref="StreamConsumedException"/> first.
/// </summary>
internal sealed class HttpResponseMessageBody : ResponseBody
{
    private readonly HttpResponseMessage _message;
    private int _consumed;
    private int _closed;

    // Repeats ResponseBody's second-open message verbatim (BODY-14); a test asserts the two stay equal.
    private const string ConsumedMessage =
        "This response body has already been read. A response body can be opened once; to read it more than once, "
        + "buffer it first (read it with ReadAsBytesAsync and keep the bytes, or create a replayable ResponseBody.FromBytes).";

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
        if (Volatile.Read(ref _consumed) != 0)
        {
            throw new StreamConsumedException(ConsumedMessage);
        }

        if (Volatile.Read(ref _closed) != 0)
        {
            throw new StreamClosedException("This response body was disposed before it was opened.");
        }

        if (Interlocked.Exchange(ref _consumed, 1) != 0)
        {
            throw new StreamConsumedException(ConsumedMessage);
        }

        return await _message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
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
