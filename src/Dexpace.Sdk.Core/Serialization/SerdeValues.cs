// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// The rules the typed readers and the response handlers share (SERDE-13, SERDE-27): the root-null check, the
/// missing-body message, and the one-byte peek that tells an absent payload from a present one without materialising it.
/// </summary>
/// <remarks>
/// <see cref="RequireNonNull{T}"/> is also the one place the wording of the root-null failure lives, so the typed SSE
/// adapter can reject a wire <c>null</c> the same way (design 7.3; offered to phase 7b).
/// </remarks>
internal static class SerdeValues
{
    /// <summary>
    /// Returns <paramref name="value"/>, or throws when it is <see langword="null"/> and <typeparamref name="T"/> is a
    /// reference type (SERDE-13).
    /// </summary>
    /// <typeparam name="T">The target type; a <see cref="Nullable{T}"/> target legitimately decodes <c>null</c>.</typeparam>
    /// <param name="value">What the codec returned.</param>
    /// <returns>The value, never <see langword="null"/> for a reference type.</returns>
    /// <exception cref="DeserializationException">The codec returned <see langword="null"/> for a non-nullable reference type.</exception>
    internal static T RequireNonNull<T>(T? value) =>
        value is null && !typeof(T).IsValueType
            ? throw new DeserializationException(
                $"The response body is the JSON literal null, but '{typeof(T)}' is not nullable; use ReadValueOrDefaultAsync to accept null.")
            : value!;

    /// <summary>The failure for a response that has no body (a 204, a zero-length body) (SERDE-27).</summary>
    /// <typeparam name="T">The type that was asked for.</typeparam>
    /// <returns>The exception to throw; it has no inner exception.</returns>
    internal static DeserializationException NoBody<T>() =>
        new($"The response has no body to deserialize as '{typeof(T)}'.");

    /// <summary>
    /// Reads one byte of <paramref name="stream"/> to learn whether a payload exists, and returns a stream that replays it
    /// (P7a-10). Takes ownership of <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The live body stream.</param>
    /// <returns>
    /// A stream over the whole payload, which disposes <paramref name="stream"/> exactly once; or <see langword="null"/> for
    /// an empty stream, in which case <paramref name="stream"/> has already been disposed.
    /// </returns>
    /// <exception cref="IOException">The first read failed; <paramref name="stream"/> has been disposed.</exception>
    internal static Stream? PeekFirstByte(Stream stream)
    {
        var first = new byte[1];
        int read;
        try
        {
            read = stream.Read(first, 0, 1);
        }
        catch (Exception ex)
        {
            Disposal.DisposeQuietly(stream, ex);
            throw;
        }

        if (read == 0)
        {
            stream.Dispose();
            return null;
        }

        var latch = new CloseOnce(stream);
        return new PrefixedReadStream(first, 1, null, stream, latch.Close, latch.CloseAsync);
    }

    /// <summary>Asynchronous form of <see cref="PeekFirstByte"/>.</summary>
    /// <param name="stream">The live body stream.</param>
    /// <param name="cancellationToken">A token to cancel the first read.</param>
    /// <returns>As <see cref="PeekFirstByte"/>.</returns>
    internal static async ValueTask<Stream?> PeekFirstByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var first = new byte[1];
        int read;
        try
        {
            read = await stream.ReadAsync(first.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Disposal.DisposeQuietlyAsync(stream, ex).ConfigureAwait(false);
            throw;
        }

        if (read == 0)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        var latch = new CloseOnce(stream);
        return new PrefixedReadStream(first, 1, null, stream, latch.Close, latch.CloseAsync);
    }

    // PrefixedReadStream calls its close delegate when the tail reaches the end and again on Dispose, so the raw dispose
    // delegates would release the real stream twice; this latch makes whichever comes first the only one.
    private sealed class CloseOnce(Stream stream)
    {
        private int _closed;

        internal void Close()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                stream.Dispose();
            }
        }

        internal ValueTask CloseAsync() =>
            Interlocked.Exchange(ref _closed, 1) == 0 ? stream.DisposeAsync() : ValueTask.CompletedTask;
    }
}
