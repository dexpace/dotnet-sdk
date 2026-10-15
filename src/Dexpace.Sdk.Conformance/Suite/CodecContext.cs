// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// What one run of one codec assertion owns: the subject, the face, and the codec, built fresh the way a consumer builds it (lazily, so a
/// factory that throws is an errored result of the assertion and not an exception out of the run). The two faces are the two call
/// surfaces of <see cref="ISerde"/>: the asynchronous stream pair, and the synchronous buffer and span pair.
/// </summary>
internal sealed class CodecContext(CodecSubject subject, TransportFace face, SuiteOptions options) : IAsyncDisposable
{
    private ISerde? _serde;

    /// <summary>The codec under test.</summary>
    internal CodecSubject Subject { get; } = subject;

    /// <summary>The face this run drives.</summary>
    internal TransportFace Face { get; } = face;

    /// <summary>The run's options.</summary>
    internal SuiteOptions Options { get; } = options;

    /// <summary>The codec, built on first use by the subject's factory.</summary>
    internal ISerde Serde => _serde ??= Subject.CreateSerde() ?? throw new ConformanceException("CreateSerde returned null: the codec suite needs a codec to judge.");

    /// <summary>Encodes <paramref name="value"/> on this run's face and returns the bytes written.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="token">The assertion's token.</param>
    internal async Task<byte[]> EncodeAsync<T>(T value, CancellationToken token)
    {
        if (Face == TransportFace.Async)
        {
            using var sink = new MemoryStream();
            await Serde.SerializeAsync(sink, value, token).ConfigureAwait(false);
            return sink.ToArray();
        }

        var buffer = new ArrayBufferWriter<byte>();
        Serde.Serialize(buffer, value);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Decodes <paramref name="bytes"/> on this run's face.</summary>
    /// <typeparam name="T">The type to decode to.</typeparam>
    /// <param name="bytes">The encoded value.</param>
    /// <param name="token">The assertion's token.</param>
    internal async Task<T?> DecodeAsync<T>(byte[] bytes, CancellationToken token)
    {
        if (Face == TransportFace.Async)
        {
            using var source = new MemoryStream(bytes, writable: false);
            return await Serde.DeserializeAsync<T>(source, token).ConfigureAwait(false);
        }

        return Serde.Deserialize<T>(bytes);
    }

    /// <summary>Encodes and decodes <paramref name="value"/> on this run's face, turning any codec failure into a named conformance failure.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="what">What is being round-tripped, named in a failure.</param>
    /// <param name="token">The assertion's token.</param>
    internal async Task<T?> RoundTripAsync<T>(T value, string what, CancellationToken token)
    {
        var bytes = await Check.GuardAsync(() => EncodeAsync(value, token), $"encoding {what}", token).ConfigureAwait(false);
        return await Check.GuardAsync(() => DecodeAsync<T>(bytes, token), $"decoding {what}", token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        switch (_serde)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
