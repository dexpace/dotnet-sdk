// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// Serializes values into, and deserializes them out of, request/response payloads. The seam is
/// generic and serializer-agnostic; implementations are responsible for resolving type metadata
/// (a System.Text.Json implementation ships separately).
/// </summary>
/// <remarks>
/// <para>
/// <b>Media type (SEAM-19).</b> Every codec declares its own <see cref="DefaultMediaType"/>; the property has no
/// default implementation, and body creation stamps it when the caller gives none.
/// </para>
/// <para>
/// <b>Type capture (SEAM-22).</b> The target type is the generic argument itself, which the runtime binds to a closed
/// type, so a codec never decodes into an open generic. The seam takes no <see cref="Type"/>.
/// </para>
/// <para>
/// <b>Primitives and profiles (SEAM-20).</b> The seam has two encode primitives (a stream and an
/// <see cref="IBufferWriter{T}"/>) and two decode primitives (a stream and a UTF-8 span). The fresh-bytes, string and
/// fixed-buffer profiles are written once, over any codec, in <see cref="SerdeExtensions"/>. The buffer primitives are
/// UTF-8; a codec whose wire format is not UTF-8 text implements <see cref="IStringSerde"/> as the optional string
/// override.
/// </para>
/// <para>
/// <b>Failures (SEAM-20, SEAM-21, SEAM-23).</b> A codec throws <see cref="Errors.SerializationException"/> for encode
/// failures and <see cref="Errors.DeserializationException"/> for decode failures, chaining the cause; both derive from
/// <see cref="Errors.SerdeException"/>. A failure of the underlying stream (<see cref="IOException"/>) propagates
/// unwrapped, and the codec leaves a caller-supplied stream open.
/// </para>
/// </remarks>
public interface ISerde
{
    /// <summary>The media type stamped on bodies created from values (for example, application/json).</summary>
    MediaType DefaultMediaType { get; }

    /// <summary>Serializes <paramref name="value"/> to <paramref name="destination"/>.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when serialization finishes.</returns>
    /// <exception cref="Errors.SerializationException">
    /// Serialization failed. Derives from <see cref="Errors.SerdeException"/>, as does
    /// <see cref="Errors.DeserializationException"/>.
    /// </exception>
    ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default);

    /// <summary>Deserializes a value of type <typeparamref name="T"/> from <paramref name="source"/>.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="source">The stream to read from.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The deserialized value, or <see langword="null"/>.</returns>
    /// <exception cref="Errors.DeserializationException">Deserialization failed.</exception>
    ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default);

    /// <summary>Serializes <paramref name="value"/> synchronously to <paramref name="destination"/>.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="destination">The buffer writer to write to.</param>
    /// <param name="value">The value to serialize.</param>
    /// <exception cref="Errors.SerializationException">Serialization failed.</exception>
    void Serialize<T>(IBufferWriter<byte> destination, T value);

    /// <summary>Deserializes a value of type <typeparamref name="T"/> from a UTF-8 buffer.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="utf8">The UTF-8 encoded payload.</param>
    /// <returns>The deserialized value, or <see langword="null"/>.</returns>
    /// <exception cref="Errors.DeserializationException">Deserialization failed.</exception>
    T? Deserialize<T>(ReadOnlySpan<byte> utf8);
}
