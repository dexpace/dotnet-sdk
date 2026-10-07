// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// A typed abstraction over an incoming response payload.
/// </summary>
/// <remarks>
/// The body is not pre-buffered. <see cref="OpenReadAsync"/> exposes the raw stream;
/// <see cref="ReadAsBytesAsync"/> and <see cref="ReadAsStringAsync"/> fully drain and then close
/// it. Reads are single-use: a second read after the stream is consumed raises
/// <see cref="StreamConsumedException"/>. The one exception is the buffered error body an
/// <see cref="HttpResponseException"/> raised by <see cref="Response.EnsureSuccessAsync"/> carries, which can be read
/// any number of times.
/// <para>
/// <b>Disposal (HTTP-41, BODY-15, P3b-2).</b> Dispose the body (directly or via the owning <see cref="Response"/>) to
/// release the underlying connection. <see cref="Dispose()"/> and <see cref="DisposeAsync"/> are latched: across any mix
/// of the two, the release runs at most once, and a release that throws still flips the latch, so the exception
/// propagates once and later calls are no-ops. A subclass puts its release in <see cref="Dispose(bool)"/> and, when it
/// has an asynchronous release, in <see cref="DisposeAsyncCore"/>; there is no finalizer. The convenience readers
/// (<see cref="ReadAsBytesAsync"/>, <see cref="ReadAsStringAsync"/> and their twins) dispose the body when they finish,
/// on success and on failure (BODY-16).
/// </para>
/// <para><b>Breaking:</b> <c>Dispose()</c> and <c>DisposeAsync()</c> were <c>public virtual</c> and are now not virtual;
/// a subclass overrides <see cref="Dispose(bool)"/> and <see cref="DisposeAsyncCore"/> instead. A release now happens at
/// most once. A stream-backed body opened after it was disposed throws <see cref="StreamClosedException"/> (a body that
/// was already opened still reports <see cref="StreamConsumedException"/> first).</para>
/// <para>
/// Every async member has a synchronous twin with the same name minus <c>Async</c> (<see cref="OpenRead"/>,
/// <see cref="ReadAsBytes"/>, <see cref="ReadAsString"/>). The twins are virtual: the base <see cref="OpenRead"/> throws
/// <see cref="NotSupportedException"/> naming the subclass, and every SDK variant overrides it (design position D).
/// </para>
/// <para>
/// <b>Single open, in both forms (position B, P3a-3).</b> A body is opened once: the first of <see cref="OpenRead"/> and
/// <see cref="OpenReadAsync"/> to run claims it, and a second open in either form throws
/// <see cref="StreamConsumedException"/>. The buffered error body of an <see cref="HttpResponseException"/> is
/// replayable and opens a fresh view each time.
/// </para>
/// <para>
/// <b>Materialisation cap (IO-9, P3a-12).</b> <see cref="ReadAsBytesAsync"/>, <see cref="ReadAsStringAsync"/> and their
/// sync twins refuse a body larger than <see cref="DefaultMaxMaterializedBytes"/> with
/// <see cref="BodyTooLargeException"/>, before reading when the declared length is already above it. To read more, stream
/// the body through <see cref="OpenRead"/> or <see cref="OpenReadAsync"/>. A subclass that overrides a reader opts out of
/// the cap by overriding it.
/// </para>
/// </remarks>
public abstract class ResponseBody : IAsyncDisposable, IDisposable
{
    /// <summary>The media type declared by the response, or <see langword="null"/> if absent.</summary>
    public abstract MediaType? ContentType { get; }

    /// <summary>
    /// The declared length in bytes from <c>Content-Length</c>, or <c>-1</c> when not provided.
    /// </summary>
    public virtual long ContentLength => -1;

    // True for the empty replayable body, the shape design §10 entry 30 gives an absent body: it holds no connection and
    // nothing to drain (BODY-30, P4c-18).
    internal virtual bool IsEmptyReplayable => false;

