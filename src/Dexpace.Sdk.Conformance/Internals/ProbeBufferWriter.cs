// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A caller-owned buffer writer for the codec suite: it counts how often it was disposed (a codec must not take ownership of a target it
/// was handed, <c>SERDE-3</c>) and, when given a failure, throws it as soon as a codec asks for room (a genuine downstream write error that
/// must propagate unwrapped, <c>SERDE-12</c>).
/// </summary>
internal sealed class ProbeBufferWriter(IOException? failure = null) : IBufferWriter<byte>, IDisposable
{
    private readonly ArrayBufferWriter<byte> _inner = new();
    private int _disposals;

    /// <summary>How many times the writer was disposed.</summary>
    internal int Disposals => Volatile.Read(ref _disposals);

    /// <summary>The bytes written.</summary>
    internal ReadOnlySpan<byte> Written => _inner.WrittenSpan;

    /// <inheritdoc />
    public void Advance(int count) => _inner.Advance(count);

    /// <inheritdoc />
    public Memory<byte> GetMemory(int sizeHint = 0) => failure is null ? _inner.GetMemory(sizeHint) : throw failure;

    /// <inheritdoc />
    public Span<byte> GetSpan(int sizeHint = 0) => failure is null ? _inner.GetSpan(sizeHint) : throw failure;

    /// <inheritdoc />
    public void Dispose() => Interlocked.Increment(ref _disposals);
}
