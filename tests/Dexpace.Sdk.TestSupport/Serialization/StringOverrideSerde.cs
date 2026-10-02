// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.TestSupport.Serialization;

/// <summary>
/// A <see cref="BytesSerde"/>-backed codec that also implements <see cref="IStringSerde"/>, returning a fixed string from
/// <see cref="SerializeToString{T}(T)"/> whatever its payload bytes are.
/// </summary>
/// <param name="mediaType">The media type the codec declares.</param>
/// <param name="payload">The bytes both encode primitives write.</param>
/// <param name="text">The string <see cref="SerializeToString{T}(T)"/> returns.</param>
public sealed class StringOverrideSerde(MediaType mediaType, byte[] payload, string text) : IStringSerde
{
    private readonly BytesSerde _inner = new(mediaType, payload);

    /// <inheritdoc />
    public MediaType DefaultMediaType => _inner.DefaultMediaType;

    /// <inheritdoc />
    public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) =>
        _inner.SerializeAsync(destination, value, cancellationToken);

    /// <inheritdoc />
    public ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default) =>
        _inner.DeserializeAsync<T>(source, cancellationToken);

    /// <inheritdoc />
    public void Serialize<T>(IBufferWriter<byte> destination, T value) => _inner.Serialize(destination, value);

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => _inner.Deserialize<T>(utf8);

    /// <inheritdoc />
    public string SerializeToString<T>(T value) => text;
}
