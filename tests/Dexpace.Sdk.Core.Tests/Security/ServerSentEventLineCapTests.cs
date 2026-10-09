// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 7b, issue #10 (SSE-19; design §7.2, §10 entry 20): a hostile server that streams a line with no
/// terminator must not grow the reader without limit. The verified defect was a line accumulator with no cap, so an
/// unterminated 8 MiB line was buffered whole before any consumer saw a thing. The reader now holds at most
/// <c>maxLineBytes</c> content bytes of a line (1 MiB by default), throws <see cref="ServerSentEventLineTooLongException"/>
/// after reading at most one cap plus two read buffers, and stays failed because the stream is mid-line. Permanent
/// (roadmap constraint 5; precedent <c>RetryPacingOverflowTests</c>); the aggregate-per-block residual R1 (P7b-5) is a
/// ruling still open for the lead and is deliberately not pinned here.
/// </summary>
[Trait("Category", "Security")]
public sealed class ServerSentEventLineCapTests
{
    // Utf8LineReader refills 4096 bytes at a time and checks its cap before each refill (design fact 1), so a read can
    // overshoot the cap by one refill and the check lands one refill later.
    private const int ReadBuffer = 4096;
    private const int EightMiB = 8 * 1024 * 1024;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unterminated_eight_MiB_line_fails_with_the_SSE_exception_after_a_bounded_read()
    {
        using var source = new EndlessStream((byte)'a', EightMiB);
        var reader = new ServerSentEventReader(source);

        var ex = await Assert.ThrowsAsync<ServerSentEventLineTooLongException>(() => reader.ReadNextAsync(Token).AsTask());

        Assert.Equal(ServerSentEventReader.DefaultMaxLineBytes, ex.MaxLineBytes);
        Assert.True(
            source.BytesServed <= ServerSentEventReader.DefaultMaxLineBytes + (2 * ReadBuffer),
            $"the reader pulled {source.BytesServed} bytes of an {EightMiB}-byte line before failing.");
        Assert.True(source.BytesServed > ServerSentEventReader.DefaultMaxLineBytes, "the cap must be reached, not undercut.");
    }

    [Fact]
    public void The_synchronous_form_is_bounded_in_the_same_way()
    {
        using var source = new EndlessStream((byte)'a', EightMiB);
        var reader = new ServerSentEventReader(source);

        var ex = Assert.Throws<ServerSentEventLineTooLongException>(() => reader.ReadNext());

        Assert.Equal(ServerSentEventReader.DefaultMaxLineBytes, ex.MaxLineBytes);
        Assert.True(source.BytesServed <= ServerSentEventReader.DefaultMaxLineBytes + (2 * ReadBuffer));
    }

    [Fact]
    public async Task The_message_names_the_cap_and_holds_no_content_byte()
    {
        using var source = new EndlessStream((byte)'S', 9 * 1024 * 1024);
        var reader = new ServerSentEventReader(source);

        var ex = await Assert.ThrowsAsync<ServerSentEventLineTooLongException>(() => reader.ReadNextAsync(Token).AsTask());

        Assert.Contains("1048576", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SSSS", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SSSS", ex.ToString(), StringComparison.Ordinal);
        Assert.NotNull(ex.InnerException);
        Assert.DoesNotContain("SSSS", ex.InnerException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_reader_stays_failed_so_the_next_pull_throws_the_same_type_again()
    {
        using var asyncSource = new EndlessStream((byte)'a', EightMiB);
        var asyncReader = new ServerSentEventReader(asyncSource, maxLineBytes: 1024);
        await Assert.ThrowsAsync<ServerSentEventLineTooLongException>(() => asyncReader.ReadNextAsync(Token).AsTask());
        var servedAfterFailure = asyncSource.BytesServed;

        await Assert.ThrowsAsync<ServerSentEventLineTooLongException>(() => asyncReader.ReadNextAsync(Token).AsTask());
        await Assert.ThrowsAsync<ServerSentEventLineTooLongException>(() => asyncReader.ReadNextAsync(Token).AsTask());

        Assert.Equal(servedAfterFailure, asyncSource.BytesServed);

        using var syncSource = new EndlessStream((byte)'a', EightMiB);
        var syncReader = new ServerSentEventReader(syncSource, maxLineBytes: 1024);
        Assert.Throws<ServerSentEventLineTooLongException>(() => syncReader.ReadNext());
        Assert.Throws<ServerSentEventLineTooLongException>(() => syncReader.ReadNext());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_line_of_exactly_the_cap_passes_and_one_byte_more_fails(bool useAsync)
    {
        const int Cap = 64;
        var exact = "data: " + new string('x', Cap - 6) + "\n\n";
        var over = "data: " + new string('x', Cap - 6 + 1) + "\n\n";

        var ok = new ServerSentEventReader(new MemoryStream(Encoding.UTF8.GetBytes(exact)), Cap);
        var first = useAsync ? await ok.ReadNextAsync(Token) : ok.ReadNext();
        Assert.NotNull(first);
        Assert.Equal(Cap - 6, Assert.Single(first.Data).Length);

        var failing = new ServerSentEventReader(new MemoryStream(Encoding.UTF8.GetBytes(over)), Cap);
        if (useAsync)
        {
            await Assert.ThrowsAsync<ServerSentEventLineTooLongException>(() => failing.ReadNextAsync(Token).AsTask());
        }
        else
        {
            Assert.Throws<ServerSentEventLineTooLongException>(() => failing.ReadNext());
        }
    }

    [Theory]
    [InlineData(16)]
    [InlineData(1024)]
    public async Task A_custom_cap_is_honoured(int cap)
    {
        using var source = new EndlessStream((byte)'a', EightMiB);
        var reader = new ServerSentEventReader(source, cap);

        var ex = await Assert.ThrowsAsync<ServerSentEventLineTooLongException>(() => reader.ReadNextAsync(Token).AsTask());

        Assert.Equal(cap, ex.MaxLineBytes);
        Assert.Contains(cap.ToString(System.Globalization.CultureInfo.InvariantCulture), ex.Message, StringComparison.Ordinal);
        Assert.True(source.BytesServed <= cap + (2 * ReadBuffer));
    }

    [Fact]
    public async Task The_exception_is_a_StreamingException()
    {
        using var source = new EndlessStream((byte)'a', EightMiB);
        var reader = new ServerSentEventReader(source, 256);

        var ex = await Record.ExceptionAsync(() => reader.ReadNextAsync(Token).AsTask());

        Assert.IsAssignableFrom<StreamingException>(ex);
        Assert.IsAssignableFrom<SdkException>(ex);
    }

    [Fact]
    public async Task Lines_that_each_fit_are_not_affected_by_the_cap_across_many_events()
    {
        // The cap is per line: a long stream of short lines is unlimited, so the cap never truncates a healthy stream.
        var line = "data: " + new string('y', 50) + "\n\n";
        var reader = new ServerSentEventReader(new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(line, 5000)))), 64);

        var count = 0;
        while (await reader.ReadNextAsync(Token) is not null)
        {
            count++;
        }

        Assert.Equal(5000, count);
    }

    // Serves one repeated byte until a total is reached, counting what it served: an unterminated line of that length.
    private sealed class EndlessStream(byte value, int total) : Stream
    {
        private long _served;

        public long BytesServed => Interlocked.Read(ref _served);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var n = (int)Math.Min(buffer.Length, total - _served);
            buffer[..n].Fill(value);
            Interlocked.Add(ref _served, n);
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(Read(buffer.Span));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
