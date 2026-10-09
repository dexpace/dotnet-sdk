// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The event value: SSE-4, SSE-9, SSE-11 (construction half), SSE-20, SSE-21, SSE-22 (P7b-7).</summary>
[Trait("Category", "Unit")]
public class ServerSentEventTests
{
    private static ServerSentEvent Full() => new()
    {
        Id = "7",
        Event = "tick",
        Data = ["a", "b"],
        Comment = "hi",
        Retry = TimeSpan.FromMilliseconds(5000),
    };

    [Fact]
    public void Data_defaults_to_an_empty_list_and_the_other_four_to_null()
    {
        var ev = new ServerSentEvent();

        Assert.Empty(ev.Data);
        Assert.Null(ev.Id);
        Assert.Null(ev.Event);
        Assert.Null(ev.Comment);
        Assert.Null(ev.Retry);
    }

    [Fact]
    public void A_present_but_empty_data_line_is_distinct_from_no_data()
    {
        var present = new ServerSentEvent { Data = [""] };
        var absent = new ServerSentEvent();

        Assert.NotEqual(absent, present);
        Assert.True(absent.IsEmpty);
        Assert.False(present.IsEmpty);
        Assert.Equal([""], present.Data);
    }

    [Fact]
    public void Data_is_copied_at_init_and_through_with()
    {
        var source = new List<string> { "a", "b" };
        var ev = new ServerSentEvent { Data = source };
        source.Add("c");
        source[0] = "changed";

        Assert.Equal(["a", "b"], ev.Data);

        var replaced = new List<string> { "x" };
        var copy = ev with { Data = replaced };
        replaced.Add("y");

        Assert.Equal(["x"], copy.Data);
        Assert.Equal(["a", "b"], ev.Data);
    }

