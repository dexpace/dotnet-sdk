// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-7 (P9-8, P9-29): the duration grammar; the cases port the sibling SDKs' tables and add V2's four divergences from <c>XmlConvert</c>.</summary>
[Trait("Category", "Unit")]
public sealed class DurationGrammarTests
{
    public static TheoryData<string, long> Iso => new()
    {
        { "PT5S", 5_000 },
        { "P1D", 86_400_000 },
        { "P1DT2H3M4S", 93_784_000 },
        { "PT0S", 0 },
        { "pt5s", 5_000 },
        { "PT0.5S", 500 },
        { "PT1M", 60_000 },
        { "PT1H30M", 5_400_000 },
    };

    public static TheoryData<string, long> Shorthand => new()
    {
        { "250ms", 250 },
        { "3s", 3_000 },
        { "2m", 120_000 },
        { "1h", 3_600_000 },
        { "1d", 86_400_000 },
        { "250MS", 250 },
        { "250 ms", 250 },
        { "1.5s", 1_500 },
        { "2S", 2_000 },
        { "4h", 14_400_000 },
        { "5d", 432_000_000 },
    };

    [Theory]
    [MemberData(nameof(Iso))]
    public void Accepts_the_ISO_8601_forms(string text, long milliseconds) => AssertParses(text, milliseconds);

    [Theory]
    [MemberData(nameof(Shorthand))]
    public void Accepts_the_shorthand_forms(string text, long milliseconds) => AssertParses(text, milliseconds);

    [Theory]
    [InlineData("1500", 1_500)]
    [InlineData("0", 0)]
    [InlineData("1000", 1_000)]
    public void Reads_a_bare_integer_as_milliseconds(string text, long milliseconds) => AssertParses(text, milliseconds);

    [Fact]
    public void PT0_0000001S_is_one_tick()
    {
        Assert.True(DurationGrammar.TryParse("PT0.0000001S", out var value, out _));
        Assert.Equal(TimeSpan.FromTicks(1), value);
    }

    [Theory]
    [InlineData("  PT5S  ", 5_000)]
    [InlineData(" 250ms ", 250)]
    [InlineData("\t3s\r\n", 3_000)]
    public void Tolerates_surrounding_whitespace(string text, long milliseconds) => AssertParses(text, milliseconds);

    [Theory]
    [InlineData("-PT5S")] // XmlConvert reads this as minus five seconds
    [InlineData("PT-5S")]
    [InlineData("P5M")] // XmlConvert reads this as 150 days
    [InlineData("P1Y")] // and this as 365 days
    [InlineData("P1W")]
    [InlineData("P1Y2M")]
    [InlineData("PT0.00000001S")] // XmlConvert truncates this to zero
    public void Rejects_each_of_V2s_divergences_from_XmlConvert(string text) => AssertRejected(text);

    [Theory]
    [InlineData("P")]
    [InlineData("PT")]
    [InlineData("P1X")]
    [InlineData("-3s")]
    [InlineData("-1")]
    [InlineData("5w")]
    [InlineData("soon")]
    [InlineData("5x")]
    [InlineData("-500ms")]
    [InlineData("P1DT")]
    [InlineData("PT1.5H")]
    [InlineData("PT1S2M")]
    [InlineData("1.5")]
    [InlineData("1 5s")]
    [InlineData("5 s junk")]
    [InlineData("+5s")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("P99999999D")]
    [InlineData("99999999999999999999ms")]
    [InlineData("none")]
    [InlineData("1,5s")]
    [InlineData("1.00000000s")]
    public void Rejects_malformed_input(string text) => AssertRejected(text);

    [Fact]
    public void The_three_grammars_land_on_one_scale()
    {
        for (var seconds = 0; seconds <= 100_000; seconds++)
        {
            Assert.True(DurationGrammar.TryParse("PT" + seconds + "S", out var iso, out _));
            Assert.True(DurationGrammar.TryParse(seconds + "s", out var shorthand, out _));
            Assert.True(DurationGrammar.TryParse((seconds * 1000L).ToString(System.Globalization.CultureInfo.InvariantCulture), out var bare, out _));
            Assert.Equal(iso, shorthand);
            Assert.Equal(iso, bare);
        }
    }

    [Fact]
    public void Never_throws_for_an_arbitrary_string_and_never_yields_a_negative_value()
    {
        var random = new Random(20261012);
        const string Alphabet = "PTpt0123456789.DHMSdhmsmn -+xé́٣";
        for (var i = 0; i < 2_000; i++)
        {
            var text = string.Create(random.Next(0, 12), random, static (span, r) =>
            {
                for (var k = 0; k < span.Length; k++)
                {
                    span[k] = Alphabet[r.Next(Alphabet.Length)];
                }
            });
            if (DurationGrammar.TryParse(text, out var value, out var rule))
            {
                Assert.True(value >= TimeSpan.Zero);
                Assert.Null(rule);
            }
        }
    }

    [Fact]
    public void A_failure_never_quotes_the_input()
    {
        Assert.False(DurationGrammar.TryParse("s3cr3t", out _, out var rule));
        Assert.DoesNotContain("s3cr3t", rule, StringComparison.Ordinal);
    }

    private static void AssertParses(string text, long milliseconds)
    {
        Assert.True(DurationGrammar.TryParse(text, out var value, out var rule), rule);
        Assert.Equal(TimeSpan.FromMilliseconds(milliseconds), value);
        Assert.Null(rule);
    }

    private static void AssertRejected(string text)
    {
        Assert.False(DurationGrammar.TryParse(text, out _, out var rule));
        Assert.False(string.IsNullOrEmpty(rule));
    }
}
