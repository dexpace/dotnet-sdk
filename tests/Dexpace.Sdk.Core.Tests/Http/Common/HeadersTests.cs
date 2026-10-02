// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

[Trait("Category", "Unit")]
public class HeadersTests
{
    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var headers = Headers.Empty.Set("Content-Type", "application/json");
        Assert.Equal("application/json", headers.Get("content-type"));
        Assert.Equal("application/json", headers.Get("CONTENT-TYPE"));
        Assert.True(headers.Contains("Content-Type"));
    }

    [Fact]
    public void With_AppendsMultipleValues()
    {
        var headers = Headers.Empty
            .With("Accept", "text/plain")
            .With("Accept", "application/json");
        Assert.Equal((string[])["text/plain", "application/json"], headers.GetAll("accept"));
    }

    [Fact]
    public void Set_ReplacesExistingValues()
    {
        var headers = Headers.Empty
            .With("X-Test", "one")
            .With("X-Test", "two")
            .Set("X-Test", "final");
        Assert.Equal((string[])["final"], headers.GetAll("x-test"));
    }

    [Fact]
    public void Mutation_IsNonDestructive()
    {
        var original = Headers.Empty.Set("A", "1");
        var modified = original.With("A", "2");
        Assert.Single(original.GetAll("a"));
        Assert.Equal(2, modified.GetAll("a").Count);
    }

    [Fact]
    public void Without_RemovesName()
    {
        var headers = Headers.Empty.Set("A", "1").Without("a");
        Assert.False(headers.Contains("A"));
    }

    [Fact]
    public void Lookups_And_Removals_TrimTheName_AsWritesDo()
    {
        var headers = Headers.Empty.With(" X-A ", "v");

        Assert.True(headers.Contains(" X-A "));
        Assert.True(headers.Contains("\tx-a"));
        Assert.Equal("v", headers.Get(" x-a\t"));
        Assert.Equal((string[])["v"], headers.GetAll(" X-A "));
        Assert.False(headers.Without(" X-A ").Contains("X-A"));
        Assert.False(headers.ToBuilder().Remove("\tX-A ").Build().Contains("X-A"));
    }

    [Fact]
    public void Builder_BatchesEdits()
    {
        var headers = new Headers.Builder()
            .Add("A", "1")
            .Add("A", "2")
            .Set("B", "x")
            .Build();
        Assert.Equal((string[])["1", "2"], headers.GetAll("a"));
        Assert.Equal("x", headers.Get("b"));
    }

    // ---- Phase 2a: HTTP-13 .. HTTP-16, HTTP-21, HTTP-5 ----

    [Fact]
    public void A_name_added_under_one_casing_resolves_under_every_other()
    {
        var headers = Headers.Empty.With("X-Trace", "v");

        foreach (var spelling in new[] { "X-Trace", "x-trace", "X-TRACE", "x-TRACE" })
        {
            Assert.True(headers.Contains(spelling));
            Assert.Equal("v", headers.Get(spelling));
            Assert.Equal((string[])["v"], headers.GetAll(spelling));
        }

        Assert.False(Headers.Empty.With("key", "v").Contains("Key-2"));
    }

    [Fact]
    public void Folding_is_culture_invariant_under_tr_TR()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            var headers = Headers.Empty.With("TITLE", "v");

            Assert.Equal("v", headers.Get("title"));
            Assert.Equal("v", headers.Get("TITLE"));
            Assert.Equal("v", Headers.Empty.With("title", "v").Get("TITLE"));
            Assert.Null(Headers.Empty.With("key", "v").Get("KEY\u0130"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void A_non_ASCII_lookup_finds_nothing_and_does_not_throw()
    {
        // Breaking item 6: the Kelvin sign (U+212A) used to fold to 'k' under ToLowerInvariant.
        var headers = Headers.Empty.With("key", "v");

        Assert.False(headers.Contains("\u212Aey"));
        Assert.Null(headers.Get("\u212Aey"));
        Assert.Empty(headers.GetAll("\u212Aey"));
        Assert.Same(headers, headers.Without("\u212Aey"));
        Assert.False(headers.Contains("h\u00E9der"));
        Assert.Equal(headers, headers.ToBuilder().Remove("\u212Aey").Build());
    }

    [Fact]
    public void Equal_content_gives_equal_instances_and_hashes_and_casing_is_ignored()
    {
        var a = Headers.Empty.With("Accept", "text/plain").With("X-Trace", "1").With("X-Trace", "2");
        var b = Headers.Empty.With("accept", "text/plain").With("x-TRACE", "1").With("X-TRACE", "2");

        Assert.Equal(a, b);
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a.With("X-Trace", "3"));
        Assert.NotEqual(a, a.Without("Accept"));
        Assert.NotEqual(Headers.Empty.With("A", "1"), Headers.Empty.With("A", "2"));
        Assert.NotEqual(Headers.Empty.With("A", "1"), Headers.Empty.With("B", "1"));
        Assert.Equal(Headers.Empty, Headers.Empty.Without("nothing"));
        Assert.False(a.Equals((object?)null));
        Assert.False(a.Equals("Accept"));
    }

    [Fact]
    public void Differently_ordered_names_are_unequal()
    {
        var a = Headers.Empty.With("A", "1").With("B", "2");
        var b = Headers.Empty.With("B", "2").With("A", "1");

        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    [Fact]
    public void Operators_equal_and_not_equal_handle_null()
    {
        var headers = Headers.Empty.With("A", "1");

        Assert.False(headers == null);
        Assert.True(headers != null);
        Assert.False(null == headers);
        Assert.True((Headers?)null == null);
        Assert.True(headers == Headers.Empty.With("a", "1"));
        Assert.False(headers != Headers.Empty.With("a", "1"));
    }

    [Fact]
    public void Add_appends_and_Set_replaces_on_the_instance()
    {
        var added = Headers.Empty.With("X-T", "a").With("X-T", "b");
        Assert.Equal((string[])["a", "b"], added.GetAll("x-t"));

        var replaced = added.Set("X-T", "c");
        Assert.Equal((string[])["c"], replaced.GetAll("x-t"));
        Assert.Equal((string[])["a", "b"], added.GetAll("x-t"));
    }

    [Fact]
    public void Set_with_null_removes_the_header_and_Set_of_an_absent_name_returns_the_same_instance()
    {
        var headers = Headers.Empty.With("A", "1").With("B", "2");

        var removed = headers.Set("a", null);

        Assert.False(removed.Contains("A"));
        Assert.True(removed.Contains("B"));
        Assert.Same(headers, headers.Set("absent", null));
        Assert.Same(Headers.Empty, Headers.Empty.Set("absent", null));
        Assert.Equal(1, removed.Count);
    }

    [Fact]
    public void Set_with_null_still_validates_the_name()
    {
        Assert.Throws<ArgumentException>(() => Headers.Empty.Set("a b", null));
    }

    [Fact]
    public void Enumeration_and_Names_are_in_insertion_order_z_a_m()
    {
        var headers = Headers.Empty.With("z", "1").With("a", "2").With("m", "3");

        Assert.Equal((string[])["z", "a", "m"], headers.Names);
        Assert.Equal(["z", "a", "m"], headers.Select(pair => pair.Key));
        Assert.Equal(3, headers.Count);
    }

    [Fact]
    public void Set_on_an_existing_name_keeps_its_position_and_first_casing()
    {
        var headers = Headers.Empty.With("X-First", "1").With("Second", "2").With("Third", "3");

        var replaced = headers.Set("x-FIRST", "9");

        Assert.Equal((string[])["X-First", "Second", "Third"], replaced.Names);
        Assert.Equal("9", replaced.Get("X-First"));
    }

    [Fact]
    public void With_on_an_existing_name_keeps_its_position_and_first_casing()
    {
        var headers = Headers.Empty.With("X-First", "1").With("Second", "2").With("x-first", "3");

        Assert.Equal((string[])["X-First", "Second"], headers.Names);
        Assert.Equal((string[])["1", "3"], headers.GetAll("X-FIRST"));
    }

    [Fact]
    public void Without_then_With_moves_the_name_to_the_end()
    {
        var headers = Headers.Empty.With("A", "1").With("B", "2").With("C", "3");

        var moved = headers.Without("a").With("A", "1");

        Assert.Equal((string[])["B", "C", "A"], moved.Names);
    }

    [Fact]
    public void Enumeration_and_Names_carry_the_first_insertions_casing()
    {
        var headers = Headers.Empty.With("X-Trace-Id", "1").With("x-trace-id", "2").With("Accept", "x");

        Assert.Equal((string[])["X-Trace-Id", "Accept"], headers.Names);
        Assert.Equal(["X-Trace-Id", "Accept"], headers.Select(pair => pair.Key));
        Assert.Equal(["X-Trace-Id"], headers.ToBuilder().Build().Names.Take(1));
    }

    [Fact]
    public void A_name_added_through_a_string_is_visible_through_HttpHeaderName_and_back()
    {
        var name = HttpHeaderName.Of("X-Typed");
        var viaString = Headers.Empty.With("x-typed", "s");
        var viaTyped = Headers.Empty.With(name, "t");

        Assert.True(viaString.Contains(name));
        Assert.Equal("s", viaString.Get(name));
        Assert.Equal((string[])["s"], viaString.GetAll(name));
        Assert.True(viaTyped.Contains("X-TYPED"));
        Assert.Equal("t", viaTyped.Get("X-Typed"));
        Assert.Equal((string[])["t"], viaTyped.GetAll("x-typed"));

        Assert.Equal((string[])["s", "u"], viaString.With(name, "u").GetAll("X-Typed"));
        Assert.Equal((string[])["v"], viaString.Set(name, "v").GetAll("X-Typed"));
        Assert.False(viaString.Without(name).Contains("X-Typed"));
        Assert.False(viaString.Set(name, null).Contains("X-Typed"));
        Assert.Same(viaString, viaString.Without(HttpHeaderName.Of("Other")));
        Assert.Equal("X-Typed", viaTyped.Names[0]);
    }

    [Fact]
    public void Typed_overloads_validate_the_value_like_the_string_ones()
    {
        var name = HttpHeaderName.Of("X-Typed");

        Assert.Throws<ArgumentException>(() => Headers.Empty.With(name, "a\r\nb"));
        Assert.Throws<ArgumentException>(() => Headers.Empty.Set(name, "caf\u00E9"));
        Assert.Throws<ArgumentNullException>(() => Headers.Empty.With(name, null!));
        Assert.Throws<ArgumentNullException>(() => Headers.Empty.With((HttpHeaderName)null!, "v"));
    }

    [Fact]
    public void Names_is_a_read_only_list_snapshot()
    {
        // HTTP-5 pin: not a List<string>, an IList<string>.Add throws, and a later ToBuilder edit never reaches it.
        var headers = Headers.Empty.With("A", "1");
        var names = headers.Names;

        Assert.False(names is List<string>);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)names).Add("B"));
        headers.ToBuilder().Add("B", "2").Remove("A");
        Assert.Equal((string[])["A"], names);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)headers.GetAll("A")).Add("x"));
        Assert.False(headers.GetAll("A") is List<string>);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)headers.GetAll("absent")).Add("x"));
    }

    [Fact]
    public void ToString_lists_names_only()
    {
        var headers = Headers.Empty.With("Accept", "secret-1").With("X-Trace", "secret-2");

        Assert.Equal("Headers[Accept, X-Trace]", headers.ToString());
        Assert.Equal("Headers[]", Headers.Empty.ToString());
    }
}
