// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Serialization;

/// <summary>
/// SERDE-13 / SERDE-27 (P7a-9, P7a-10): the internal helpers behind the typed readers: the root-null rule, the missing-body
/// message, and the one-byte peek that tells an absent payload from a present one without materialising it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerdeValuesTests
{
    [Fact]
    public void RequireNonNull_returns_a_non_null_reference()
    {
        Assert.Equal("a", SerdeValues.RequireNonNull<string>("a"));
    }

    [Fact]
    public void RequireNonNull_throws_DeserializationException_naming_T_for_a_null_reference()
    {
        var ex = Assert.Throws<DeserializationException>(() => SerdeValues.RequireNonNull<string>(null));

        Assert.Contains("System.String", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void RequireNonNull_returns_default_for_a_non_nullable_value_type_default()
    {
        // A non-nullable value type never reaches the throw: its default is a value, not null (the codec rejects wire null).
        Assert.Equal(0, SerdeValues.RequireNonNull<int>(0));
    }

    [Fact]
    public void RequireNonNull_admits_a_null_Nullable()
    {
        Assert.Null(SerdeValues.RequireNonNull<int?>(null));
    }

    [Fact]
    public void The_message_names_ReadValueOrDefaultAsync()
    {
        var ex = Assert.Throws<DeserializationException>(() => SerdeValues.RequireNonNull<object>(null));

        Assert.Contains("ReadValueOrDefaultAsync", ex.Message, StringComparison.Ordinal);
        Assert.Contains("JSON literal null", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoBody_names_T_and_carries_no_inner_exception()
    {
        var ex = SerdeValues.NoBody<Version>();

        Assert.Equal("The response has no body to deserialize as 'System.Version'.", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Peek_returns_null_for_an_empty_stream_and_disposes_it_once()
    {
        using var stream = new DisposeCountingStream(new MemoryStream());

        var peeked = SerdeValues.PeekFirstByte(stream);

        Assert.Null(peeked);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task Peek_returns_null_for_an_empty_stream_and_disposes_it_once_async()
    {
        using var stream = new DisposeCountingStream(new MemoryStream());

        var peeked = await SerdeValues.PeekFirstByteAsync(stream, TestContext.Current.CancellationToken);

        Assert.Null(peeked);
        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public void Peek_returns_a_stream_replaying_the_first_byte()
    {
        using var stream = new DisposeCountingStream(new MemoryStream("abc"u8.ToArray()));

        using var peeked = SerdeValues.PeekFirstByte(stream)!;
        using var copy = new MemoryStream();
        peeked.CopyTo(copy);

        Assert.Equal("abc"u8.ToArray(), copy.ToArray());
    }

    [Fact]
    public async Task Peek_returns_a_stream_replaying_the_first_byte_async()
    {
        using var stream = new DisposeCountingStream(new MemoryStream("abc"u8.ToArray()));

        var peeked = await SerdeValues.PeekFirstByteAsync(stream, TestContext.Current.CancellationToken);
        await using var scope = peeked!;
        using var copy = new MemoryStream();
        await peeked!.CopyToAsync(copy, TestContext.Current.CancellationToken);

        Assert.Equal("abc"u8.ToArray(), copy.ToArray());
    }

    [Fact]
    public void Peek_disposal_disposes_the_underlying_stream_once_after_a_read_to_the_end()
    {
        // PrefixedReadStream closes its tail at EOF and again on Dispose; the close-once latch keeps the real stream's count at 1.
        using var stream = new DisposeCountingStream(new MemoryStream("abc"u8.ToArray()));
        var peeked = SerdeValues.PeekFirstByte(stream)!;
        peeked.CopyTo(Stream.Null);

        peeked.Dispose();

        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public async Task Peek_disposal_disposes_the_underlying_stream_once_after_a_read_to_the_end_async()
    {
        using var stream = new DisposeCountingStream(new MemoryStream("abc"u8.ToArray()));
        var peeked = (await SerdeValues.PeekFirstByteAsync(stream, TestContext.Current.CancellationToken))!;
        await peeked.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken);

        await peeked.DisposeAsync();

        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public void Peek_disposal_without_reading_further_still_disposes_the_underlying_stream_once()
    {
        using var stream = new DisposeCountingStream(new MemoryStream("abc"u8.ToArray()));
        var peeked = SerdeValues.PeekFirstByte(stream)!;

        peeked.Dispose();
        peeked.Dispose();

        Assert.Equal(1, stream.DisposeCount);
    }

    [Fact]
    public void A_failure_while_peeking_propagates_and_disposes_the_stream()
    {
        using var stream = new DisposeCountingStream(new ThrowingStream());

        Assert.Throws<IOException>(() => SerdeValues.PeekFirstByte(stream));
        Assert.Equal(1, stream.DisposeCount);
    }

    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("boom");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
