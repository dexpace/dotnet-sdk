// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// Materialises a stream into a byte array under a size limit (IO-9, IO-11, P3a-12): a declared length above the limit is
/// refused before any read; otherwise the stream is drained up to the limit, and when the drain stopped at the limit
/// without seeing the end, one more byte is probed (R4): any byte means the body is larger and the call throws.
/// </summary>
/// <remarks>
/// Stateless, taking one stream per call (IO-37). It never issues a zero-count read (IO-2): the probe uses a one-byte
/// buffer. It does not dispose the stream; the caller owns it.
/// </remarks>
internal static class BodyMaterializer
{
    /// <summary>Reads every remaining byte of <paramref name="source"/>, refusing more than <paramref name="limit"/>.</summary>
    /// <param name="source">The stream to drain.</param>
    /// <param name="declaredLength">The declared length, or a negative number when unknown.</param>
    /// <param name="limit">The most bytes to accept; non-negative.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The bytes; an empty array for an empty stream (IO-11).</returns>
    /// <exception cref="BodyTooLargeException">The body is larger than <paramref name="limit"/>.</exception>
    internal static byte[] ReadAll(Stream source, long declaredLength, long limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        RefuseDeclaredLength(declaredLength, limit);
        var writer = new ArrayBufferWriter<byte>();
        if (!StreamCopy.DrainUpTo(source, writer, limit, cancellationToken))
        {
            var probe = new byte[1];
            if (source.Read(probe, 0, 1) != 0)
            {
                throw TooLarge(limit, declaredLength);
            }
        }

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Asynchronous form of <see cref="ReadAll"/>.</summary>
    /// <param name="source">The stream to drain.</param>
    /// <param name="declaredLength">The declared length, or a negative number when unknown.</param>
    /// <param name="limit">The most bytes to accept; non-negative.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The bytes; an empty array for an empty stream (IO-11).</returns>
    internal static async ValueTask<byte[]> ReadAllAsync(
        Stream source,
        long declaredLength,
        long limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        RefuseDeclaredLength(declaredLength, limit);
        var writer = new ArrayBufferWriter<byte>();
        if (!await StreamCopy.DrainUpToAsync(source, writer, limit, cancellationToken).ConfigureAwait(false))
        {
            var probe = new byte[1];
            if (await source.ReadAsync(probe.AsMemory(), cancellationToken).ConfigureAwait(false) != 0)
            {
                throw TooLarge(limit, declaredLength);
            }
        }

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Builds the refusal for a body over <paramref name="limit"/>, naming the limit and any declared length.</summary>
    /// <param name="limit">The limit that was exceeded.</param>
    /// <param name="declaredLength">The declared length, or a negative number when unknown.</param>
    /// <returns>The exception to throw.</returns>
    internal static BodyTooLargeException TooLarge(long limit, long declaredLength)
    {
        var declared = declaredLength >= 0 ? $" (declared length {declaredLength} bytes)" : string.Empty;
        return new BodyTooLargeException(
            $"The body exceeds the limit of {limit} bytes{declared}; read it as a stream with OpenRead or OpenReadAsync instead of buffering it.");
    }

    internal static void RefuseDeclaredLength(long declaredLength, long limit)
    {
        if (declaredLength > limit)
        {
            throw TooLarge(limit, declaredLength);
        }
    }
}
