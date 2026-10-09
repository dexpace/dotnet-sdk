// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>Serialization conveniences over <see cref="ResponseBody"/>: the typed readers (SERDE-7, SERDE-13, SERDE-27).</summary>
/// <remarks>
/// <para>
/// Each reader streams: it opens the body, peeks one byte to tell a missing payload from a present one, and hands the codec
/// a stream that replays that byte, so nothing is materialised. Each reader <b>disposes the body</b> on every path, success,
/// codec failure, <see cref="IOException"/> and root <c>null</c> alike (the single-use body is useless afterwards); a failure
/// to dispose after a failed read is attached to that failure's trail and never replaces it.
/// </para>
/// <para>
/// <see cref="ReadValueAsync{T}"/> and <see cref="ReadValue{T}"/> return a non-null value of the target type: a wire
/// <c>null</c> for a reference type is a <see cref="Errors.DeserializationException"/> naming the type
/// (<c>Nullable&lt;&gt;</c> targets legitimately decode <c>null</c>). <see cref="ReadValueOrDefaultAsync{T}"/> and
/// <see cref="ReadValueOrDefault{T}"/> are the explicit nullable route. A body with no payload (a 204, a zero-length body) is
/// a <see cref="Errors.DeserializationException"/> for every one of them: the nullable readers admit a wire
/// <c>null</c>, not an absent payload.
/// </para>
/// <para>
/// <b>Breaking:</b> <c>ReadValueAsync</c> returned <c>ValueTask&lt;T?&gt;</c> and returned <see langword="null"/> for a wire
/// <c>null</c>; it disposed only the stream it opened, not the body; and an empty body surfaced as the codec's own
/// "no JSON tokens" failure with a generic message. Use <see cref="ReadValueOrDefaultAsync{T}"/> to accept <c>null</c>.
/// </para>
/// </remarks>
public static class ResponseBodySerdeExtensions
{
    /// <summary>Reads and deserializes the body as <typeparamref name="T"/> using <paramref name="serde"/>.</summary>
    /// <typeparam name="T">The target type. Not constrained to non-nullable: <c>int?</c> is a legitimate target.</typeparam>
    /// <param name="body">The response body (read once, and disposed).</param>
    /// <param name="serde">The serializer.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The deserialized value; never <see langword="null"/> for a reference type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> or <paramref name="serde"/> is <see langword="null"/>; the body is then not disposed.</exception>
    /// <exception cref="Errors.StreamConsumedException">The body has already been read.</exception>
    /// <exception cref="Errors.DeserializationException">
    /// The body has no payload, or is the JSON literal <c>null</c> for a non-nullable reference type, or the codec failed.
    /// </exception>
    /// <exception cref="IOException">The body stream failed; it propagates unwrapped (SERDE-12).</exception>
    public static async ValueTask<T> ReadValueAsync<T>(
        this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default)
    {
        var value = await ReadCoreAsync<T>(body, serde, admitNull: false, cancellationToken).ConfigureAwait(false);
        return value!;
    }

    /// <summary>
    /// Reads and deserializes the body as <typeparamref name="T"/>, admitting a wire <c>null</c> (the explicit nullable route).
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="body">The response body (read once, and disposed).</param>
    /// <param name="serde">The serializer.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The deserialized value, or <see langword="null"/> when the body is the JSON literal <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> or <paramref name="serde"/> is <see langword="null"/>; the body is then not disposed.</exception>
    /// <exception cref="Errors.StreamConsumedException">The body has already been read.</exception>
    /// <exception cref="Errors.DeserializationException">The body has no payload, or the codec failed.</exception>
    /// <exception cref="IOException">The body stream failed; it propagates unwrapped (SERDE-12).</exception>
    public static ValueTask<T?> ReadValueOrDefaultAsync<T>(
        this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default) =>
        ReadCoreAsync<T>(body, serde, admitNull: true, cancellationToken);

