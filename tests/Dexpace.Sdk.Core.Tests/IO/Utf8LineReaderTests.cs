// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The IO-14 and decode cases are driven by tests/vectors/io/utf8-lines.json, ported from nodejs-sdk@c0ff3fd
// packages/core/src/io/buffered-source.text.test.ts and product-spec appendix C IO-14.

using System.Text;
using Dexpace.Sdk.Core.IO;
using Dexpace.Sdk.TestSupport.Streams;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>The line reader: IO-2, IO-6, IO-13, IO-14, IO-21 (line-reader form), IO-37, IO-41, IO-42.</summary>
[Trait("Category", "Unit")]
public class Utf8LineReaderTests
{
    private static readonly int[] s_readSizes = [1, 2, 3, 7, 4096];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static LineTerminators ModeOf(string mode) =>
        mode == "whatwg" ? LineTerminators.Whatwg : LineTerminators.LineFeedOrCrLf;

    private static async Task<string?> Read(Utf8LineReader reader, bool useAsync) =>
        useAsync ? await reader.ReadLineAsync(Token) : reader.ReadLine();

    private static async Task<List<string?>> ReadAll(Utf8LineReader reader, bool useAsync)
    {
        var lines = new List<string?>();
        while (true)
        {
            var line = await Read(reader, useAsync);
            lines.Add(line);
            if (line is null)
            {
                return lines;
            }
        }
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Vectors_produce_the_expected_lines_at_every_read_size(bool useAsync)
    {
        var cases = VectorFile.Load<LineCase>("io/utf8-lines.json");
        Assert.NotEmpty(cases);

        foreach (var vector in cases)
        {
            var input = vector.InputHex is not null ? Convert.FromHexString(vector.InputHex) : Utf8(vector.Input ?? string.Empty);
            foreach (var size in s_readSizes)
            {
                using var reader = new Utf8LineReader(new ChunkedReadStream(input, size), ModeOf(vector.Mode));

                var lines = await ReadAll(reader, useAsync);

                if (!lines.SequenceEqual(vector.Lines))
                {
                    Assert.Fail($"{vector.Name} at {size} bytes per read: expected [{Show(vector.Lines)}] but read [{Show(lines)}].");
                }
            }
        }
    }

    private static string Show(IEnumerable<string?> lines) =>
        string.Join(", ", lines.Select(l => l is null ? "null" : "\"" + l.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal) + "\""));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_lone_CR_before_EOF_is_content(bool useAsync)
    {
        using var reader = new Utf8LineReader(new MemoryStream(Utf8("abc\r")));

        Assert.Equal(["abc\r", null], await ReadAll(reader, useAsync));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_lone_CR_is_decided_by_one_byte_of_lookahead_across_a_refill(bool useAsync)
    {
        using var content = new Utf8LineReader(new ChunkedReadStream(Utf8("a\rXb\n"), 1));
        Assert.Equal(["a\rXb", null], await ReadAll(content, useAsync));

        using var terminator = new Utf8LineReader(new ChunkedReadStream(Utf8("a\r\nXb\n"), 1));
        Assert.Equal(["a", "Xb", null], await ReadAll(terminator, useAsync));
    }

    [Fact]
    public async Task Whatwg_mode_terminates_on_CR_immediately_and_skips_the_following_LF()
    {
        using var source = new GatedStream(Utf8("a\r"), Utf8("\nb\n"));
        using var reader = new Utf8LineReader(source, LineTerminators.Whatwg);

        // The first chunk is "a\r" and the second chunk is gated: the line must not wait for it.
        var first = await reader.ReadLineAsync(Token).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.Equal("a", first);

        source.Release();
        Assert.Equal("b", await reader.ReadLineAsync(Token));
        Assert.Null(await reader.ReadLineAsync(Token));
    }

    [Fact]
    public void Whatwg_mode_terminates_on_CR_immediately_in_the_sync_form()
    {
        using var source = new GatedStream(Utf8("a\r"), Utf8("\nb\n"));
        using var reader = new Utf8LineReader(source, LineTerminators.Whatwg);

        Assert.Equal("a", reader.ReadLine());
        Assert.Equal(1, source.ReadsStarted);

        source.Release();
        Assert.Equal("b", reader.ReadLine());
    }

    [Theory]
    [InlineData(false, "é")]
    [InlineData(true, "é")]
    [InlineData(false, "€")]
    [InlineData(true, "€")]
    [InlineData(false, "\U0001F600")]
    [InlineData(true, "\U0001F600")]
    public async Task A_multi_byte_character_split_across_reads_decodes_once(bool useAsync, string character)
    {
        using var reader = new Utf8LineReader(new ChunkedReadStream(Utf8("x" + character + "y\n"), 1));

        Assert.Equal(["x" + character + "y", null], await ReadAll(reader, useAsync));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_malformed_sequence_decodes_to_U_FFFD(bool useAsync)
    {
        using var reader = new Utf8LineReader(new MemoryStream([0x61, 0xFF, 0x62, 0x0A]));

        Assert.Equal(["a�b", null], await ReadAll(reader, useAsync));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_BOM_is_content_in_both_modes(bool useAsync, bool whatwg)
    {
        var mode = whatwg ? LineTerminators.Whatwg : LineTerminators.LineFeedOrCrLf;
        using var reader = new Utf8LineReader(new MemoryStream(Utf8("﻿a\n﻿b\n")), mode);

        Assert.Equal(["﻿a", "﻿b", null], await ReadAll(reader, useAsync));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_line_longer_than_the_cap_throws_InvalidDataException_naming_the_cap(bool useAsync)
    {
        using var reader = new Utf8LineReader(new MemoryStream(Utf8("123456789\nok\n")), maxLineBytes: 8);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Read(reader, useAsync));

        Assert.Contains("8", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("123456789", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_line_longer_than_the_cap_throws_InvalidDataException_naming_the_cap_and_the_reader_stays_failed(bool useAsync)
    {
        using var reader = new Utf8LineReader(new ChunkedReadStream(Utf8("123456789\nok\n"), 3), maxLineBytes: 8);

        await Assert.ThrowsAsync<InvalidDataException>(() => Read(reader, useAsync));
        await Assert.ThrowsAsync<InvalidDataException>(() => Read(reader, useAsync));
        await Assert.ThrowsAsync<InvalidDataException>(() => Read(reader, useAsync));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_hostile_stream_without_a_newline_fails_at_the_cap_without_reading_to_the_end(bool useAsync)
    {
        using var source = new StrictReadStream(new MemoryStream(new byte[1_000_000]));
        using var reader = new Utf8LineReader(source, maxLineBytes: 100);

        await Assert.ThrowsAsync<InvalidDataException>(() => Read(reader, useAsync));

        Assert.True(source.RequestedCounts.Count < 10);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_line_of_exactly_the_cap_is_accepted(bool useAsync)
    {
        using var reader = new Utf8LineReader(new ChunkedReadStream(Utf8("12345678\r\nabcdefgh"), 3), maxLineBytes: 8);

        Assert.Equal(["12345678", "abcdefgh", null], await ReadAll(reader, useAsync));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reading_a_long_line_byte_by_byte_scans_each_byte_once(bool useAsync)
    {
        var input = new string('a', 10_000) + "\n";
        var bytes = Utf8(input);
        using var reader = new Utf8LineReader(new ChunkedReadStream(bytes, 1));

        var line = await Read(reader, useAsync);

        Assert.Equal(10_000, line!.Length);
        Assert.Equal(bytes.Length, reader.ScannedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task An_overflowing_CapturedBytes_slice_reads_as_null(bool useAsync, bool whatwg)
    {
        var mode = whatwg ? LineTerminators.Whatwg : LineTerminators.LineFeedOrCrLf;
        var slice = CapturedBytes.Own(Utf8("abc\ndef\n")).Slice(1_000, 5);
        using var reader = new Utf8LineReader(slice.OpenRead(), mode);

        Assert.Null(await Read(reader, useAsync));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task No_read_is_ever_zero_count(bool useAsync, bool whatwg)
    {
        var mode = whatwg ? LineTerminators.Whatwg : LineTerminators.LineFeedOrCrLf;
        var input = Utf8("one\r\ntwo\rthree\n\n" + new string('x', 9000) + "\nlast\r");
        using var source = new StrictReadStream(new ChunkedReadStream(input, 5000));
        using var reader = new Utf8LineReader(source, mode);

        _ = await ReadAll(reader, useAsync);

        Assert.True(source.AnyRead);
        Assert.All(source.RequestedCounts, count => Assert.True(count >= 1));
    }

    [Fact]
    public void A_non_positive_cap_throws_ArgumentOutOfRangeException()
    {
        using var source = new MemoryStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8LineReader(source, maxLineBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8LineReader(source, maxLineBytes: -1));
    }

    [Fact]
    public async Task Disposing_the_reader_disposes_the_stream_it_owns()
    {
        using var first = new DisposeCountingStream(new MemoryStream());
        new Utf8LineReader(first).Dispose();
        using var second = new DisposeCountingStream(new MemoryStream());
        await new Utf8LineReader(second).DisposeAsync();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public async Task With_leaveOpen_the_stream_survives_the_reader()
    {
        using var source = new DisposeCountingStream(new MemoryStream());
        await using var reader = new Utf8LineReader(source, leaveOpen: true);

        reader.Dispose();
        await reader.DisposeAsync();

        Assert.Equal(0, source.DisposeCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Disposing_twice_in_any_mix_disposes_the_owned_stream_once(bool firstAsync, bool secondAsync)
    {
        using var source = new DisposeCountingStream(new MemoryStream());
        await using var reader = new Utf8LineReader(source);

        foreach (var useAsync in new[] { firstAsync, secondAsync })
        {
            if (useAsync)
            {
                await reader.DisposeAsync();
            }
            else
            {
                reader.Dispose();
            }
        }

        Assert.Equal(1, source.DisposeCount);
    }

    [Fact]
    public async Task Reading_after_dispose_throws_ObjectDisposedException()
    {
        using var reader = new Utf8LineReader(new MemoryStream(Utf8("a\n")));
        reader.Dispose();

        Assert.Throws<ObjectDisposedException>(() => reader.ReadLine());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => reader.ReadLineAsync(Token).AsTask());
    }

    [Fact]
    public async Task ReadLineAsync_honours_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var reader = new Utf8LineReader(new MemoryStream(Utf8("a\n")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadLineAsync(cts.Token).AsTask());
    }

    private sealed record LineCase(string Name, string Mode, string? Input, string? InputHex, List<string?> Lines);

    /// <summary>Serves a first chunk at once and a second only after <see cref="Release"/>.</summary>
    private sealed class GatedStream(byte[] first, byte[] second) : Stream
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _stage;
        private int _position;

        public int ReadsStarted { get; private set; }

        public void Release() => _gate.TrySetResult();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadsStarted++;
            if (_stage == 1)
            {
                _gate.Task.Wait(TestContext.Current.CancellationToken);
            }

            return Serve(buffer.AsSpan(offset, count));
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadsStarted++;
            if (_stage == 1)
            {
                await _gate.Task.WaitAsync(cancellationToken);
            }

            return Serve(buffer.Span);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        private int Serve(Span<byte> destination)
        {
            var chunk = _stage == 0 ? first : second;
            if (_stage > 1)
            {
                return 0;
            }

            var n = Math.Min(destination.Length, chunk.Length - _position);
            chunk.AsSpan(_position, n).CopyTo(destination);
            _position += n;
            if (_position == chunk.Length)
            {
                _stage++;
                _position = 0;
            }

            return n;
        }
    }
}
