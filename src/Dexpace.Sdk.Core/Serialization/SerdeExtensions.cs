// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text;

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// The derived serialization profiles of SEAM-20, written once over any <see cref="ISerde"/>: fresh bytes, a fresh
/// string, and a caller-supplied fixed buffer.
/// </summary>
/// <remarks>
/// Every profile is built on the seam's <see cref="ISerde.Serialize{T}(IBufferWriter{byte}, T)"/> primitive. A failure
/// from the primitive surfaces unchanged (a <see cref="Errors.SerializationException"/> is not re-wrapped).
/// </remarks>
public static class SerdeExtensions
{
    /// <summary>Serializes <paramref name="value"/> to a freshly allocated UTF-8 byte array.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="serde">The codec.</param>
    /// <param name="value">The value to serialize.</param>
    /// <returns>A new array on every call.</returns>
    /// <exception cref="Errors.SerializationException">Serialization failed.</exception>
    public static byte[] SerializeToUtf8Bytes<T>(this ISerde serde, T value)
    {
        ArgumentNullException.ThrowIfNull(serde);
        var writer = new ArrayBufferWriter<byte>();
        serde.Serialize(writer, value);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Serializes <paramref name="value"/> to a string.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="serde">The codec.</param>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The string form of <paramref name="value"/>.</returns>
    /// <remarks>
    /// The seam's buffer is UTF-8, so the buffer is decoded as UTF-8, whatever media type the codec declares. A codec
    /// whose wire format is not UTF-8 text implements <see cref="IStringSerde"/> to supply its own string, which
    /// this method returns instead.
    /// </remarks>
    /// <exception cref="Errors.SerializationException">Serialization failed.</exception>
    public static string SerializeToString<T>(this ISerde serde, T value)
    {
        ArgumentNullException.ThrowIfNull(serde);
        if (serde is IStringSerde stringSerde)
        {
            return stringSerde.SerializeToString(value);
        }

        var writer = new ArrayBufferWriter<byte>();
        serde.Serialize(writer, value);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    /// <summary>Serializes <paramref name="value"/> into a caller-supplied fixed buffer.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="serde">The codec.</param>
    /// <param name="destination">
    /// The buffer to fill, starting at its first byte; pass <c>buffer.AsSpan(offset)</c> to write at an offset.
    /// </param>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The number of bytes written.</returns>
    /// <remarks>
    /// The value is serialized into scratch space first and copied only when it fits, so on overflow
    /// <paramref name="destination"/> is left untouched.
    /// </remarks>
    /// <exception cref="Errors.SerializationException">Serialization failed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The payload does not fit in <paramref name="destination"/>.</exception>
    public static int Serialize<T>(this ISerde serde, Span<byte> destination, T value)
    {
        ArgumentNullException.ThrowIfNull(serde);
        var writer = new ArrayBufferWriter<byte>();
        serde.Serialize(writer, value);

        var written = writer.WrittenSpan;
        if (written.Length > destination.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destination),
                $"The serialized payload needs {written.Length} bytes but the destination holds {destination.Length}.");
        }

        written.CopyTo(destination);
        return written.Length;
    }
}