    // The one message every second open throws (BODY-14): it names the buffering route. SystemNet repeats it verbatim.
    internal const string ConsumedMessage =
        "This response body has already been read. A response body can be opened once; to read it more than once, "
        + "buffer it first (read it with ReadAsBytesAsync and keep the bytes, or keep the bytes and wrap them in a new ResponseBody.FromBytes for each read).";

    /// <summary>The cap the convenience readers apply: 64 MiB (IO-9, P3a-12).</summary>
    public const long DefaultMaxMaterializedBytes = 64L * 1024 * 1024;

    /// <summary>Opens the underlying payload stream for reading.</summary>
    /// <param name="cancellationToken">A token to cancel opening the stream.</param>
    /// <returns>The payload stream; the caller must not dispose it independently of this body.</returns>
    /// <exception cref="StreamConsumedException">The body has already been opened, in either form.</exception>
    public abstract Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens the underlying payload stream for reading, synchronously.</summary>
    /// <remarks>
    /// The synchronous twin of <see cref="OpenReadAsync"/> (P3a-5). It shares the open latch with it: a second open in
    /// either form throws <see cref="StreamConsumedException"/> (<b>Breaking</b> in the sense of a new interaction: it is a
    /// new member, and it makes the existing single-open rule hold across forms). The base implementation throws
    /// <see cref="NotSupportedException"/> naming the subclass; override it to support the sync path. A transport's own
    /// body inherits that default until the transport implements it.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel opening the stream.</param>
    /// <returns>The payload stream; the caller must not dispose it independently of this body.</returns>
    /// <exception cref="NotSupportedException">The subclass does not override this member.</exception>
    /// <exception cref="StreamConsumedException">The body has already been opened, in either form.</exception>
    public virtual Stream OpenRead(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{GetType().Name} does not support synchronous reads; override OpenRead.");

    /// <summary>Fully reads the payload into a byte array, then closes the stream and the body.</summary>
    /// <remarks>
    /// <b>Breaking:</b> the body is now disposed when the read finishes, on success and on failure (BODY-16, P3b-13); it
    /// used to be left open. The read was also unbounded; it now throws <see cref="BodyTooLargeException"/> for a body larger than
    /// <see cref="DefaultMaxMaterializedBytes"/> (64 MiB), before reading when the declared length is already above it. Read
    /// a larger body as a stream through <see cref="OpenReadAsync"/>.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The payload bytes; empty for an empty body.</returns>
    /// <exception cref="BodyTooLargeException">The body is larger than <see cref="DefaultMaxMaterializedBytes"/>.</exception>
    public virtual Task<byte[]> ReadAsBytesAsync(CancellationToken cancellationToken = default) =>
        ReadAsBytesBoundedAsync(DefaultMaxMaterializedBytes, cancellationToken);

    /// <summary>Fully reads the payload into a byte array, synchronously, then closes the stream and the body.</summary>
    /// <remarks>
    /// The synchronous twin of <see cref="ReadAsBytesAsync"/>, built over <see cref="OpenRead"/> and bounded by
    /// <see cref="DefaultMaxMaterializedBytes"/> in the same way.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The payload bytes; empty for an empty body.</returns>
    /// <exception cref="BodyTooLargeException">The body is larger than <see cref="DefaultMaxMaterializedBytes"/>.</exception>
    /// <exception cref="NotSupportedException">The subclass does not override <see cref="OpenRead"/>.</exception>
    public virtual byte[] ReadAsBytes(CancellationToken cancellationToken = default) =>
        ReadAsBytesBounded(DefaultMaxMaterializedBytes, cancellationToken);

    /// <summary>
    /// Fully reads the payload and decodes it as text, then closes the stream and the body. Uses the charset from
    /// <see cref="ContentType"/> when present, otherwise UTF-8.
    /// </summary>
    /// <remarks>
    /// <b>Breaking:</b> a leading byte-order mark that matches the resolved charset's preamble is now stripped (HTTP-42,
    /// P3b-12); it used to decode to a leading U+FEFF. A mark that does not match the declared charset is kept. The body is
    /// also disposed when the read finishes (BODY-16). The read was unbounded; it is now capped at <see cref="DefaultMaxMaterializedBytes"/> exactly as
    /// <see cref="ReadAsBytesAsync"/> is.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The decoded text.</returns>
    /// <exception cref="BodyTooLargeException">The body is larger than <see cref="DefaultMaxMaterializedBytes"/>.</exception>
    public virtual Task<string> ReadAsStringAsync(CancellationToken cancellationToken = default) =>
        ReadAsStringBoundedAsync(DefaultMaxMaterializedBytes, cancellationToken);

    /// <summary>Fully reads the payload and decodes it as text, synchronously, then closes the stream and the body.</summary>
    /// <remarks>
    /// The synchronous twin of <see cref="ReadAsStringAsync"/>: the same cap, and the same decode routine (IO-13).
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The decoded text.</returns>
    /// <exception cref="BodyTooLargeException">The body is larger than <see cref="DefaultMaxMaterializedBytes"/>.</exception>
    /// <exception cref="NotSupportedException">The subclass does not override <see cref="OpenRead"/>.</exception>
    public virtual string ReadAsString(CancellationToken cancellationToken = default) =>
        ReadAsStringBounded(DefaultMaxMaterializedBytes, cancellationToken);

    // The bounded readers behind the public ones; tests lower the limit through them (plan reading R1).
    internal async Task<byte[]> ReadAsBytesBoundedAsync(long limit, CancellationToken cancellationToken)
    {
        try
        {
            // The body owns the stream (BODY-15): the finally below releases it exactly once.
            var stream = await OpenReadAsync(cancellationToken).ConfigureAwait(false);
            return await BodyMaterializer.ReadAllAsync(stream, ContentLength, limit, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // BODY-16: the reader closes the body, not only the stream, on success and on failure.
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    internal byte[] ReadAsBytesBounded(long limit, CancellationToken cancellationToken)
    {
        try
        {
            var stream = OpenRead(cancellationToken); // released once, by the body, in the finally
            return BodyMaterializer.ReadAll(stream, ContentLength, limit, cancellationToken);
        }
        finally
        {
            Dispose();
        }
    }

    internal async Task<string> ReadAsStringBoundedAsync(long limit, CancellationToken cancellationToken)
    {
        var bytes = await ReadAsBytesBoundedAsync(limit, cancellationToken).ConfigureAwait(false);
        return TextDecoding.Decode(bytes, ContentType?.Charset);
    }

    internal string ReadAsStringBounded(long limit, CancellationToken cancellationToken) =>
        TextDecoding.Decode(ReadAsBytesBounded(limit, cancellationToken), ContentType?.Charset);

    /// <summary>Creates a buffered, in-memory response body (useful for tests and replay).</summary>
    /// <param name="bytes">The payload bytes.</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <returns>A <see cref="ResponseBody"/> backed by the supplied bytes.</returns>
    public static ResponseBody FromBytes(ReadOnlyMemory<byte> bytes, MediaType? contentType = null) =>
        new BytesResponseBody(bytes.ToArray(), contentType);

    /// <summary>
    /// Creates an in-memory body that, unlike <see cref="FromBytes"/>, can be read any number of times. Used for the
    /// buffered error body an <see cref="Errors.HttpResponseException"/> carries (HTTP-52, BODY-30).
    /// </summary>
    internal static ResponseBody FromReplayableBytes(byte[] bytes, MediaType? contentType) =>
        new ReplayableBytesResponseBody(bytes, contentType);

    /// <summary>Creates a streaming response body wrapping <paramref name="source"/>.</summary>
    /// <param name="source">The payload stream (owned by the returned body).</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <param name="contentLength">The declared length, or <c>-1</c>.</param>
    /// <returns>A single-use <see cref="ResponseBody"/>.</returns>
    /// <remarks>
    /// <b>Breaking:</b> a <paramref name="contentLength"/> below <c>-1</c> used to be accepted; it now throws
    /// <see cref="ArgumentOutOfRangeException"/> at construction (IO-3).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="contentLength"/> is below <c>-1</c>.</exception>
    public static ResponseBody FromStream(Stream source, MediaType? contentType = null, long contentLength = -1)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(contentLength, -1L);
        return new StreamResponseBody(source, contentType, contentLength);
    }

    private int _disposed;

    /// <summary>Releases the body, at most once across <see cref="Dispose()"/> and <see cref="DisposeAsync"/>.</summary>
    /// <remarks>
    /// <b>Breaking:</b> this was <c>public virtual</c>. Override <see cref="Dispose(bool)"/> instead.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the body asynchronously, at most once across <see cref="Dispose()"/> and this method.</summary>
    /// <remarks>
    /// <b>Breaking:</b> this was <c>public virtual</c>. Override <see cref="DisposeAsyncCore"/> instead.
    /// </remarks>
    /// <returns>A task that completes when the release has finished.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the resources the body holds; called at most once.</summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/> or <see cref="DisposeAsync"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>The asynchronous release; called at most once. The default calls <c>Dispose(true)</c>.</summary>
    /// <returns>A task that completes when the release has finished.</returns>
    protected virtual ValueTask DisposeAsyncCore()
    {
        Dispose(disposing: true);
        return ValueTask.CompletedTask;
    }

    private sealed class BytesResponseBody(byte[] bytes, MediaType? contentType) : ResponseBody
    {
        private int _consumed;

        public override MediaType? ContentType { get; } = contentType;

        public override long ContentLength => bytes.LongLength;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            ClaimOnce();
            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }

        public override Stream OpenRead(CancellationToken cancellationToken = default)
        {
            ClaimOnce();
            return new MemoryStream(bytes, writable: false);
        }

        // One latch for both forms (position B): whichever open runs first claims the body.
        private void ClaimOnce()
        {
            if (Interlocked.Exchange(ref _consumed, 1) != 0)
            {
                throw new StreamConsumedException(ConsumedMessage);
            }
        }
    }

    private sealed class ReplayableBytesResponseBody(byte[] bytes, MediaType? contentType) : ResponseBody
    {
        internal override bool IsEmptyReplayable => bytes.Length == 0;

        public override MediaType? ContentType { get; } = contentType;

        public override long ContentLength => bytes.LongLength;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));

        public override Stream OpenRead(CancellationToken cancellationToken = default) =>
            new MemoryStream(bytes, writable: false);
    }

    private sealed class StreamResponseBody(Stream source, MediaType? contentType, long contentLength) : ResponseBody
    {
        private int _consumed;
        private int _closed;
        private int _sourceReleased;

        public override MediaType? ContentType { get; } = contentType;

        public override long ContentLength => contentLength;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            Claim();
            return Task.FromResult(source);
        }

        public override Stream OpenRead(CancellationToken cancellationToken = default)
        {
            Claim();
            return source;
        }

        // One latch for both forms (position B). The consumed check runs first, so a body that was read and then
        // disposed still reports "consumed" (P3b-11); a body disposed before any open reports "closed".
        private void Claim()
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
        }

        protected override void Dispose(bool disposing)
        {
            Volatile.Write(ref _closed, 1);
            if (disposing && Interlocked.Exchange(ref _sourceReleased, 1) == 0)
            {
                source.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            Volatile.Write(ref _closed, 1);
            if (Interlocked.Exchange(ref _sourceReleased, 1) == 0)
            {
                await source.DisposeAsync().ConfigureAwait(false);
            }

            // The base routes to Dispose(true), which finds the source already released.
            await base.DisposeAsyncCore().ConfigureAwait(false);
        }
    }
}
