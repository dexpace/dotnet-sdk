// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.ServerSentEvents;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The line layer under the event layer: terminators and the BOM (SSE-2, SSE-12, SSE-14, SSE-19; P7b-3).</summary>
[Trait("Category", "Unit")]
public class ServerSentEventLineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static async Task<List<ServerSentEvent>> ReadEvents(Stream source, bool useAsync, int maxLineBytes = ServerSentEventReader.DefaultMaxLineBytes)
    {
        var reader = new ServerSentEventReader(source, maxLineBytes);
        var events = new List<ServerSentEvent>();
        while (true)
        {
            var next = useAsync ? await reader.ReadNextAsync(Token) : reader.ReadNext();
            if (next is null)
            {
                return events;
            }

            events.Add(next);
        }
    }

    [Theory]
    [InlineData("event: x\ndata: a\ndata: b\n\n", 1, false)]
    [InlineData("event: x\rdata: a\rdata: b\r\r", 1, false)]
    [InlineData("event: x\r\ndata: a\r\ndata: b\r\n\r\n", 1, false)]
    [InlineData("event: x\rdata: a\r\ndata: b\n\r\n", 1, false)]
    [InlineData("event: x\ndata: a\rdata: b\r\n\n", 1, true)]
    [InlineData("event: x\ndata: a\ndata: b\n\n", 4096, true)]
    [InlineData("event: x\rdata: a\rdata: b\r\r", 4096, true)]
    [InlineData("event: x\r\ndata: a\r\ndata: b\r\n\r\n", 3, true)]
    [InlineData("event: x\rdata: a\r\ndata: b\n\r\n", 2, false)]
    public async Task The_same_event_under_each_terminator_and_mixed_terminators_is_identical(string wire, int chunk, bool useAsync)
    {
        var events = await ReadEvents(new ChunkedReadStream(Utf8(wire), chunk), useAsync);

        var only = Assert.Single(events);
        Assert.Equal("x", only.Event);
        Assert.Equal(["a", "b"], only.Data);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_CR_at_a_chunk_end_then_LF_at_the_next_chunk_start_is_one_terminator(bool useAsync)
    {
        // One byte per read puts every CR at a chunk end and every LF at the next chunk start. If CRLF split into two
        // terminators there would be a blank line between the data lines and two events.
        var events = await ReadEvents(new ChunkedReadStream(Utf8("data: a\r\ndata: b\r\n\r\n"), 1), useAsync);

        Assert.Equal(["a", "b"], Assert.Single(events).Data);
    }

    [Fact]
    public async Task A_CR_terminated_event_is_delivered_before_the_next_bytes_arrive()
    {
        // The defect design 7.2 found in StreamReader: a CR-terminated line was held for the next chunk, so an event
        // ended by CR CR was not delivered until the server sent more. The source below never returns more.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new GatedAfterStream(Utf8("data: x\r\r"), gate.Task);
        var reader = new ServerSentEventReader(source);

        var first = await reader.ReadNextAsync(Token).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Token);

        Assert.NotNull(first);
        Assert.Equal(["x"], first.Data);
        Assert.False(gate.Task.IsCompleted);
        gate.SetResult(true);
        Assert.Null(await reader.ReadNextAsync(Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_leading_BOM_is_removed_once(bool useAsync)
    {
        byte[] wire = [0xEF, 0xBB, 0xBF, .. Utf8("data: x\n\n")];

        var events = await ReadEvents(new MemoryStream(wire), useAsync);

        Assert.Equal(["x"], Assert.Single(events).Data);
    }

    [Theory]
    [InlineData("EFBB0A646174613A20780A0A")]
    [InlineData("EFBB410A646174613A20780A0A")]
    public async Task A_partial_BOM_is_content_and_swallows_nothing(string hex)
    {
        // Fact 2: EF BB decodes to U+FFFD, never U+FEFF, so it is an ordinary (unknown) first line.
        var events = await ReadEvents(new MemoryStream(Convert.FromHexString(hex)), useAsync: true);

        Assert.Equal(["x"], Assert.Single(events).Data);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_doubled_BOM_keeps_the_second_as_data(bool useAsync)
    {
        byte[] wire = [0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF, .. Utf8("data: x\n\n")];

        var events = await ReadEvents(new MemoryStream(wire), useAsync);

        // The second BOM starts the field name, so the line is an unknown field and the block was never seen.
        Assert.Empty(events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_BOM_split_across_reads_is_still_removed(bool useAsync)
    {
        byte[] wire = [0xEF, 0xBB, 0xBF, .. Utf8("data: x\n\n")];

        var events = await ReadEvents(new ChunkedReadStream(wire, 1), useAsync);

        Assert.Equal(["x"], Assert.Single(events).Data);
    }

    [Fact]
    public async Task A_BOM_on_a_later_line_is_not_removed()
    {
        byte[] wire = [.. Utf8("data: a\n"), 0xEF, 0xBB, 0xBF, .. Utf8("data: b\n\n")];

        var events = await ReadEvents(new MemoryStream(wire), useAsync: true);

        // Node's test: the BOM-prefixed line is an unknown field name and is dropped.
        Assert.Equal(["a"], Assert.Single(events).Data);
    }

    [Fact]
    public async Task A_BOM_after_a_non_BOM_first_line_is_data_not_a_second_chance()
    {
        // The flag is set by the first line read, BOM or not: a BOM at the start of the second block is not stripped.
        byte[] wire = [.. Utf8("data: a\n\n"), 0xEF, 0xBB, 0xBF, .. Utf8("data: b\n\n")];

        var events = await ReadEvents(new MemoryStream(wire), useAsync: false);

        Assert.Equal(["a"], Assert.Single(events).Data);
    }

    [Fact]
    public async Task A_BOM_costs_three_bytes_of_the_first_lines_cap()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] fits = [.. bom, .. Utf8("data: " + new string('x', 64 - 3 - 6) + "\n\n")];
        byte[] over = [.. bom, .. Utf8("data: " + new string('x', 64 - 3 - 6 + 1) + "\n\n")];

        var events = await ReadEvents(new MemoryStream(fits), useAsync: true, maxLineBytes: 64);
        Assert.Single(events);

        await Assert.ThrowsAsync<ServerSentEventLineTooLongException>(
            () => ReadEvents(new MemoryStream(over), useAsync: true, maxLineBytes: 64));
    }

    [Fact]
    public async Task Malformed_UTF8_decodes_to_the_replacement_character()
    {
        // A pin of 3a (P3a-11): the decode never throws, it substitutes.
        byte[] wire = [.. Utf8("data: "), 0xFF, .. Utf8("\n\n")];

        var events = await ReadEvents(new MemoryStream(wire), useAsync: true);

        Assert.Equal(["�"], Assert.Single(events).Data);
    }

    // Serves its bytes on the first read, then waits for the gate; a second read before the gate would hang the test.
    private sealed class GatedAfterStream(byte[] bytes, Task gate) : Stream
    {
        private int _served;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_served < bytes.Length)
            {
                var n = Math.Min(buffer.Length, bytes.Length - _served);
                bytes.AsMemory(_served, n).CopyTo(buffer);
                _served += n;
                return n;
            }

            await gate.WaitAsync(cancellationToken);
            return 0;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
