// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
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
/// any number of times. Always dispose the body (directly or via the owning
/// <see cref="Response"/>) to release the underlying connection.
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

    /// <summary>Fully reads the payload into a byte array, then closes the stream.</summary>
    /// <remarks>
    /// <b>Breaking:</b> the read was unbounded; it now throws <see cref="BodyTooLargeException"/> for a body larger than
    /// <see cref="DefaultMaxMaterializedBytes"/> (64 MiB), before reading when the declared length is already above it. Read
    /// a larger body as a stream through <see cref="OpenReadAsync"/>.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The payload bytes; empty for an empty body.</returns>
    /// <exception cref="BodyTooLargeException">The body is larger than <see cref="DefaultMaxMaterializedBytes"/>.</exception>
    public virtual Task<byte[]> ReadAsBytesAsync(CancellationToken cancellationToken = default) =>
        ReadAsBytesBoundedAsync(DefaultMaxMaterializedBytes, cancellationToken);

    /// <summary>Fully reads the payload into a byte array, synchronously, then closes the stream.</summary>
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
    /// Fully reads the payload and decodes it as text, then closes the stream. Uses the charset from
    /// <see cref="ContentType"/> when present, otherwise UTF-8.
    /// </summary>
    /// <remarks>
    /// <b>Breaking:</b> the read was unbounded; it is now capped at <see cref="DefaultMaxMaterializedBytes"/> exactly as
    /// <see cref="ReadAsBytesAsync"/> is.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The decoded text.</returns>
    /// <exception cref="BodyTooLargeException">The body is larger than <see cref="DefaultMaxMaterializedBytes"/>.</exception>
    public virtual Task<string> ReadAsStringAsync(CancellationToken cancellationToken = default) =>
        ReadAsStringBoundedAsync(DefaultMaxMaterializedBytes, cancellationToken);

    /// <summary>Fully reads the payload and decodes it as text, synchronously, then closes the stream.</summary>
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
        var stream = await OpenReadAsync(cancellationToken).ConfigureAwait(false);
        await using var streamScope = stream.ConfigureAwait(false);
        return await BodyMaterializer.ReadAllAsync(stream, ContentLength, limit, cancellationToken).ConfigureAwait(false);
    }

    internal byte[] ReadAsBytesBounded(long limit, CancellationToken cancellationToken)
    {
        using var stream = OpenRead(cancellationToken);
        return BodyMaterializer.ReadAll(stream, ContentLength, limit, cancellationToken);
    }

    internal async Task<string> ReadAsStringBoundedAsync(long limit, CancellationToken cancellationToken)
    {
        var bytes = await ReadAsBytesBoundedAsync(limit, cancellationToken).ConfigureAwait(false);
        return Decode(bytes, ContentType);
    }

    internal string ReadAsStringBounded(long limit, CancellationToken cancellationToken) =>
        Decode(ReadAsBytesBounded(limit, cancellationToken), ContentType);

    // The one decode routine both string readers share (IO-13, plan reading R3): the declared charset, else UTF-8.
    private static string Decode(byte[] bytes, MediaType? contentType) =>
        (contentType?.Charset ?? Encoding.UTF8).GetString(bytes);

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

    /// <inheritdoc/>
    public virtual void Dispose() => GC.SuppressFinalize(this);

    /// <inheritdoc/>
    public virtual ValueTask DisposeAsync()
    {
        Dispose();
        GC.SuppressFinalize(this);
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
                throw new StreamConsumedException("This response body has already been read.");
            }
        }
    }

    private sealed class ReplayableBytesResponseBody(byte[] bytes, MediaType? contentType) : ResponseBody
    {
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

        public override MediaType? ContentType { get; } = contentType;

        public override long ContentLength => contentLength;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            ClaimOnce();
            return Task.FromResult(source);
        }

        public override Stream OpenRead(CancellationToken cancellationToken = default)
        {
            ClaimOnce();
            return source;
        }

        // One latch for both forms (position B): whichever open runs first claims the body.
        private void ClaimOnce()
        {
            if (Interlocked.Exchange(ref _consumed, 1) != 0)
            {
                throw new StreamConsumedException("This response body has already been read.");
            }
        }

        public override void Dispose()
        {
            source.Dispose();
            base.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await source.DisposeAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
