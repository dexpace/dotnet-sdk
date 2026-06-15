// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// A typed abstraction over an incoming response payload.
/// </summary>
/// <remarks>
/// The body is not pre-buffered. <see cref="OpenReadAsync"/> exposes the raw stream;
/// <see cref="ReadAsBytesAsync"/> and <see cref="ReadAsStringAsync"/> fully drain and then close
/// it. Reads are single-use: a second read after the stream is consumed raises
/// <see cref="StreamConsumedException"/>. Always dispose the body (directly or via the owning
/// <see cref="Response"/>) to release the underlying connection.
/// </remarks>
public abstract class ResponseBody : IAsyncDisposable, IDisposable
{
    /// <summary>The media type declared by the response, or <see langword="null"/> if absent.</summary>
    public abstract MediaType? ContentType { get; }

    /// <summary>
    /// The declared length in bytes from <c>Content-Length</c>, or <c>-1</c> when not provided.
    /// </summary>
    public virtual long ContentLength => -1;

    /// <summary>Opens the underlying payload stream for reading.</summary>
    /// <param name="cancellationToken">A token to cancel opening the stream.</param>
    /// <returns>The payload stream; the caller must not dispose it independently of this body.</returns>
    /// <exception cref="StreamConsumedException">The body has already been read.</exception>
    public abstract Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Fully reads the payload into a byte array, then closes the stream.</summary>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The payload bytes.</returns>
    public virtual async Task<byte[]> ReadAsBytesAsync(CancellationToken cancellationToken = default)
    {
        await using var stream = await OpenReadAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// Fully reads the payload and decodes it as text, then closes the stream. Uses the charset from
    /// <see cref="ContentType"/> when present, otherwise UTF-8.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The decoded text.</returns>
    public virtual async Task<string> ReadAsStringAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAsBytesAsync(cancellationToken).ConfigureAwait(false);
        var encoding = ContentType?.Charset ?? Encoding.UTF8;
        return encoding.GetString(bytes);
    }

    /// <summary>Creates a buffered, in-memory response body (useful for tests and replay).</summary>
    /// <param name="bytes">The payload bytes.</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <returns>A <see cref="ResponseBody"/> backed by the supplied bytes.</returns>
    public static ResponseBody FromBytes(ReadOnlyMemory<byte> bytes, MediaType? contentType = null) =>
        new BytesResponseBody(bytes.ToArray(), contentType);

    /// <summary>Creates a streaming response body wrapping <paramref name="source"/>.</summary>
    /// <param name="source">The payload stream (owned by the returned body).</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <param name="contentLength">The declared length, or <c>-1</c>.</param>
    /// <returns>A single-use <see cref="ResponseBody"/>.</returns>
    public static ResponseBody FromStream(Stream source, MediaType? contentType = null, long contentLength = -1)
    {
        ArgumentNullException.ThrowIfNull(source);
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
            if (Interlocked.Exchange(ref _consumed, 1) != 0)
            {
                throw new StreamConsumedException("This response body has already been read.");
            }

            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
    }

    private sealed class StreamResponseBody(Stream source, MediaType? contentType, long contentLength) : ResponseBody
    {
        private int _consumed;

        public override MediaType? ContentType { get; } = contentType;

        public override long ContentLength => contentLength;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _consumed, 1) != 0)
            {
                throw new StreamConsumedException("This response body has already been read.");
            }

            return Task.FromResult(source);
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
