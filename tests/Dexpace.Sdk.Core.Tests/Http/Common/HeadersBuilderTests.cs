// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

/// <summary>HTTP-14, HTTP-15, HTTP-19 and HTTP-3 (multimaps) for <see cref="Headers.Builder"/>.</summary>
[Trait("Category", "Unit")]
public class HeadersBuilderTests
{
    [Fact]
    public void Builder_add_appends_and_set_replaces()
    {
        var builder = new Headers.Builder().Add("X-T", "a").Add("x-t", "b");
        Assert.Equal((string[])["a", "b"], builder.Build().GetAll("X-T"));

        builder.Set("X-T", "c");
        Assert.Equal((string[])["c"], builder.Build().GetAll("X-T"));
    }

    [Fact]
    public void Builder_set_null_removes_the_header()
    {
        var headers = new Headers.Builder().Add("A", "1").Add("B", "2").Set("a", null).Build();

        Assert.False(headers.Contains("A"));
        Assert.Equal((string[])["B"], headers.Names);
        Assert.Equal(0, new Headers.Builder().Set("absent", null).Build().Count);
    }

    [Fact]
    public void Builder_set_keeps_position_and_first_casing_and_remove_then_add_moves_to_the_end()
    {
        var headers = new Headers.Builder().Add("X-First", "1").Add("Second", "2").Set("x-first", "9").Build();
        Assert.Equal((string[])["X-First", "Second"], headers.Names);

        var moved = new Headers.Builder().Add("A", "1").Add("B", "2").Remove("a").Add("A", "3").Build();
        Assert.Equal((string[])["B", "A"], moved.Names);
        Assert.Equal("3", moved.Get("a"));
    }

    [Fact]
    public void Builder_typed_overloads_match_the_string_ones()
    {
        var name = HttpHeaderName.Of("X-Typed");

        var typed = new Headers.Builder().Add(name, "a").Add(name, "b").Set(HttpHeaderName.Of("Other"), "o").Remove(HttpHeaderName.Of("Gone")).Build();
        var strings = new Headers.Builder().Add("X-Typed", "a").Add("X-Typed", "b").Set("Other", "o").Remove("Gone").Build();

        Assert.Equal(strings, typed);
        Assert.Equal((string[])["a", "b"], typed.GetAll(name));
        Assert.False(new Headers.Builder().Add(name, "a").Remove(name).Build().Contains(name));
        Assert.False(new Headers.Builder().Add(name, "a").Set(name, null).Build().Contains(name));
        Assert.Throws<ArgumentException>(() => new Headers.Builder().Add(name, "a\r\nb"));
    }

    [Fact]
    public void Build_is_a_deep_copy_and_later_edits_never_reach_the_result()
    {
        var builder = new Headers.Builder().Add("A", "1");
        var built = builder.Build();

        builder.Add("A", "2").Add("B", "3").Remove("a");
        var second = builder.Build();

        Assert.Equal((string[])["1"], built.GetAll("A"));
        Assert.False(built.Contains("B"));
        Assert.Equal((string[])["B"], second.Names);
    }

    [Fact]
    public void ToBuilder_is_a_fresh_builder()
    {
        var original = Headers.Empty.With("A", "1").With("A", "2").With("B", "3");

        var derived = original.ToBuilder().Add("A", "4").Remove("B").Build();

        Assert.Equal((string[])["1", "2", "4"], derived.GetAll("A"));
        Assert.False(derived.Contains("B"));
        Assert.Equal((string[])["1", "2"], original.GetAll("A"));
        Assert.True(original.Contains("B"));
        Assert.Equal(original, original.ToBuilder().Build());
    }

    [Fact]
    public void AddInbound_keeps_the_senders_casing_and_stays_lenient()
    {
        // HTTP-19. Mirrors, and does not replace, the Security The_inbound_path_* tests.
        var headers = new Headers.Builder()
            .AddInbound("X-Sender-Case", "café")
            .AddInbound("x-sender-case", "second")
            .Build();

        Assert.Equal((string[])["X-Sender-Case"], headers.Names);
        Assert.Equal((string[])["café", "second"], headers.GetAll("X-SENDER-CASE"));
        Assert.Throws<ArgumentException>(() => new Headers.Builder().AddInbound("X-A", "a\u0001b"));
        Assert.Throws<ArgumentException>(() => new Headers.Builder().AddInbound("X A", "v"));
    }
}
