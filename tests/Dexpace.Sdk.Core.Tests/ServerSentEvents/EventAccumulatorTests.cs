// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>
/// The per-block grammar, driven line by line with no stream (convention 12): SSE-1, SSE-3 to SSE-11, SSE-13, SSE-14,
/// SSE-16. The whole-stream behaviour is the vector table in <c>ServerSentEventParserTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public class EventAccumulatorTests
{
    private static ServerSentEvent? Feed(EventAccumulator accumulator, params string[] lines)
    {
        ServerSentEvent? last = null;
        foreach (var line in lines)
        {
            last = accumulator.Accept(line);
        }

        return last;
    }

    [Fact]
    public void Accept_returns_null_until_a_blank_line()
    {
        var accumulator = new EventAccumulator();

        Assert.Null(accumulator.Accept("data: a"));
        Assert.Null(accumulator.Accept("event: e"));
        Assert.Null(accumulator.Accept(": c"));

        var dispatched = accumulator.Accept(string.Empty);

        Assert.NotNull(dispatched);
        Assert.Equal(["a"], dispatched.Data);
        Assert.Equal("e", dispatched.Event);
        Assert.Equal("c", dispatched.Comment);
    }

    [Fact]
    public void A_blank_line_with_nothing_seen_returns_null()
    {
        var accumulator = new EventAccumulator();

        Assert.Null(accumulator.Accept(string.Empty));
        Assert.Null(accumulator.Accept(string.Empty));
        Assert.Null(accumulator.Flush());
    }

    [Fact]
    public void Flush_dispatches_a_pending_block_once()
    {
        var accumulator = new EventAccumulator();
        accumulator.Accept("data: tail");

        var first = accumulator.Flush();

        Assert.NotNull(first);
        Assert.Equal(["tail"], first.Data);
        Assert.Null(accumulator.Flush());
        Assert.Null(accumulator.Accept(string.Empty));
    }

    [Theory]
    [InlineData("id: a\0b")]
    [InlineData("retry: bad")]
    [InlineData("retry: 2147483648")]
    [InlineData("retry:")]
    [InlineData("unknown: x")]
    [InlineData("unknown")]
    [InlineData("Data: x")]
    public void An_ignored_field_does_not_mark_the_block_seen(string ignored)
    {
        var accumulator = new EventAccumulator();

        Assert.Null(accumulator.Accept(ignored));
        Assert.Null(accumulator.Accept(string.Empty));
        Assert.Null(accumulator.Flush());
    }

    [Fact]
    public void An_ignored_id_and_an_ignored_retry_leave_earlier_valid_values_alone()
    {
        var accumulator = new EventAccumulator();

        var dispatched = Feed(accumulator, "id: good", "id: a\0b", "retry: 7", "retry: nope", string.Empty);

        Assert.NotNull(dispatched);
        Assert.Equal("good", dispatched.Id);
        Assert.Equal(TimeSpan.FromMilliseconds(7), dispatched.Retry);
    }

    [Fact]
    public void Every_accumulator_is_reset_after_dispatch()
    {
        var accumulator = new EventAccumulator();
        Feed(accumulator, "id: 1", "event: e", "retry: 5", ": c", "data: d", string.Empty);

        var second = Feed(accumulator, "data: only", string.Empty);

        Assert.NotNull(second);
        Assert.Null(second.Id);
        Assert.Null(second.Event);
        Assert.Null(second.Comment);
        Assert.Null(second.Retry);
        Assert.Equal(["only"], second.Data);
    }

    [Fact]
    public void Every_accumulator_is_reset_after_a_flush()
    {
        var accumulator = new EventAccumulator();
        accumulator.Accept("id: 1");
        accumulator.Accept("data: d");
        Assert.NotNull(accumulator.Flush());

        accumulator.Accept("event: e");
        var next = accumulator.Flush();

        Assert.NotNull(next);
        Assert.Null(next.Id);
        Assert.Empty(next.Data);
        Assert.Equal("e", next.Event);
    }

    [Theory]
    [InlineData("Data: x")]
    [InlineData("DATA: x")]
    [InlineData("Event: x")]
    [InlineData("ID: x")]
    [InlineData("Retry: 5")]
    [InlineData(" data: x")]
    [InlineData("data : x")]
    [InlineData("data : x")]
    public void Field_names_are_ordinal_and_case_sensitive(string line)
    {
        var accumulator = new EventAccumulator();

        Assert.Null(Feed(accumulator, line, string.Empty));
    }

    [Fact]
    public void The_value_starts_after_the_first_colon_and_one_space()
    {
        var accumulator = new EventAccumulator();

        var dispatched = Feed(accumulator, "data:  a: b ", "event:x:y", string.Empty);

        Assert.NotNull(dispatched);
        Assert.Equal([" a: b "], dispatched.Data);
        Assert.Equal("x:y", dispatched.Event);
    }

    [Fact]
    public void A_colon_less_line_is_a_name_with_an_empty_value()
    {
        var accumulator = new EventAccumulator();

        var dispatched = Feed(accumulator, "data", "event", "id", string.Empty);

        Assert.NotNull(dispatched);
        Assert.Equal([string.Empty], dispatched.Data);
        Assert.Equal(string.Empty, dispatched.Event);
        Assert.Equal(string.Empty, dispatched.Id);
    }

    [Fact]
    public void A_comment_keeps_the_latest_text_with_one_space_stripped()
    {
        var accumulator = new EventAccumulator();

        var dispatched = Feed(accumulator, ": first", ":  second", string.Empty);

        Assert.NotNull(dispatched);
        Assert.Equal(" second", dispatched.Comment);
    }

    [Fact]
    public void Data_lines_accumulate_unjoined_in_order()
    {
        var accumulator = new EventAccumulator();

        var dispatched = Feed(accumulator, "data: a", "data", "data: b", string.Empty);

        Assert.NotNull(dispatched);
        Assert.Equal(["a", string.Empty, "b"], dispatched.Data);
    }

    [Fact]
    public void The_dispatched_event_does_not_alias_the_accumulator()
    {
        var accumulator = new EventAccumulator();
        var first = Feed(accumulator, "data: a", string.Empty);
        Feed(accumulator, "data: b", string.Empty);

        Assert.NotNull(first);
        Assert.Equal(["a"], first.Data);
    }
}
