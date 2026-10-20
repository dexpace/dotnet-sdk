// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-3, OBS-4, OBS-6 and P5b-3: the structured state of the header-bearing events.</summary>
[Trait("Category", "Unit")]
public sealed class HttpLogRecordTests
{
    private static HttpLogRecord Make(params (string Key, object? Value)[] pairs) =>
        new(
            [.. pairs.Select(p => new KeyValuePair<string, object?>(p.Key, p.Value))],
            "HTTP {http.request.method}",
            "HTTP GET");

    [Fact]
    public void The_record_is_a_read_only_list_of_key_value_pairs()
    {
        var record = Make(("a", "1"), ("b", 2));

        Assert.Equal(3, record.Count);
        Assert.Equal("a", record[0].Key);
        Assert.Equal("b", record[1].Key);
        Assert.Throws<ArgumentOutOfRangeException>(() => record[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => record[-1]);
        Assert.Equal(["a", "b", "{OriginalFormat}"], record.Select(p => p.Key));
        Assert.Equal(["a", "b", "{OriginalFormat}"], ((System.Collections.IEnumerable)record).Cast<KeyValuePair<string, object?>>().Select(p => p.Key));
    }

    [Fact]
    public void OriginalFormat_is_the_last_pair_so_template_grouping_sinks_group()
    {
        var record = Make(("a", "1"));

        Assert.Single(record, p => p.Key == "{OriginalFormat}");
        Assert.Equal(new KeyValuePair<string, object?>("{OriginalFormat}", "HTTP {http.request.method}"), record[^1]);
    }

    [Fact]
    public void A_null_value_is_emitted_as_the_literal_null()
    {
        var record = Make(("n", null), ("m", "x"), ("z", 0));

        Assert.Equal("n", record[0].Key);
        Assert.IsType<string>(record[0].Value);
        Assert.Equal("null", record[0].Value);
        Assert.Equal("x", record[1].Value);
        Assert.Equal(0, record[2].Value);
        Assert.DoesNotContain(record, p => p.Value is null);

        // A sink that renders the state pair by pair sees `null`, never the framework's `(null)` and never an empty string.
        var rendered = string.Join(' ', record.Select(p => $"{p.Key}={p.Value}"));
        Assert.Contains("n=null", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("(null)", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void The_formatter_renders_the_precomputed_message()
    {
        var record = Make(("a", "SECRET-HEADER-VALUE"));

        Assert.Equal("HTTP GET", HttpLogRecord.Formatter(record, null));
        Assert.Equal("HTTP GET", record.ToString());
    }

    [Fact]
    public void No_key_is_named_event_and_none_is_empty()
    {
        var record = Make(("a", 1), ("b", 2));

        Assert.All(record, p => Assert.False(string.IsNullOrEmpty(p.Key)));
        Assert.DoesNotContain(record, p => p.Key == "event");
    }
}
