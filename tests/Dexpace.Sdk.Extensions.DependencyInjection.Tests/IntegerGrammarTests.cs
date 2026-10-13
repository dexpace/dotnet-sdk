// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-5 (P9-8, P9-26): base-10 integers; ports the sibling SDKs' tables, with <c>1_000</c> rejected where Ruby tolerates it.</summary>
[Trait("Category", "Unit")]
public sealed class IntegerGrammarTests
{
    [Theory]
    [InlineData("010", 10)]
    [InlineData("-5", -5)]
    [InlineData("+3", 3)]
    [InlineData("0", 0)]
    [InlineData("-0", 0)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void Parses_decimal_integers(string text, int expected)
    {
        Assert.True(ScalarGrammar.TryParseInt32(text, out var value, out var rule));
        Assert.Equal(expected, value);
        Assert.Null(rule);
    }

    [Theory]
    [InlineData("1_000")]
    [InlineData("0x10")]
    [InlineData("1e3")]
    [InlineData("1.5")]
    [InlineData("5x")]
    [InlineData("٣")]
    [InlineData("２")]
    [InlineData("2147483648")]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("1 000")]
    [InlineData(" 5 ")]
    public void Rejects_everything_else(string text)
    {
        Assert.False(ScalarGrammar.TryParseInt32(text, out _, out var rule));
        Assert.Contains("32-bit", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void The_long_target_has_the_long_range_and_the_rule_names_the_type()
    {
        Assert.True(ScalarGrammar.TryParseInt64("9223372036854775807", out var value, out _));
        Assert.Equal(long.MaxValue, value);
        Assert.False(ScalarGrammar.TryParseInt64("9223372036854775808", out _, out var rule));
        Assert.Contains("64-bit", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_never_quotes_the_input()
    {
        Assert.False(ScalarGrammar.TryParseInt32("s3cr3t", out _, out var rule));
        Assert.DoesNotContain("s3cr3t", rule, StringComparison.Ordinal);
    }
}
