// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.TestSupport.Serialization;

/// <summary>
/// An in-memory <see cref="ISerde"/> whose asynchronous decode drains the source stream and returns the next
/// scripted value, whatever the bytes were. Asking for a type other than <typeparamref name="TScripted"/>, or for more
/// values than were scripted, throws <see cref="InvalidOperationException"/>. Encoding writes nothing, and the
/// span-based decode returns <see langword="default"/>.
/// </summary>
/// <typeparam name="TScripted">The type every decode is expected to ask for.</typeparam>
/// <param name="values">The values returned by successive decodes.</param>
public sealed class ScriptedSerde<TScripted>(params TScripted[] values) : ISerde
{
    private int _index;

    /// <inheritdoc />
    public MediaType DefaultMediaType => MediaType.Of("application", "json");

    /// <inheritdoc />
    public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Drain the body, as a real codec would, so the response stream is fully consumed.
        await source.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);

        if (typeof(T) != typeof(TScripted))
        {
            throw new InvalidOperationException(
                $"ScriptedSerde<{typeof(TScripted).Name}> asked for {typeof(T).Name}.");
        }

        if (_index >= values.Length)
        {
            throw new InvalidOperationException("ScriptedSerde exhausted.");
        }

        return (T)(object)values[_index++]!;
    }

    /// <inheritdoc />
    public void Serialize<T>(IBufferWriter<byte> destination, T value)
    {
    }

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => default;
}
