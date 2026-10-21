// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>
/// The per-event data cap (SSE-19, design §10 entry 20 residual R1, P11-19): the data lines of one event cost their UTF-8 value bytes plus one
/// each, and an event whose data passes <c>maxEventBytes</c> fails with <see cref="ServerSentEventTooLargeException"/>. The Security test that
/// pins the hostile stream is <c>ServerSentEventLineCapTests</c>; this class pins the arithmetic, the surface and the defaults.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ServerSentEventEventCapTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ServerSentEventReader ReaderOver(string wire, int eventCap) =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(wire)), ServerSentEventReader.DefaultMaxLineBytes, eventCap);

    private static async Task<ServerSentEvent?> NextAsync(ServerSentEventReader reader, bool useAsync) =>
        useAsync ? await reader.ReadNextAsync(Token) : reader.ReadNext();

    [Fact]
    public void The_default_event_cap_is_8_MiB_and_eight_times_the_line_cap()
    {
        Assert.Equal(8 * 1024 * 1024, ServerSentEventReader.DefaultMaxEventBytes);
        Assert.Equal(8 * ServerSentEventReader.DefaultMaxLineBytes, ServerSentEventReader.DefaultMaxEventBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_event_of_exactly_the_cap_passes_and_one_more_data_byte_fails(bool useAsync)
    {
        const int Cap = 64;

        // One data line costs its value bytes plus one for the line break that ends it: 63 + 1 = 64.
        var exact = ReaderOver("data:" + new string('x', Cap - 1) + "\n\n", Cap);
        var accepted = await NextAsync(exact, useAsync);
        Assert.NotNull(accepted);
        Assert.Equal(Cap - 1, Assert.Single(accepted.Data).Length);

        var over = ReaderOver("data:" + new string('x', Cap) + "\n\n", Cap);
        var ex = useAsync
            ? await Assert.ThrowsAsync<ServerSentEventTooLargeException>(() => over.ReadNextAsync(Token).AsTask())
            : Assert.Throws<ServerSentEventTooLargeException>(() => over.ReadNext());
        Assert.Equal(Cap, ex.MaxEventBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_cost_is_the_sum_over_the_data_lines_of_one_event(bool useAsync)
    {
        // Four lines of 15 bytes cost 16 each: 64 fits a cap of 64, a fifth line does not.
        var line = "data:" + new string('y', 15) + "\n";
        var fits = ReaderOver(string.Concat(Enumerable.Repeat(line, 4)) + "\n", 64);
        Assert.Equal(4, (await NextAsync(fits, useAsync))!.Data.Count);

        var tooMany = ReaderOver(string.Concat(Enumerable.Repeat(line, 5)) + "\n", 64);
        if (useAsync)
        {
            await Assert.ThrowsAsync<ServerSentEventTooLargeException>(() => tooMany.ReadNextAsync(Token).AsTask());
        }
        else
        {
            Assert.Throws<ServerSentEventTooLargeException>(() => tooMany.ReadNext());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_cap_is_per_event_so_a_long_stream_of_events_that_each_fit_is_unaffected(bool useAsync)
    {
        var reader = ReaderOver(string.Concat(Enumerable.Repeat("data:" + new string('z', 30) + "\n\n", 5_000)), 64);

        var count = 0;
        while (await NextAsync(reader, useAsync) is not null)
        {
            count++;
        }

        Assert.Equal(5_000, count);
    }

    [Fact]
    public void Data_is_counted_in_UTF8_bytes_not_characters()
    {
        // "é" is two bytes, U+20AC three, U+1F600 four (two chars): 2 + 3 + 4 = 9, plus one for the line break = 10.
        const string Value = "é€\U0001F600";
        Assert.Equal(10, Encoding.UTF8.GetByteCount(Value) + 1);

        Assert.NotNull(ReaderOver("data:" + Value + "\n\n", 10).ReadNext());
        Assert.Throws<ServerSentEventTooLargeException>(() => ReaderOver("data:" + Value + "\n\n", 9).ReadNext());
    }

    [Fact]
    public void An_empty_data_line_costs_one_so_empty_lines_cannot_grow_an_event_without_bound()
    {
        Assert.NotNull(ReaderOver(string.Concat(Enumerable.Repeat("data:\n", 8)) + "\n", 8).ReadNext());

        Assert.Throws<ServerSentEventTooLargeException>(() => ReaderOver(string.Concat(Enumerable.Repeat("data:\n", 9)) + "\n", 8).ReadNext());
    }

    [Fact]
    public void Only_data_counts_so_event_id_retry_and_comment_lines_are_never_a_cap_failure()
    {
        var lines = string.Concat(Enumerable.Repeat("event: e\nid: 1\nretry: 100\n: a comment that is longer than the cap\nignored: field\n", 50));
        var reader = ReaderOver(lines + "data:x\n\n", 8);

        var ev = reader.ReadNext();

        Assert.NotNull(ev);
        Assert.Equal(["x"], ev.Data);
    }

    [Fact]
    public void A_trailing_event_with_no_blank_line_is_checked_like_any_other()
    {
        Assert.NotNull(ReaderOver("data:abc", 4).ReadNext());
        Assert.Throws<ServerSentEventTooLargeException>(() => ReaderOver("data:abcd", 4).ReadNext());
    }

    [Fact]
    public void The_failure_names_the_cap_and_holds_no_content_byte()
    {
        var reader = ReaderOver("data:" + new string('S', 100) + "\n\n", 16);

        var ex = Assert.Throws<ServerSentEventTooLargeException>(() => reader.ReadNext());

        Assert.Contains("16", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SSSS", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SSSS", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_one_and_two_argument_forms_apply_the_default_event_cap_and_the_line_cap_alone_is_unchanged()
    {
        // 100 data lines of 100 bytes is 10 KB, far under 8 MiB: nothing fails with either default form.
        var wire = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("data:" + new string('q', 99) + "\n", 100)) + "\n");

        Assert.Equal(100, new ServerSentEventReader(new MemoryStream(wire)).ReadNext()!.Data.Count);
        Assert.Equal(100, new ServerSentEventReader(new MemoryStream(wire), 1024).ReadNext()!.Data.Count);
        Assert.Equal(100, ServerSentEventReader.ReadAll(new MemoryStream(wire)).Single().Data.Count);
        Assert.Equal(100, ServerSentEventReader.ReadAll(new MemoryStream(wire), 1024).Single().Data.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_event_cap_is_rejected_by_every_entry_point(int cap)
    {
        static MemoryStream Source() => new([]);

        Assert.Equal("maxEventBytes", Assert.Throws<ArgumentOutOfRangeException>(() => new ServerSentEventReader(Source(), 1024, cap)).ParamName);
        Assert.Equal("maxEventBytes", Assert.Throws<ArgumentOutOfRangeException>(() => ServerSentEventReader.ReadAll(Source(), 1024, cap)).ParamName);
        Assert.Equal("maxEventBytes", Assert.Throws<ArgumentOutOfRangeException>(() => ServerSentEventReader.ReadAllAsync(Source(), 1024, cap)).ParamName);
    }

    [Fact]
    public async Task ReadAll_and_ReadAllAsync_apply_the_event_cap_at_the_pull_that_passes_it()
    {
        var wire = Encoding.UTF8.GetBytes("data:ok\n\ndata:" + new string('x', 64) + "\n\n");

        var blocking = ServerSentEventReader.ReadAll(new MemoryStream(wire), 1024, 16).GetEnumerator();
        Assert.True(blocking.MoveNext());
        Assert.Equal("ok", Assert.Single(blocking.Current.Data));
        Assert.Throws<ServerSentEventTooLargeException>(() => blocking.MoveNext());

        var seen = new List<string>();
        await Assert.ThrowsAsync<ServerSentEventTooLargeException>(async () =>
        {
            await foreach (var ev in ServerSentEventReader.ReadAllAsync(new MemoryStream(wire), 1024, 16).WithCancellation(Token))
            {
                seen.Add(ev.Data[0]);
            }
        });
        Assert.Equal(["ok"], seen);
    }
}
