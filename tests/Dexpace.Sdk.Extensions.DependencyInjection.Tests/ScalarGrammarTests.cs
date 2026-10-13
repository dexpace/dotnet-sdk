// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Http.SystemNet;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>P9-8: the decimal, enum and list grammars (V1's mis-conversions answered).</summary>
[Trait("Category", "Unit")]
public sealed class ScalarGrammarTests
{
    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("2", 2.0)]
    [InlineData("-1", -1.0)]
    [InlineData("1e2", 100.0)]
    public void Decimals_parse_in_invariant_notation(string text, double expected)
    {
        Assert.True(ScalarGrammar.TryParseDouble(text, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("1,5")]
    [InlineData("abc")]
    public void Non_finite_and_non_invariant_decimals_fail(string text) => Assert.False(ScalarGrammar.TryParseDouble(text, out _, out _));

    [Fact]
    public void The_decimal_parse_ignores_the_current_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.True(ScalarGrammar.TryParseDouble("1.5", out var value, out _));
            Assert.Equal(1.5, value);
            Assert.False(ScalarGrammar.TryParseDouble("1,5", out _, out _));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("Body")]
    [InlineData("body")]
    [InlineData(" BODY ")]
    public void An_enum_is_read_by_name_in_any_case_after_trimming(string text)
    {
        Assert.True(ScalarGrammar.TryParseEnum<HttpLogLevel>(ScalarGrammar.AsciiTrim(text), out var value, out _));
        Assert.Equal(HttpLogLevel.Body, value);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("Verbose")]
    public void A_number_an_undefined_value_or_an_unknown_name_fails_and_the_rule_lists_the_names(string text)
    {
        // V1: the binder admits "2" and the undefined 7; this reader does not (OBS-35).
        Assert.False(ScalarGrammar.TryParseEnum<HttpLogLevel>(text, out _, out var rule));
        Assert.Contains("None, Headers, Body", rule, StringComparison.Ordinal);
        Assert.DoesNotContain(text, rule!.Replace("None, Headers, Body", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_rule_holds_for_the_other_two_enums()
    {
        Assert.True(ScalarGrammar.TryParseEnum<HeaderDropLogging>("every", out var drop, out _));
        Assert.Equal(HeaderDropLogging.Every, drop);
        Assert.True(ScalarGrammar.TryParseEnum<ProxyType>("socks5", out var proxy, out _));
        Assert.Equal(ProxyType.Socks5, proxy);
        Assert.False(ScalarGrammar.TryParseEnum<HeaderDropLogging>("1", out _, out _));
        Assert.False(ScalarGrammar.TryParseEnum<ProxyType>("9", out _, out _));
    }

    [Fact]
    public void A_scalar_list_splits_on_commas_and_trims_each_item()
    {
        Assert.True(ScalarGrammar.TrySplitList("a, b ,c", out var items, out _));
        Assert.Equal(["a", "b", "c"], items);
    }

    [Fact]
    public void None_is_the_empty_list()
    {
        Assert.True(ScalarGrammar.TrySplitList("NONE", out var items, out _));
        Assert.Empty(items);
    }

    [Theory]
    [InlineData("a,")]
    [InlineData(",a")]
    [InlineData("a,,b")]
    public void A_stray_comma_fails_instead_of_dropping_an_item(string text) => Assert.False(ScalarGrammar.TrySplitList(text, out _, out _));

    [Fact]
    public void Children_form_a_list_in_the_configurations_order_and_both_forms_together_fail()
    {
        var children = Config.Read(("Retry:RetryableStatusCodes:0", "503"), ("Retry:RetryableStatusCodes:1", "429"));
        Assert.Empty(children.Failures);
        Assert.Equal(new HashSet<int> { 503, 429 }, children.Options.Retry.RetryableStatusCodes);

        var both = Config.Read(("Retry:RetryableStatusCodes", "503"), ("Retry:RetryableStatusCodes:0", "429"));
        Assert.Contains("not both", Assert.Single(Config.Failures(both)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_list_item_that_fails_names_the_index_and_never_the_value()
    {
        var configuration = Config.Read(("Retry:RetryableStatusCodes", "503, s3cr3t"));

        var failure = Assert.Single(Config.Failures(configuration));
        Assert.Contains("item 1", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_failure_is_value_free()
    {
        const string Marker = "s3cr3t-value";
        string?[] rules =
        [
            Rule(ScalarGrammar.TryParseInt32(Marker, out _, out var a), a),
            Rule(ScalarGrammar.TryParseInt64(Marker, out _, out var b), b),
            Rule(ScalarGrammar.TryParseBoolean(Marker, out _, out var c), c),
            Rule(ScalarGrammar.TryParseDouble(Marker, out _, out var d), d),
            Rule(ScalarGrammar.TryParseDuration(Marker, out _, out var e), e),
            Rule(ScalarGrammar.TryParseOptionalDuration(Marker, out _, out var f), f),
            Rule(ScalarGrammar.TryParseEnum<HttpLogLevel>(Marker, out _, out var g), g),
            Rule(ScalarGrammar.TryParseAbsoluteUri(Marker, out _, out var h), h),
        ];

        Assert.All(rules, rule => Assert.DoesNotContain(Marker, rule, StringComparison.Ordinal));
    }

    private static string? Rule(bool parsed, string? rule)
    {
        Assert.False(parsed);
        return rule;
    }
}
