// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.TestSupport.Serialization;

/// <summary>
/// A tiny in-memory codec for the reader and handler tests of <c>Dexpace.Sdk.Core.Tests</c>, which may not use a real
/// codec (SEAM-2). It decodes a fixed byte grammar, over all three decode primitives:
/// <list type="bullet">
/// <item><c>null</c> decodes to <see langword="null"/>.</item>
/// <item><c>ok:&lt;text&gt;</c> decodes to the text (for <c>T = string</c>).</item>
/// <item><c>bad</c> throws <see cref="DeserializationException"/> with an inner <see cref="FormatException"/>.</item>
/// <item><c>io...</c> throws <see cref="IOException"/> right after reading the first byte, as a dropped connection would.</item>
/// <item><c>first...</c> returns <c>"first"</c> after reading <b>one</b> byte, never reaching the end of the stream.</item>
/// <item>anything else throws <see cref="DeserializationException"/> with no inner exception.</item>
/// </list>
/// The stream members never dispose the stream they are given (SERDE-3), record what they were given, and implement the
/// synchronous stream decode explicitly so the sync reader tests do not depend on the seam's default member.
/// </summary>
public sealed class Utf8LiteralSerde : ISerde
{
    private int _streamReads;

    /// <summary>How many stream decodes (asynchronous and synchronous) ran.</summary>
    public int StreamReads => Volatile.Read(ref _streamReads);

    /// <summary>The stream the last stream decode was handed.</summary>
    public Stream? LastStream { get; private set; }

    /// <summary>The cancellation token the last asynchronous stream decode was handed.</summary>
    public CancellationToken LastToken { get; private set; }

    /// <summary>All the bytes the last decode read (not the whole payload for <c>first</c> or <c>io</c>).</summary>
    public byte[]? LastBytes { get; private set; }

    /// <inheritdoc />
    public MediaType DefaultMediaType => MediaType.Of("application", "json");

    /// <inheritdoc />
    public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    /// <inheritdoc />
    public async ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        Interlocked.Increment(ref _streamReads);
        LastStream = source;
        LastToken = cancellationToken;

        var first = new byte[1];
        if (await source.ReadAsync(first.AsMemory(), cancellationToken).ConfigureAwait(false) == 0)
        {
            return Decode<T>([]);
        }

        if (first[0] == (byte)'i')
        {
            throw new IOException("The connection was reset mid-stream.");
        }

        if (first[0] == (byte)'f')
        {
            LastBytes = first;
            return (T)(object)"first";
        }

        using var rest = new MemoryStream();
        await rest.WriteAsync(first, cancellationToken).ConfigureAwait(false);
        await source.CopyToAsync(rest, cancellationToken).ConfigureAwait(false);
        return Decode<T>(rest.ToArray());
    }

    /// <summary>The synchronous stream decode, implemented explicitly (SERDE-3, SERDE-12).</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="source">The stream to read; left open.</param>
    /// <returns>The decoded value.</returns>
    public T? Deserialize<T>(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Interlocked.Increment(ref _streamReads);
        LastStream = source;

        var first = new byte[1];
        if (source.Read(first, 0, 1) == 0)
        {
            return Decode<T>([]);
        }

        if (first[0] == (byte)'i')
        {
            throw new IOException("The connection was reset mid-stream.");
        }

        if (first[0] == (byte)'f')
        {
            LastBytes = first;
            return (T)(object)"first";
        }

        using var rest = new MemoryStream();
        rest.Write(first, 0, 1);
        source.CopyTo(rest);
        return Decode<T>(rest.ToArray());
    }

    /// <inheritdoc />
    public void Serialize<T>(IBufferWriter<byte> destination, T value)
    {
    }

    /// <inheritdoc />
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => Decode<T>(utf8.ToArray());

    private T? Decode<T>(byte[] bytes)
    {
        LastBytes = bytes;
        var text = Encoding.UTF8.GetString(bytes);
        if (text == "null")
        {
            return default;
        }

        if (text.StartsWith("ok:", StringComparison.Ordinal))
        {
            return typeof(T) == typeof(string)
                ? (T)(object)text[3..]
                : throw new InvalidOperationException($"Utf8LiteralSerde decodes 'ok:' payloads only for string, not {typeof(T).Name}.");
        }

        if (text == "bad")
        {
            throw new DeserializationException("The literal 'bad' is not decodable.", new FormatException("bad"));
        }

        throw new DeserializationException($"Utf8LiteralSerde cannot decode '{text}'.");
    }
}
