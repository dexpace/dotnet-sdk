// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Internals;

[Trait("Category", "Unit")]
public sealed class CountingStreamTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task It_counts_bytes_and_reads_across_every_read_overload()
    {
        using var stream = CountingStream.OfBytes(new byte[10]);
        var buffer = new byte[4];

        Assert.Equal(4, stream.Read(buffer, 0, 4));
        Assert.Equal(3, stream.Read(buffer.AsSpan(0, 3)));
        Assert.Equal(2, await stream.ReadAsync(buffer.AsMemory(0, 2), Ct));
#pragma warning disable CA1835 // The byte[] overload is exercised on purpose: a body can be read through either.
        Assert.Equal(1, await stream.ReadAsync(buffer, 0, 1, Ct));
#pragma warning restore CA1835
        Assert.Equal(0, await stream.ReadAsync(buffer.AsMemory(), Ct));

        Assert.Equal(10, stream.BytesRead);
        Assert.Equal(5, stream.ReadCalls);
    }

    [Fact]
    public void It_is_read_only_and_not_seekable_so_a_body_over_it_is_single_use()
    {
        using var stream = CountingStream.OfBytes([1, 2, 3]);

        Assert.True(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position = 0);
        Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
    }

    [Fact]
    public void It_counts_disposal_and_disposes_what_it_wraps()
    {
        var inner = new MemoryStream([1]);
        var stream = new CountingStream(inner);

        Assert.False(stream.IsDisposed);
        stream.Dispose();
        stream.Dispose();

        Assert.True(stream.IsDisposed);
        Assert.Equal(2, stream.DisposeCount);
        Assert.False(inner.CanRead);
    }

    [Fact]
    public async Task A_parked_read_waits_until_released_and_then_ends_the_stream()
    {
        using var stream = new ParkedReadStream();
        var read = stream.ReadAsync(new byte[1], Ct).AsTask();

        await stream.ReadStarted.WaitAsync(Waits.Bound, Ct);
        Assert.True(await Waits.StaysPendingAsync(read, Ct));
        stream.Release();

        Assert.Equal(0, await read.WaitAsync(Waits.Bound, Ct));
        Assert.False(stream.ObservedCancellation);
    }

    [Fact]
    public async Task A_parked_read_observes_cancellation()
    {
        using var stream = new ParkedReadStream();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var read = stream.ReadAsync(new byte[1], cts.Token).AsTask();
        await stream.ReadStarted.WaitAsync(Waits.Bound, Ct);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.WaitAsync(Waits.Bound, Ct));
        Assert.True(stream.ObservedCancellation);
    }

    [Fact]
    public async Task A_parked_read_fails_when_the_stream_is_disposed_under_it()
    {
        var stream = new ParkedReadStream();
        var read = stream.ReadAsync(new byte[1], Ct).AsTask();
        await stream.ReadStarted.WaitAsync(Waits.Bound, Ct);

        stream.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => read.WaitAsync(Waits.Bound, Ct));
        Assert.True(stream.IsDisposed);
        Assert.False(stream.ObservedCancellation);
    }

    [Fact]
    public void A_parked_stream_refuses_synchronous_reads()
    {
        using var stream = new ParkedReadStream();

        Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
        Assert.False(stream.CanSeek);
    }
}