    /// <summary>Reads and deserializes the body as <typeparamref name="T"/>, synchronously.</summary>
    /// <typeparam name="T">The target type. Not constrained to non-nullable: <c>int?</c> is a legitimate target.</typeparam>
    /// <param name="body">The response body (read once, and disposed).</param>
    /// <param name="serde">The serializer.</param>
    /// <param name="cancellationToken">A token passed to <see cref="ResponseBody.OpenRead"/>; the synchronous stream decode takes none.</param>
    /// <returns>The deserialized value; never <see langword="null"/> for a reference type.</returns>
    /// <remarks>
    /// Over a transport's response body that does not override <see cref="ResponseBody.OpenRead"/> this throws
    /// <see cref="NotSupportedException"/>, as every synchronous body reader does; <c>SystemNetHttpClient</c>'s body implements
    /// the synchronous open (phase 7b).
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> or <paramref name="serde"/> is <see langword="null"/>; the body is then not disposed.</exception>
    /// <exception cref="Errors.StreamConsumedException">The body has already been read.</exception>
    /// <exception cref="Errors.DeserializationException">
    /// The body has no payload, or is the JSON literal <c>null</c> for a non-nullable reference type, or the codec failed.
    /// </exception>
    /// <exception cref="IOException">The body stream failed; it propagates unwrapped (SERDE-12).</exception>
    public static T ReadValue<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default)
    {
        var value = ReadCore<T>(body, serde, admitNull: false, cancellationToken);
        return value!;
    }

    /// <summary>
    /// Reads and deserializes the body as <typeparamref name="T"/> synchronously, admitting a wire <c>null</c> (the explicit
    /// nullable route).
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="body">The response body (read once, and disposed).</param>
    /// <param name="serde">The serializer.</param>
    /// <param name="cancellationToken">A token passed to <see cref="ResponseBody.OpenRead"/>; the synchronous stream decode takes none.</param>
    /// <returns>The deserialized value, or <see langword="null"/> when the body is the JSON literal <c>null</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="body"/> or <paramref name="serde"/> is <see langword="null"/>; the body is then not disposed.</exception>
    /// <exception cref="Errors.StreamConsumedException">The body has already been read.</exception>
    /// <exception cref="Errors.DeserializationException">The body has no payload, or the codec failed.</exception>
    /// <exception cref="IOException">The body stream failed; it propagates unwrapped (SERDE-12).</exception>
    public static T? ReadValueOrDefault<T>(this ResponseBody body, ISerde serde, CancellationToken cancellationToken = default) =>
        ReadCore<T>(body, serde, admitNull: true, cancellationToken);

    // The arguments are validated before anything is disposed: an argument error must not release a body the caller still owns.
    private static async ValueTask<T?> ReadCoreAsync<T>(
        ResponseBody body, ISerde serde, bool admitNull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(serde);

        Exception? primary = null;
        try
        {
            if (body.ContentLength == 0)
            {
                throw SerdeValues.NoBody<T>();
            }

            var stream = await body.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            var peeked = await SerdeValues.PeekFirstByteAsync(stream, cancellationToken).ConfigureAwait(false)
                ?? throw SerdeValues.NoBody<T>();
            await using var scope = peeked.ConfigureAwait(false);
            var value = await serde.DeserializeAsync<T>(peeked, cancellationToken).ConfigureAwait(false);
            return admitNull ? value : SerdeValues.RequireNonNull(value);
        }
        catch (Exception ex)
        {
            primary = ex;
            throw;
        }
        finally
        {
            await Disposal.DisposeQuietlyAsync(body, primary).ConfigureAwait(false);
        }
    }

    private static T? ReadCore<T>(ResponseBody body, ISerde serde, bool admitNull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(serde);

        Exception? primary = null;
        try
        {
            if (body.ContentLength == 0)
            {
                throw SerdeValues.NoBody<T>();
            }

            var stream = body.OpenRead(cancellationToken);
            using var peeked = SerdeValues.PeekFirstByte(stream) ?? throw SerdeValues.NoBody<T>();
            var value = serde.Deserialize<T>(peeked);
            return admitNull ? value : SerdeValues.RequireNonNull(value);
        }
        catch (Exception ex)
        {
            primary = ex;
            throw;
        }
        finally
        {
            Disposal.DisposeQuietly(body, primary);
        }
    }
}
