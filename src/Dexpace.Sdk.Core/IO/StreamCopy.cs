// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// The copy primitives behind the stream bodies, the capped drain and the materialisers (IO-1, IO-2, IO-12, IO-17,
/// HTTP-39, HTTP-52). None of them disposes either stream: they are not wrappers (IO-6).
/// </summary>
/// <remarks>
/// <para>
/// Core never issues a zero-count read (IO-2; design §10 <c>eof-is-zero-read</c>): every read requests at least one byte,
/// so a zero result is always the end of the stream. Reads of a short source are reassembled in order (IO-1).
/// </para>
/// <para>
/// Single-threaded contract (IO-37): the helpers hold no shared state, one call per stream at a time, and no concurrent
/// use of the same stream. The synchronous forms check the cancellation token between chunks and nowhere else (IO-40);
/// the asynchronous forms check it before each chunk and pass it to the read and the write.
/// </para>
/// </remarks>
internal static class StreamCopy
{
    /// <summary>The transient chunk size, below the large-object-heap threshold.</summary>
    internal const int ChunkSize = 81_920;

    /// <summary>The one message form for HTTP-39, BODY-10 and BODY-13.</summary>
    /// <param name="delivered">The bytes delivered before the source ended.</param>
    /// <param name="total">The bytes that were required.</param>
    /// <returns>The message text.</returns>
    internal static string ShortTransferMessage(long delivered, long total) =>
        $"The source ended after {delivered} of {total} bytes.";

    /// <summary>Pumps <paramref name="source"/> to its end (IO-17) and returns the byte total.</summary>
    internal static long CopyToEnd(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        using var chunk = PooledChunk.Rent(ChunkSize);
        long total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = source.Read(chunk.Array, 0, ChunkSize);
            if (read == 0)
            {
                return total;
            }

            destination.Write(chunk.Array, 0, read);
            total += read;
        }
    }

    /// <summary>Pumps <paramref name="source"/> to its end (IO-17) and returns the byte total.</summary>
    internal static async ValueTask<long> CopyToEndAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        using var chunk = PooledChunk.Rent(ChunkSize);
        long total = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(chunk.Array.AsMemory(0, ChunkSize), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return total;
            }

            await destination.WriteAsync(chunk.Array.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
        }
    }

    /// <summary>
    /// Copies exactly <paramref name="count"/> bytes (HTTP-39, IO-12). A premature end throws
    /// <see cref="EndOfStreamException"/> naming delivered-of-total; a count of zero performs no read; the source's
    /// remainder is never read.
    /// </summary>
    internal static void CopyExactly(Stream source, Stream destination, long count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
        {
            return;
        }

        using var chunk = PooledChunk.Rent((int)Math.Min(ChunkSize, count));
        long delivered = 0;
        while (delivered < count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var want = (int)Math.Min(chunk.Array.Length, Math.Min(ChunkSize, count - delivered));
            var read = source.Read(chunk.Array, 0, want);
            if (read == 0)
            {
                throw new EndOfStreamException(ShortTransferMessage(delivered, count));
            }

            destination.Write(chunk.Array, 0, read);
            delivered += read;
        }
    }

    /// <summary>Asynchronous form of <see cref="CopyExactly"/>.</summary>
    internal static async ValueTask CopyExactlyAsync(
        Stream source,
        Stream destination,
        long count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
        {
            return;
        }

        using var chunk = PooledChunk.Rent((int)Math.Min(ChunkSize, count));
        long delivered = 0;
        while (delivered < count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var want = (int)Math.Min(chunk.Array.Length, Math.Min(ChunkSize, count - delivered));
            var read = await source.ReadAsync(chunk.Array.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException(ShortTransferMessage(delivered, count));
            }

            await destination.WriteAsync(chunk.Array.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            delivered += read;
        }
    }

    /// <summary>
    /// Appends up to <paramref name="maxBytes"/> bytes of <paramref name="source"/> to <paramref name="into"/> (so the
    /// bytes read before a failure survive the throw). Returns <see langword="true"/> when the end of the stream was seen
    /// before the cap, and <see langword="false"/> when the drain stopped at the cap without seeing the end (an exactly-cap
    /// source reports <see langword="false"/>; a caller that must tell the two apart probes one more byte).
    /// </summary>
    internal static bool DrainUpTo(
        Stream source,
        ArrayBufferWriter<byte> into,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(into);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        if (maxBytes == 0)
        {
            return false;
        }

        using var chunk = PooledChunk.Rent((int)Math.Min(ChunkSize, maxBytes));
        var remaining = maxBytes;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var want = (int)Math.Min(chunk.Array.Length, Math.Min(ChunkSize, remaining));
            var read = source.Read(chunk.Array, 0, want);
            if (read == 0)
            {
                return true;
            }

            into.Write(chunk.Array.AsSpan(0, read));
            remaining -= read;
        }

        return false;
    }

    /// <summary>Asynchronous form of <see cref="DrainUpTo"/>.</summary>
    internal static async ValueTask<bool> DrainUpToAsync(
        Stream source,
        ArrayBufferWriter<byte> into,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(into);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        if (maxBytes == 0)
        {
            return false;
        }

        using var chunk = PooledChunk.Rent((int)Math.Min(ChunkSize, maxBytes));
        var remaining = maxBytes;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var want = (int)Math.Min(chunk.Array.Length, Math.Min(ChunkSize, remaining));
            var read = await source.ReadAsync(chunk.Array.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return true;
            }

            into.Write(chunk.Array.AsSpan(0, read));
            remaining -= read;
        }

        return false;
    }
}
