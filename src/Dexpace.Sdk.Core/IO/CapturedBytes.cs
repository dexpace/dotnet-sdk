// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// An immutable window over a core-allocated, never-pooled byte array, read through non-consuming views (IO-8,
/// IO-19 to IO-24, IO-37, IO-38).
/// </summary>
/// <remarks>
/// <para>
/// Captured body bytes are never pooled (P3a-8): the array is allocated by core and owned for good, so a capture has no
/// close, closing a view invalidates nothing, and a stale read of recycled memory cannot happen. The
/// <c>RS0030</c> ban on <c>ArrayPool&lt;T&gt;.Rent</c> keeps it that way.
/// </para>
/// <para>
/// Single-threaded contract (IO-37): a view is a <see cref="MemoryStream"/> and follows <see cref="Stream"/>'s contract,
/// one consumer at a time. The one concurrency guarantee that exists is that independent views (and slices) have their
/// own cursors and may be read from different threads.
/// </para>
/// </remarks>
internal sealed class CapturedBytes
{
    private readonly byte[] _array;
    private readonly int _offset;
    private readonly int _length;

    private CapturedBytes(byte[] array, int offset, int length)
    {
        _array = array;
        _offset = offset;
        _length = length;
    }

    /// <summary>The empty capture.</summary>
    internal static CapturedBytes Empty { get; } = new([], 0, 0);

    /// <summary>The number of bytes in this capture or slice.</summary>
    internal int Length => _length;

    /// <summary>The captured bytes. The caller must not retain a reference to the underlying array.</summary>
    internal ReadOnlySpan<byte> Span => new(_array, _offset, _length);

    /// <summary>Takes ownership of a core-allocated, never-pooled array without copying it.</summary>
    /// <param name="bytes">The array; the caller must not mutate or retain it afterwards.</param>
    /// <returns>The capture.</returns>
    internal static CapturedBytes Own(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new CapturedBytes(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// A window of at most <paramref name="count"/> bytes from <paramref name="offset"/> (IO-20 to IO-23). An offset beyond
    /// the end constructs and reads as empty (IO-21); a slice of a slice composes additively and is capped by the outer
    /// window. The parent is unchanged.
    /// </summary>
    /// <param name="offset">The start of the window; non-negative.</param>
    /// <param name="count">The most bytes in the window; non-negative.</param>
    /// <returns>The slice.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A negative offset or count (IO-3, IO-21).</exception>
    internal CapturedBytes Slice(long offset, long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        // new MemoryStream(array, index, count) rejects index > array.Length (design fact 3), so the clamp is the
        // constructor's precondition and what makes an overflowing offset an empty window.
        var start = (int)Math.Min(offset, _length);
        var take = (int)Math.Min(count, _length - start);
        return new CapturedBytes(_array, _offset + start, take);
    }

    /// <summary>A fresh read-only, non-publicly-visible view over this capture, with its own cursor (IO-19).</summary>
    /// <returns>The view; disposing it affects nothing else (IO-22).</returns>
    internal Stream OpenRead() => new MemoryStream(_array, _offset, _length, writable: false, publiclyVisible: false);

    /// <summary>A fresh copy of the bytes (IO-8).</summary>
    /// <returns>A new array.</returns>
    internal byte[] ToArray() => Span.ToArray();
}
