// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// A typed abstraction over an outgoing request payload.
/// </summary>
/// <remarks>
/// <see cref="WriteToAsync"/> is the primary streaming surface; transports call it to drain the
/// body to the wire. Implementations differ on whether they can be replayed (see
/// <see cref="IsReplayable"/>): byte- and string-backed bodies are replayable, while
/// stream-backed bodies are single-use and raise <see cref="StreamConsumedException"/> on a
/// second write. Call <see cref="ToReplayableAsync"/> before the first send if retries are
/// needed. Use the static factories rather than subclassing for the common cases.
/// <para>
/// <b>Equality contract (HTTP-46; position A).</b> <see cref="RequestBody"/> itself keeps reference equality, because
/// it is open and a subclass the SDK knows nothing about gets the only safe default. The in-memory variant behind
/// <see cref="FromBytes"/>, <see cref="FromString"/>, <see cref="FromValue{T}"/> and <see cref="ToReplayableAsync"/>
/// overrides <c>Equals</c> and <c>GetHashCode</c>: two such bodies are equal when their <see cref="ContentType"/>
/// values are equal and their bytes are equal, and the hash covers only the content type and length, so hashing stays
/// O(1). The single-use stream variant behind <see cref="FromStream"/> keeps identity: two bodies over two streams are
/// two values, and a body equals itself. A body whose bytes are a construction-time fact compares by value; one over a
/// live source compares by identity. Every body variant added later (file, form-urlencoded, multipart, logging
/// wrappers) follows the same rule.
/// </para>
/// </remarks>
public abstract class RequestBody
{
    /// <summary>The media type describing the payload, or <see langword="null"/> if unknown.</summary>
    public abstract MediaType? ContentType { get; }

    /// <summary>
    /// The payload length in bytes, or <c>-1</c> when not known ahead of time (the transport then
    /// uses chunked transfer-encoding).
    /// </summary>
    public virtual long ContentLength => -1;

    /// <summary>
    /// True when the body can be written more than once (required for transparent retries).
    /// </summary>
    public virtual bool IsReplayable => false;

    /// <summary>Writes the entire payload to <paramref name="destination"/>.</summary>
    /// <param name="destination">The stream to write the body to.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>A task that completes when the body has been fully written.</returns>
    /// <exception cref="StreamConsumedException">A single-use body was written more than once.</exception>
    public abstract Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a replayable equivalent of this body. If <see cref="IsReplayable"/> is already
    /// <see langword="true"/>, returns this instance; otherwise drains the payload into memory and
    /// returns a buffered copy.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the buffering.</param>
    /// <returns>A replayable <see cref="RequestBody"/>.</returns>
    public virtual async Task<RequestBody> ToReplayableAsync(CancellationToken cancellationToken = default)
    {
        if (IsReplayable)
        {
            return this;
        }

        using var buffer = new MemoryStream();
        await WriteToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return new BytesRequestBody(buffer.ToArray(), ContentType);
    }

    /// <summary>Creates a replayable body from an in-memory byte buffer.</summary>
    /// <param name="bytes">The payload bytes (copied defensively).</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <returns>A replayable <see cref="RequestBody"/>.</returns>
    public static RequestBody FromBytes(ReadOnlyMemory<byte> bytes, MediaType? contentType = null) =>
        new BytesRequestBody(bytes.ToArray(), contentType);

    /// <summary>
    /// Creates a replayable body from a string. Defaults to UTF-8 and
    /// <see cref="CommonMediaTypes.TextPlain"/> with a charset parameter when no type is given.
    /// </summary>
    /// <param name="text">The text payload.</param>
    /// <param name="contentType">The media type, or <see langword="null"/> for text/plain.</param>
    /// <param name="encoding">The encoding, or <see langword="null"/> for UTF-8.</param>
    /// <returns>A replayable <see cref="RequestBody"/>.</returns>
    public static RequestBody FromString(string text, MediaType? contentType = null, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var enc = encoding ?? Encoding.UTF8;
        var type = contentType ?? MediaType.Of(
            "text",
            "plain",
            new Dictionary<string, string> { ["charset"] = enc.WebName });
        return new BytesRequestBody(enc.GetBytes(text), type);
    }

    /// <summary>
    /// Creates a replayable body by serializing <paramref name="value"/> with <paramref name="serde"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <param name="serde">The serializer.</param>
    /// <param name="contentType">The media type, or <see langword="null"/> for the serde's default.</param>
    /// <returns>A replayable <see cref="RequestBody"/>.</returns>
    public static RequestBody FromValue<T>(T value, ISerde serde, MediaType? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(serde);
        var buffer = new ArrayBufferWriter<byte>();
        serde.Serialize(buffer, value);
        return FromBytes(buffer.WrittenMemory, contentType ?? serde.DefaultMediaType);
    }

    /// <summary>
    /// Creates a single-use body that streams from <paramref name="source"/>. The source is read
    /// exactly once; call <see cref="ToReplayableAsync"/> first if retries are needed.
    /// </summary>
    /// <param name="source">The stream to read the payload from.</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <param name="contentLength">The known length, or <c>-1</c> if unknown.</param>
    /// <returns>A single-use <see cref="RequestBody"/>.</returns>
    public static RequestBody FromStream(Stream source, MediaType? contentType = null, long contentLength = -1)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new StreamRequestBody(source, contentType, contentLength);
    }

    private sealed class BytesRequestBody(byte[] bytes, MediaType? contentType) : RequestBody
    {
        public override MediaType? ContentType { get; } = contentType;

        public override long ContentLength => bytes.LongLength;

        public override bool IsReplayable => true;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            return destination.WriteAsync(bytes, cancellationToken).AsTask();
        }

        // HTTP-46: same variant, equal content types, equal bytes. The hash excludes the bytes (equal bytes have equal
        // lengths), so hashing a large payload is O(1) and the O(n) comparison runs only when type and length match.
        public override bool Equals(object? obj) =>
            ReferenceEquals(this, obj)
            || (obj is BytesRequestBody other
                && Equals(ContentType, other.ContentType)
                && bytes.AsSpan().SequenceEqual(other.Bytes));

        public override int GetHashCode() => HashCode.Combine(ContentType, bytes.LongLength);

        private byte[] Bytes => bytes;
    }

    private sealed class StreamRequestBody(Stream source, MediaType? contentType, long contentLength) : RequestBody
    {
        private int _consumed;

        public override MediaType? ContentType { get; } = contentType;

        public override long ContentLength => contentLength;

        public override bool IsReplayable => false;

        public override async Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destination);
            if (Interlocked.Exchange(ref _consumed, 1) != 0)
            {
                throw new StreamConsumedException(
                    "This request body is single-use and has already been written. "
                    + "Call ToReplayableAsync() before the first send if retries are needed.");
            }

            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
    }
}
