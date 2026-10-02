// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.TestSupport.Serialization;

/// <summary>
/// An in-memory <see cref="ISerde"/> that declares the media type it is given and writes a fixed payload from both
/// encode primitives, or throws a given exception instead. The decode primitives return <see langword="default"/>.
/// Unlike <see cref="ScriptedSerde{TScripted}"/>, which writes nothing and declares a fixed <c>application/json</c>,
/// it can drive a media-type pin or a serialization profile test.
/// </summary>
/// <param name="mediaType">The media type the codec declares.</param>
/// <param name="payload">The bytes both encode primitives write.</param>
/// <param name="failure">When given, thrown by both encode primitives instead of writing.</param>
public sealed class BytesSerde(MediaType mediaType, byte[] payload, Exception? failure = null) : ISerde
{
    /// <inheritdoc />
    public MediaType DefaultMediaType => mediaType;

    /// <inheritdoc />
    public async ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (failure is not null)
        {
            throw failure;
        }

        await destination.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<T?>(default);

    /// <inheritdoc />
    public void Serialize<T>(IBufferWriter<byte> destination, T value)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (failure is not null)
        {
            throw failure;
        }

        destination.Write(payload);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => default;
}