    [Fact]
    public void The_exposed_data_list_cannot_be_written_through()
    {
        var ev = new ServerSentEvent { Data = ["a"] };

        Assert.IsNotType<List<string>>(ev.Data);
        Assert.False(ev.Data is string[]);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)ev.Data).Add("x"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)ev.Data)[0] = "x");
    }

    [Fact]
    public void A_with_copy_without_a_data_change_keeps_the_same_values()
    {
        var ev = Full();

        var copy = ev with { Event = "other" };

        Assert.Equal(ev.Data, copy.Data);
        Assert.Equal("other", copy.Event);
        Assert.Equal("tick", ev.Event);
    }

    [Fact]
    public void A_null_data_list_or_a_null_element_is_rejected()
    {
        var nullList = Assert.Throws<ArgumentNullException>(() => new ServerSentEvent { Data = null! });
        Assert.Equal("value", nullList.ParamName);

        var nullElement = Assert.Throws<ArgumentException>(() => new ServerSentEvent { Data = ["a", null!] });
        Assert.Equal("value", nullElement.ParamName);
    }

    [Fact]
    public void Id_rejects_a_NUL_and_the_message_never_echoes_the_value()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ServerSentEvent { Id = "SECRET\0ID" });

        Assert.DoesNotContain("SECRET", ex.Message, StringComparison.Ordinal);
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void Id_accepts_the_empty_string_and_null()
    {
        Assert.Equal(string.Empty, new ServerSentEvent { Id = "" }.Id);
        Assert.Null(new ServerSentEvent { Id = null }.Id);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2147483648L)]
    public void Retry_rejects_negative_and_over_cap_values(long milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ServerSentEvent { Retry = TimeSpan.FromMilliseconds(milliseconds) });
    }

    [Fact]
    public void Retry_rejects_sub_millisecond_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServerSentEvent { Retry = TimeSpan.FromTicks(1) });
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ServerSentEvent { Retry = TimeSpan.FromMilliseconds(5) + TimeSpan.FromTicks(1) });
    }

    [Fact]
    public void Retry_accepts_zero_the_cap_and_null()
    {
        Assert.Equal(TimeSpan.Zero, new ServerSentEvent { Retry = TimeSpan.Zero }.Retry);
        Assert.Equal(
            TimeSpan.FromMilliseconds(int.MaxValue),
            new ServerSentEvent { Retry = TimeSpan.FromMilliseconds(int.MaxValue) }.Retry);
        Assert.Null(new ServerSentEvent { Retry = null }.Retry);
    }

    [Fact]
    public void Equality_compares_all_five_fields_with_data_element_wise_and_ordinal()
    {
        Assert.Equal(Full(), Full());
        Assert.True(Full() == Full());
        Assert.False(Full() != Full());
        Assert.Equal(Full().GetHashCode(), Full().GetHashCode());

        var variants = new[]
        {
            Full() with { Id = "8" },
            Full() with { Event = "tock" },
            Full() with { Data = ["a", "c"] },
            Full() with { Comment = "ho" },
            Full() with { Retry = TimeSpan.FromMilliseconds(5001) },
        };
        Assert.All(variants, variant => Assert.NotEqual(Full(), variant));

        Assert.NotEqual(new ServerSentEvent { Data = ["a", "b"] }, new ServerSentEvent { Data = ["b", "a"] });
        Assert.NotEqual(new ServerSentEvent { Event = "A" }, new ServerSentEvent { Event = "a" });
        Assert.NotEqual(new ServerSentEvent { Id = "" }, new ServerSentEvent { Id = null });
        Assert.NotEqual(new ServerSentEvent { Data = ["a"] }, new ServerSentEvent { Data = ["a", "a"] });
    }

    [Fact]
    public void Equality_handles_null_and_foreign_objects()
    {
        var ev = Full();

        Assert.False(ev.Equals((ServerSentEvent?)null));
        Assert.False(ev.Equals((object?)null));
        Assert.False(ev.Equals("not an event"));
        Assert.True(ev.Equals((object)Full()));
        Assert.True(ev.Equals(ev));
    }

    [Fact]
    public void ToString_is_lossless_so_unequal_events_never_print_alike()
    {
        Assert.Contains("Id = \"\"", new ServerSentEvent { Id = "" }.ToString(), StringComparison.Ordinal);
        Assert.Contains("Id = null", new ServerSentEvent().ToString(), StringComparison.Ordinal);
        Assert.Contains("Data = [\"a\", \"b\"]", Full().ToString(), StringComparison.Ordinal);
        Assert.Contains("Data = []", new ServerSentEvent().ToString(), StringComparison.Ordinal);
        Assert.Contains("Retry = 5000ms", Full().ToString(), StringComparison.Ordinal);
        Assert.Contains("Retry = null", new ServerSentEvent().ToString(), StringComparison.Ordinal);

        var escaped = new ServerSentEvent { Event = "q\"b\\n\n\r\t\u0001\u007f" }.ToString();
        Assert.Contains("\"q\\\"b\\\\n\\n\\r\\t\\u0001\\u007f\"", escaped, StringComparison.Ordinal);
    }

    [Fact]
    public void Distinct_events_from_a_generated_set_never_share_a_string()
    {
        string?[] texts = [null, "", " ", "a", "\"", "\\", "\n", "null", "a\", Event = \"b"];
        var events = new List<ServerSentEvent>();
        foreach (var id in texts.Where(t => t is null || !t.Contains('\0', StringComparison.Ordinal)))
        {
            foreach (var comment in texts)
            {
                events.Add(new ServerSentEvent { Id = id, Comment = comment });
                events.Add(new ServerSentEvent { Id = id, Event = comment, Data = comment is null ? [] : [comment] });
            }
        }

        var byString = events.GroupBy(e => e.ToString(), StringComparer.Ordinal).ToList();
        foreach (var group in byString)
        {
            Assert.Single(group.Distinct());
        }
    }

    [Fact]
    public void IsEmpty_is_true_only_when_every_field_is_absent()
    {
        Assert.True(new ServerSentEvent().IsEmpty);
        Assert.False((new ServerSentEvent { Id = "" }).IsEmpty);
        Assert.False((new ServerSentEvent { Event = "" }).IsEmpty);
        Assert.False((new ServerSentEvent { Comment = "" }).IsEmpty);
        Assert.False((new ServerSentEvent { Retry = TimeSpan.Zero }).IsEmpty);
        Assert.False((new ServerSentEvent { Data = [""] }).IsEmpty);
    }
}
