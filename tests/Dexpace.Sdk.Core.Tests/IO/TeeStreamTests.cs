// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/io/tee-sink.test.ts: the mirror-before-forward, limit, lifecycle and
// argument-validation cases. The emit, text-write and toWritableStream cases are Okio-shaped and are not ported.

#pragma warning disable CA1835 // The byte[] WriteAsync overload is the subject here: design fact 1 needs both overloads exercised.

using System.Reflection;
using Dexpace.Sdk.Core.IO;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>The request-mirroring tee: IO-4 to IO-6, IO-8, IO-25 to IO-29, IO-40 to IO-42.</summary>
[Trait("Category", "Unit")]
public class TeeStreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly byte[] s_payload = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    [Fact]
    public async Task The_primary_receives_every_byte_written_in_order()
    {
        using var primary = new MemoryStream();
        using var tee = new TeeStream(primary, leaveOpen: true);

        tee.Write(s_payload, 0, 2);
        tee.Write(s_payload.AsSpan(2, 2));
        tee.WriteByte(5);
        await tee.WriteAsync(s_payload.AsMemory(5, 2), Token);
        await tee.WriteAsync(s_payload, 7, 3, Token);

        Assert.Equal(s_payload, primary.ToArray());
        Assert.Equal(s_payload, tee.SnapshotTap());
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(5L)]
    [InlineData(10L)]
    [InlineData(TeeStream.Unbounded)]
    public void The_primary_receives_the_full_payload_whatever_the_tap_limit(long limit)
    {
        using var primary = new MemoryStream();
        using var tee = new TeeStream(primary, limit, leaveOpen: true);

        tee.Write(s_payload, 0, 10);

        Assert.Equal(s_payload, primary.ToArray());
    }

    [Fact]
    public void The_tap_stops_at_its_limit_while_forwarding_continues()
    {
        using var primary = new MemoryStream();
        using var tee = new TeeStream(primary, 4, leaveOpen: true);

        tee.Write(s_payload, 0, 3);
        tee.Write(s_payload, 3, 3);
        tee.Write(s_payload, 6, 4);

        Assert.Equal(s_payload.AsSpan(0, 4).ToArray(), tee.SnapshotTap());
        Assert.Equal(4, tee.TapLength);
        Assert.Equal(s_payload, primary.ToArray());
    }

    [Fact]
    public void A_zero_limit_mirrors_nothing()
    {
        using var primary = new MemoryStream();
        using var tee = new TeeStream(primary, 0, leaveOpen: true);

        tee.Write(s_payload, 0, 10);

        Assert.Empty(tee.SnapshotTap());
        Assert.Equal(10, primary.Length);
    }

    [Fact]
    public void The_default_limit_mirrors_everything()
    {
        using var primary = new MemoryStream();
        using var tee = new TeeStream(primary, leaveOpen: true);

        tee.Write(new byte[200_000], 0, 200_000);

        Assert.Equal(200_000, tee.TapLength);
    }

    [Fact]
    public void A_negative_tap_limit_throws_ArgumentOutOfRangeException()
    {
        using var primary = new MemoryStream();

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new TeeStream(primary, -1));

        Assert.Equal("tapLimit", error.ParamName);
    }

    [Fact]
    public void A_failed_primary_write_still_leaves_the_attempted_bytes_in_the_tap()
    {
        using var primary = new FailingWriteStream(2, new IOException("boom"));
        using var tee = new TeeStream(primary, leaveOpen: true);

        tee.Write(s_payload, 0, 4);
        Assert.Throws<IOException>(() => tee.Write(s_payload, 4, 3));

        Assert.Equal(s_payload.AsSpan(0, 7).ToArray(), tee.SnapshotTap());
        Assert.Equal(s_payload.AsSpan(0, 4).ToArray(), primary.WrittenBeforeFailure);
    }

    [Fact]
    public void A_write_after_a_failed_one_adds_only_its_own_bytes()
    {
        using var primary = new FailingWriteStream(1, new IOException("boom"));
        using var tee = new TeeStream(primary, leaveOpen: true);

        Assert.Throws<IOException>(() => tee.Write(s_payload, 0, 3));
        tee.Write(s_payload, 3, 2);

        Assert.Equal(s_payload.AsSpan(0, 5).ToArray(), tee.SnapshotTap());
        Assert.Equal(s_payload.AsSpan(3, 2).ToArray(), primary.WrittenBeforeFailure);
    }

    [Fact]
    public void Exposes_no_handle_on_its_tap()
    {
        const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;
        var handleTypes = new[] { typeof(byte[]), typeof(Memory<byte>), typeof(ReadOnlyMemory<byte>), typeof(ArraySegment<byte>) };

        foreach (var member in typeof(TeeStream).GetMembers(All))
        {
            if (member.Name == nameof(TeeStream.SnapshotTap) || IsPrivate(member))
            {
                continue;
            }

            var returned = member switch
            {
                MethodInfo m => m.ReturnType,
                PropertyInfo p => p.PropertyType,
                FieldInfo f => f.FieldType,
                _ => typeof(void),
            };

            Assert.False(handleTypes.Contains(returned), $"{member.Name} returns {returned}.");
            Assert.False(typeof(Stream).IsAssignableFrom(returned), $"{member.Name} returns {returned}.");
        }

        Assert.False(typeof(MemoryStream).IsAssignableFrom(typeof(TeeStream)));

        using var tee = new TeeStream(new MemoryStream());
        tee.Write(s_payload, 0, 3);
        var first = tee.SnapshotTap();
        var second = tee.SnapshotTap();
        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    private static bool IsPrivate(MemberInfo member) => member switch
    {
        MethodBase m => m.IsPrivate,
        PropertyInfo p => (p.GetMethod?.IsPrivate ?? true) && (p.SetMethod?.IsPrivate ?? true),
        FieldInfo f => f.IsPrivate,
        _ => false,
    };

    [Fact]
    public void A_snapshot_is_unchanged_by_later_writes()
    {
        using var tee = new TeeStream(new MemoryStream());
        tee.Write(s_payload, 0, 3);
        var snapshot = tee.SnapshotTap();

        tee.Write(s_payload, 3, 3);

        Assert.Equal(s_payload.AsSpan(0, 3).ToArray(), snapshot);
        Assert.Equal(6, tee.SnapshotTap().Length);
    }

    [Fact]
    public async Task Both_async_write_overloads_stay_asynchronous()
    {
        using var primary = new RecordingWriteStream();
        using var tee = new TeeStream(primary, leaveOpen: true);

        await tee.WriteAsync(s_payload, 0, 4, Token);
        await tee.WriteAsync(s_payload.AsMemory(4, 4), Token);

        // A sync-only primary would have seen these on the sync Write; the recording stream overrides both async forms.
        Assert.Equal(0, primary.SyncWriteCalls);
        Assert.Equal(2, primary.AsyncWriteThreadIds.Count);
        Assert.Equal(s_payload.AsSpan(0, 8).ToArray(), primary.WrittenBytes);
    }

    [Fact]
    public async Task Flush_and_FlushAsync_reach_the_primary()
    {
        using var primary = new RecordingWriteStream();
        using var tee = new TeeStream(primary, leaveOpen: true);

        tee.Flush();
        await tee.FlushAsync(Token);

        Assert.Equal(2, primary.FlushCount);
    }

    [Fact]
    public void Flush_never_touches_the_tap()
    {
        using var tee = new TeeStream(new MemoryStream());
        tee.Write(s_payload, 0, 3);

        tee.Flush();

        Assert.Equal(s_payload.AsSpan(0, 3).ToArray(), tee.SnapshotTap());
    }

    [Fact]
    public void Dispose_reaches_the_primary_once_and_the_tap_survives()
    {
        using var primary = new DisposeCountingStream(new MemoryStream());
        var tee = new TeeStream(primary);
        tee.Write(s_payload, 0, 3);

        tee.Dispose();

        Assert.Equal(1, primary.DisposeCount);
        Assert.Equal(s_payload.AsSpan(0, 3).ToArray(), tee.SnapshotTap());
    }

    [Fact]
    public void With_leaveOpen_the_primary_survives_the_wrapper()
    {
        using var primary = new DisposeCountingStream(new MemoryStream());
        var tee = new TeeStream(primary, leaveOpen: true);

        tee.Dispose();

        Assert.Equal(0, primary.DisposeCount);
    }

    [Fact]
    public async Task Disposing_the_wrapper_disposes_the_stream_it_owns()
    {
        using var first = new DisposeCountingStream(new MemoryStream());
        var asyncTee = new TeeStream(first);
        await asyncTee.DisposeAsync();
        using var second = new DisposeCountingStream(new MemoryStream());
        var syncTee = new TeeStream(second);
        syncTee.Dispose();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Disposing_twice_in_any_mix_disposes_the_owned_stream_once(bool firstAsync, bool secondAsync)
    {
        using var primary = new DisposeCountingStream(new MemoryStream());
        await using var tee = new TeeStream(primary);

        foreach (var useAsync in new[] { firstAsync, secondAsync })
        {
            if (useAsync)
            {
                await tee.DisposeAsync();
            }
            else
            {
                tee.Dispose();
            }
        }

        Assert.Equal(1, primary.DisposeCount);
    }

    [Fact]
    public async Task Writing_after_dispose_throws_ObjectDisposedException()
    {
        var tee = new TeeStream(new MemoryStream());
        tee.Dispose();

        Assert.Throws<ObjectDisposedException>(() => tee.Write(s_payload, 0, 1));
        Assert.Throws<ObjectDisposedException>(() => tee.Write(s_payload.AsSpan(0, 1)));
        Assert.Throws<ObjectDisposedException>(() => tee.WriteByte(1));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => tee.WriteAsync(s_payload.AsMemory(0, 1), Token).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => tee.WriteAsync(s_payload, 0, 1, Token));
    }

    [Fact]
    public async Task Flush_after_dispose_throws_ObjectDisposedException()
    {
        var tee = new TeeStream(new MemoryStream());
        tee.Dispose();

        Assert.Throws<ObjectDisposedException>(() => tee.Flush());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => tee.FlushAsync(Token));
    }

    [Fact]
    public void The_tap_can_be_snapshotted_after_dispose()
    {
        var tee = new TeeStream(new MemoryStream());
        tee.Write(s_payload, 0, 4);
        tee.Dispose();

        Assert.Equal(s_payload.AsSpan(0, 4).ToArray(), tee.SnapshotTap());
        Assert.Equal(4, tee.TapLength);
    }

    [Fact]
    public async Task The_callers_token_reaches_the_primary_unchanged()
    {
        using var primary = new RecordingWriteStream();
        using var tee = new TeeStream(primary, leaveOpen: true);
        using var cts = new CancellationTokenSource();

        await tee.WriteAsync(s_payload.AsMemory(0, 2), cts.Token);
        await tee.WriteAsync(s_payload, 2, 2, cts.Token);
        await tee.FlushAsync(cts.Token);

        Assert.Equal(3, primary.Tokens.Count);
        Assert.All(primary.Tokens, t => Assert.Equal(cts.Token, t));
    }

    [Fact]
    public async Task An_OperationCanceledException_from_the_primary_propagates_as_the_same_instance()
    {
        var cancelled = new OperationCanceledException("cancelled by the primary");
        using var primary = new FailingWriteStream(1, cancelled);
        using var tee = new TeeStream(primary, leaveOpen: true);

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(
            () => tee.WriteAsync(s_payload.AsMemory(0, 2), Token).AsTask());

        Assert.Same(cancelled, thrown);
    }

    [Fact]
    public void Read_Seek_SetLength_Length_and_Position_throw_NotSupportedException()
    {
        using var tee = new TeeStream(new MemoryStream());

        Assert.Throws<NotSupportedException>(() => tee.Read(new byte[1], 0, 1));
        Assert.Throws<NotSupportedException>(() => tee.Seek(0, SeekOrigin.Begin));
        Assert.Throws<NotSupportedException>(() => tee.SetLength(0));
        Assert.Throws<NotSupportedException>(() => tee.Length);
        Assert.Throws<NotSupportedException>(() => tee.Position);
        Assert.Throws<NotSupportedException>(() => tee.Position = 0);
    }

    [Fact]
    public void CanRead_and_CanSeek_are_false()
    {
        using var tee = new TeeStream(new MemoryStream());

        Assert.False(tee.CanRead);
        Assert.False(tee.CanSeek);
        Assert.True(tee.CanWrite);
    }

    [Fact]
    public void A_non_writable_primary_is_rejected()
    {
        using var readOnly = new MemoryStream([1], writable: false);

        Assert.Throws<ArgumentException>(() => new TeeStream(readOnly));
    }
}
