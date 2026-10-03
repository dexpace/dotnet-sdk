// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.IO;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>
/// The non-consuming captured-byte views: IO-8, IO-19 to IO-24, IO-37 (ported from <c>nodejs-sdk@c0ff3fd</c>
/// <c>packages/core/src/io/buffered-source.views.test.ts</c>, peek and slice cases).
/// </summary>
[Trait("Category", "Unit")]
public class CapturedBytesTests
{
    private static byte[] Bytes(int length) => Enumerable.Range(0, length).Select(i => (byte)(i + 1)).ToArray();

    private static CapturedBytes Capture(int length) => CapturedBytes.Own(Bytes(length));

    [Fact]
    public void ToArray_returns_a_fresh_copy_each_call()
    {
        var capture = Capture(5);

        var first = capture.ToArray();
        first[0] = 99;
        var second = capture.ToArray();

        Assert.NotSame(first, second);
        Assert.Equal(Bytes(5), second);
        Assert.Equal(Bytes(5), capture.ToArray());
    }

    [Fact]
    public void Reading_one_view_moves_no_other_view()
    {
        var capture = Capture(6);
        using var a = capture.OpenRead();
        using var b = capture.OpenRead();

        var fromA = new byte[3];
        a.ReadExactly(fromA);
        var fromB = new byte[6];
        b.ReadExactly(fromB);
        var restOfA = new byte[3];
        a.ReadExactly(restOfA);

        Assert.Equal(Bytes(6).AsSpan(0, 3).ToArray(), fromA);
        Assert.Equal(Bytes(6), fromB);
        Assert.Equal(Bytes(6).AsSpan(3, 3).ToArray(), restOfA);
    }

    [Fact]
    public void A_view_is_read_only_and_not_publicly_visible()
    {
        using var view = Capture(4).OpenRead();

        Assert.False(view.CanWrite);
        Assert.True(view.CanRead);
        var memory = Assert.IsType<MemoryStream>(view);
        Assert.False(memory.TryGetBuffer(out _));
    }

    [Fact]
    public void A_slice_exposes_its_window_and_then_ends()
    {
        using var view = Capture(10).Slice(2, 4).OpenRead();
        var buffer = new byte[10];

        var read = view.ReadAtLeast(buffer, 4, throwOnEndOfStream: false);

        Assert.Equal(4, read);
        Assert.Equal(Bytes(10).AsSpan(2, 4).ToArray(), buffer.AsSpan(0, 4).ToArray());
        Assert.Equal(0, view.Read(buffer, 0, 10));
    }

    [Fact]
    public void Slicing_leaves_the_parent_unchanged()
    {
        var capture = Capture(10);

        _ = capture.Slice(3, 2);

        Assert.Equal(10, capture.Length);
        Assert.Equal(Bytes(10), capture.ToArray());
    }

    [Theory]
    [InlineData(10L)]
    [InlineData(11L)]
    [InlineData(1_000_000_000_000L)]
    public void An_overflowing_slice_constructs_and_reads_empty(long offset)
    {
        var slice = Capture(10).Slice(offset, 5);

        Assert.Equal(0, slice.Length);
        using var view = slice.OpenRead();
        Assert.Equal(0, view.Read(new byte[4], 0, 4));
        Assert.Throws<EndOfStreamException>(() => view.ReadExactly(new byte[1]));
    }

    [Fact]
    public void A_negative_offset_or_count_is_rejected_eagerly()
    {
        var capture = Capture(10);

        Assert.Throws<ArgumentOutOfRangeException>(() => capture.Slice(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => capture.Slice(0, -1));
    }

    [Theory]
    [InlineData(2L, 6L, 1L, 3L, 4, 3)]
    [InlineData(2L, 6L, 4L, 100L, 7, 2)]
    [InlineData(2L, 6L, 6L, 3L, 8, 0)]
    [InlineData(2L, 6L, 9L, 3L, 8, 0)]
    [InlineData(0L, 10L, 0L, 10L, 1, 10)]
    public void A_slice_of_a_slice_composes_additively_and_is_capped_by_the_outer_window(
        long outerOffset,
        long outerCount,
        long innerOffset,
        long innerCount,
        int firstExpectedByte,
        int expectedLength)
    {
        var slice = Capture(10).Slice(outerOffset, outerCount).Slice(innerOffset, innerCount);

        Assert.Equal(expectedLength, slice.Length);
        if (expectedLength > 0)
        {
            Assert.Equal((byte)firstExpectedByte, slice.ToArray()[0]);
            Assert.Equal((byte)(firstExpectedByte + expectedLength - 1), slice.ToArray()[expectedLength - 1]);
        }
    }

    [Fact]
    public void Slices_and_views_have_independent_cursors()
    {
        var capture = Capture(8);
        var slice = capture.Slice(0, 8);
        using var fromCapture = capture.OpenRead();
        using var fromSlice = slice.OpenRead();

        Assert.Equal(1, fromCapture.ReadByte());
        Assert.Equal(2, fromCapture.ReadByte());
        Assert.Equal(1, fromSlice.ReadByte());
    }

    [Fact]
    public void Disposing_a_view_leaves_the_capture_and_other_views_readable()
    {
        var capture = Capture(4);
        var first = capture.OpenRead();
        using var second = capture.OpenRead();

        first.Dispose();

        Assert.Equal(1, second.ReadByte());
        using var third = capture.OpenRead();
        Assert.Equal(1, third.ReadByte());
        Assert.Equal(Bytes(4), capture.ToArray());
    }

    [Fact]
    public async Task Reading_a_disposed_view_throws_in_every_read_form()
    {
        var view = Capture(4).OpenRead();
        view.Dispose();
        var buffer = new byte[2];

        Assert.Throws<ObjectDisposedException>(() => view.Read(buffer, 0, 2));
        Assert.Throws<ObjectDisposedException>(() => view.ReadByte());
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => view.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken).AsTask());
        Assert.Throws<ObjectDisposedException>(() => view.CopyTo(Stream.Null));
    }

    [Fact]
    public async Task Independent_views_can_be_read_on_different_threads()
    {
        var capture = Capture(100_000);
        var results = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => Task.Run(
                () =>
                {
                    using var view = capture.OpenRead();
                    using var sink = new MemoryStream();
                    view.CopyTo(sink);
                    return sink.ToArray();
                },
                TestContext.Current.CancellationToken)));

        Assert.All(results, r => Assert.Equal(capture.ToArray(), r));
    }

    [Fact]
    public void Empty_has_length_zero_and_opens_an_empty_view()
    {
        Assert.Equal(0, CapturedBytes.Empty.Length);
        using var view = CapturedBytes.Empty.OpenRead();
        Assert.Equal(0, view.Read(new byte[1], 0, 1));
        Assert.Empty(CapturedBytes.Empty.ToArray());
    }

    [Fact]
    public void Own_takes_the_array_without_copying_and_Span_exposes_it()
    {
        var array = Bytes(5);
        var capture = CapturedBytes.Own(array);

        Assert.True(capture.Span.SequenceEqual(array));
        // Documented: the caller must not retain the array, because the capture reads it in place.
        array[0] = 42;
        Assert.Equal(42, capture.Span[0]);
    }
}
