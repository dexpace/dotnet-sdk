// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// An optional capability of an <see cref="ISerde"/>: the codec supplies its own string form of a value.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SerdeExtensions.SerializeToString{T}(ISerde, T)"/> decodes the seam's UTF-8 buffer by default and
/// returns <see cref="SerializeToString{T}(T)"/> instead when the codec implements this interface (SEAM-20). A codec
/// implements it when its wire format is not UTF-8 text, so the buffer decode would be wrong.
/// </para>
/// <para>
/// A binary codec has no meaningful string form and should not implement it.
/// </para>
/// </remarks>
public interface IStringSerde : ISerde
{
    /// <summary>Serializes <paramref name="value"/> to the codec's own string form.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to serialize.</param>
    /// <returns>The string form of <paramref name="value"/>.</returns>
    /// <exception cref="Errors.SerializationException">Serialization failed.</exception>
    string SerializeToString<T>(T value);
}
