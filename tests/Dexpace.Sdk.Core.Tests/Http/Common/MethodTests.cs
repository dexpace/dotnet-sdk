// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

/// <summary>HTTP-9 and HTTP-1 (Method).</summary>
[Trait("Category", "Unit")]
public class MethodTests
{
    public static TheoryData<string> NonTokens => new()
    {
        "GET\r\nX: y",
        "FOO BAR",
        "FOO\tBAR",
        string.Empty,
        "   ",
        "\r\n",
        "GET/1",
        "é",
        "A(B",
    };

    [Fact]
    public void Well_known_tokens_equal_their_upper_case_names_and_resolve_to_cached_instances()
    {
        Assert.Same(Method.Get, Method.Of("GET"));
        Assert.Same(Method.Head, Method.Of("HEAD"));
        Assert.Same(Method.Post, Method.Of("POST"));
        Assert.Same(Method.Put, Method.Of("PUT"));
        Assert.Same(Method.Patch, Method.Of("PATCH"));
        Assert.Same(Method.Delete, Method.Of("DELETE"));
        Assert.Same(Method.Options, Method.Of("OPTIONS"));
        Assert.Same(Method.Trace, Method.Of("TRACE"));
        Assert.Same(Method.Connect, Method.Of("CONNECT"));
        Assert.Equal("GET", Method.Get.Name);
        Assert.Equal("CONNECT", Method.Connect.Name);
    }

    [Theory]
    [InlineData("get", "GET")]
    [InlineData("  Post ", "POST")]
    [InlineData("\tdElEtE\t", "DELETE")]
    public void Of_folds_case_for_the_nine_well_known_verbs_and_keeps_other_tokens_verbatim_known(string input, string expected)
    {
        // RULED 2026-09-30: the fold stays (get resolves to Method.Get, as design section 4.3 states).
        Assert.Equal(expected, Method.Of(input).Name);
        Assert.Same(Method.Of(expected), Method.Of(input));
    }

    [Theory]
    [InlineData("Foo")]
    [InlineData("PROPFIND")]
    [InlineData("m-search")]
    public void Of_folds_case_for_the_nine_well_known_verbs_and_keeps_other_tokens_verbatim_other(string token)
    {
        Assert.Equal(token, Method.Of(token).Name);
        Assert.Equal(token, Method.Of(" " + token + "\t").Name);
    }

    [Fact]
    public void Of_trims_SP_and_HTAB_only()
    {
        Assert.Equal("PROPFIND", Method.Of(" \t PROPFIND \t ").Name);
        Assert.Throws<ArgumentException>(() => Method.Of("\nGET"));
        Assert.Throws<ArgumentException>(() => Method.Of("GET "));
    }

    [Theory]
    [MemberData(nameof(NonTokens))]
    public void Of_rejects_a_non_token_without_echoing_it(string input)
    {
        var exception = Assert.Throws<ArgumentException>(() => Method.Of(input));

        Assert.DoesNotContain('\r', exception.Message);
        Assert.DoesNotContain('\n', exception.Message);
        if (input.Trim(' ', '\t').Length > 0)
        {
            Assert.DoesNotContain(input, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Of_rejection_names_the_code_point_and_not_the_text()
    {
        var exception = Assert.Throws<ArgumentException>(() => Method.Of("FOO BAR"));

        Assert.Contains("U+0020", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("FOO", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("BAR", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Of_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => Method.Of(null!));
    }

    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("TRACE", true)]
    [InlineData("CONNECT", true)]
    [InlineData("POST", false)]
    [InlineData("PUT", false)]
    [InlineData("PATCH", false)]
    [InlineData("DELETE", false)]
    [InlineData("OPTIONS", false)]
    [InlineData("PROPFIND", false)]
    public void ForbidsBody_is_true_for_exactly_GET_HEAD_TRACE_CONNECT(string token, bool forbids)
    {
        Assert.Equal(forbids, Method.Of(token).ForbidsBody);
    }

    [Fact]
    public void Method_has_no_public_bool_property()
    {
        // HTTP-9: IsSafe and IsIdempotent are gone from the public surface.
        var publicBools = typeof(Method)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(bool))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(publicBools);
        Assert.Null(typeof(Method).GetProperty("IsSafe", BindingFlags.Public | BindingFlags.Instance));
        Assert.Null(typeof(Method).GetProperty("IsIdempotent", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Method_is_a_reference_type_and_never_default()
    {
        Assert.True(typeof(Method).IsClass);
        Assert.True(typeof(Method).IsSealed);
        Assert.Null(default(Method));
    }

    [Fact]
    public void Method_SafetyAndIdempotency()
    {
        // Rewritten against the internal set (HTTP-9): the public IsSafe is gone, TRACE is no longer idempotent.
        Assert.True(Method.Get.IsIdempotent);
        Assert.True(Method.Head.IsIdempotent);
        Assert.True(Method.Options.IsIdempotent);
        Assert.True(Method.Put.IsIdempotent);
        Assert.True(Method.Delete.IsIdempotent);
        Assert.False(Method.Post.IsIdempotent);
        Assert.False(Method.Patch.IsIdempotent);
        Assert.False(Method.Trace.IsIdempotent);
        Assert.False(Method.Connect.IsIdempotent);
    }

    [Fact]
    public void Equality_is_by_name_and_ToString_is_the_name()
    {
        Assert.Equal(Method.Of("PROPFIND"), Method.Of("PROPFIND"));
        Assert.True(Method.Of("PROPFIND") == Method.Of("PROPFIND"));
        Assert.Equal(Method.Of("PROPFIND").GetHashCode(), Method.Of("PROPFIND").GetHashCode());
        Assert.NotEqual(Method.Get, Method.Post);
        Assert.Equal("PROPFIND", Method.Of("PROPFIND").ToString());
        Assert.Equal("GET", Method.Get.ToString());
    }
}
