// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.TestSupport.Serialization;

/// <summary>
/// An <see cref="ISerde"/> that deliberately does <b>not</b> implement the synchronous stream decode
/// (<c>Deserialize&lt;T&gt;(Stream)</c>), so a test exercises the seam's default interface member (P7a-13). Its UTF-8 span
/// decode records the bytes it received and returns a scripted value.
/// </summary>
/// <remarks>The decode result is <see langword="default"/> unless <see cref="Result"/> is set. Encoding writes nothing.</remarks>
public sealed class DefaultImplementationSerde : ISerde
{
    /// <summary>The bytes the span decode last received, or <see langword="null"/> before any decode.</summary>
    public byte[]? ReceivedBytes { get; private set; }

    /// <summary>The value every decode returns, boxed; <see langword="null"/> for <see langword="default"/>.</summary>
    public object? Result { get; set; }

    /// <inheritdoc />
    public MediaType DefaultMediaType => MediaType.Of("application", "json");

    /// <inheritdoc />
    public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        ReceivedBytes = buffer.ToArray();
        return Result is T typed ? typed : default;
    }

    /// <inheritdoc />
    public void Serialize<T>(IBufferWriter<byte> destination, T value)
    {
    }

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8)
    {
        ReceivedBytes = utf8.ToArray();
        return Result is T typed ? typed : default;
    }
}
