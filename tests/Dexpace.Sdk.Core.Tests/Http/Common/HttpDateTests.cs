// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

// CFG-29, CFG-30, CFG-31, RETRY-15 (date clause); design 5a position C, P5a-11. Vectors: tests/vectors/config/http-date.json.
[Trait("Category", "Unit")]
public sealed class HttpDateTests
{
    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var vector in VectorFile.Load<DateCase>("config/http-date.json"))
        {
            data.Add(vector.Name);
        }

        return data;
    }

    private static DateCase Case(string name) =>
        VectorFile.Load<DateCase>("config/http-date.json").Single(c => c.Name == name);

    private static DateTimeOffset Instant(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    // ---- Format (CFG-29) ----

    [Fact]
    public void Format_renders_the_canonical_form_in_UTC()
    {
        Assert.Equal("Sun, 06 Nov 1994 08:49:37 GMT", HttpDate.Format(Instant("1994-11-06T08:49:37Z")));
    }

    [Fact]
    public void Format_zero_pads_a_single_digit_day()
    {
        Assert.Equal("Thu, 01 Jan 2026 00:00:00 GMT", HttpDate.Format(Instant("2026-01-01T00:00:00Z")));
    }

    [Fact]
    public void Format_converts_a_non_UTC_offset_to_the_UTC_instant()
    {
        var instant = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.FromHours(2));

        Assert.Equal("Thu, 01 Jan 2026 00:00:00 GMT", HttpDate.Format(instant));
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    [InlineData("tr-TR")]
    [InlineData("fa-IR")]
    public void Format_ignores_the_current_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            Assert.Equal("Sun, 06 Nov 1994 08:49:37 GMT", HttpDate.Format(Instant("1994-11-06T08:49:37Z")));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Format_renders_the_outermost_instants()
    {
        Assert.Equal("Mon, 01 Jan 0001 00:00:00 GMT", HttpDate.Format(DateTimeOffset.MinValue));
        Assert.Equal("Fri, 31 Dec 9999 23:59:59 GMT", HttpDate.Format(Instant("9999-12-31T23:59:59Z")));

        // An offset instant whose UTC value is out of range of DateTime at each boundary must not throw (UtcDateTime clamps).
        var minWithOffset = new DateTimeOffset(1, 1, 1, 14, 0, 0, TimeSpan.FromHours(14));
        var maxWithOffset = new DateTimeOffset(9999, 12, 31, 11, 59, 59, TimeSpan.FromHours(-12));
        Assert.NotNull(HttpDate.Format(minWithOffset));
        Assert.NotNull(HttpDate.Format(maxWithOffset));
    }

    // ---- Parse (CFG-30, CFG-31) ----

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void TryParse_matches_every_vector(string name)
    {
        var vector = Case(name);

        var ok = HttpDate.TryParse(vector.Input, out var instant);

        if (vector.Expected is null)
        {
            Assert.False(ok);
            Assert.Equal(default, instant);
        }
        else
        {
            Assert.True(ok);
            Assert.Equal(Instant(vector.Expected), instant);
            Assert.Equal(TimeSpan.Zero, instant.Offset);
        }
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Parse_agrees_with_TryParse_for_every_vector(string name)
    {
        var vector = Case(name);

        if (vector.Expected is null)
        {
            Assert.Throws<FormatException>(() => HttpDate.Parse(vector.Input));
        }
        else
        {
            Assert.Equal(Instant(vector.Expected), HttpDate.Parse(vector.Input));
        }
    }

    [Fact]
    public void Parse_error_never_echoes_the_input()
    {
        var error = Assert.Throws<FormatException>(() => HttpDate.Parse("secret-token-123"));

        Assert.DoesNotContain("secret-token-123", error.Message, StringComparison.Ordinal);
        Assert.Contains("Sun, 06 Nov 1994 08:49:37 GMT", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_null_with_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => HttpDate.Parse(null!));
    }

    [Fact]
    public void TryParse_accepts_null_returning_false()
    {
        Assert.False(HttpDate.TryParse(null, out var instant));
        Assert.Equal(default, instant);
    }

    [Fact]
    public void TryParse_never_throws_for_arbitrary_text()
    {
        // A seeded generator: a failure replays.
        var random = new Random(5140);
        string[] fragments = ["Mon", "Thu", ",", " ", ":", "Jan", "Dec", "GMT", "UTC", "+0000", "+00:00", "2026", "01", "31", "60", "\r", "\n", "\t", "-", "/", "9", "0"];
        for (var i = 0; i < 20_000; i++)
        {
            var length = random.Next(0, 41);
            var builder = new System.Text.StringBuilder();
            while (builder.Length < length)
            {
                builder.Append(fragments[random.Next(fragments.Length)]);
            }

            var text = builder.ToString();
            var error = Record.Exception(() => HttpDate.TryParse(text, out _));
            Assert.Null(error);
        }
    }

    [Fact]
    public void Format_then_TryParse_round_trips_every_second_precision_instant()
    {
        var random = new Random(5143);
        var span = (DateTimeOffset.MaxValue - DateTimeOffset.MinValue).Ticks / TimeSpan.TicksPerSecond;
        var samples = new List<DateTimeOffset>
        {
            new(1, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new(9999, 12, 31, 23, 59, 59, TimeSpan.Zero),
        };
        for (var i = 0; i < 20_000; i++)
        {
            samples.Add(new DateTimeOffset(DateTime.MinValue.AddTicks(random.NextInt64(0, span) * TimeSpan.TicksPerSecond), TimeSpan.Zero));
        }

        foreach (var sample in samples)
        {
            Assert.True(HttpDate.TryParse(HttpDate.Format(sample), out var back));
            Assert.Equal(sample, back);
        }
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("th-TH")]
    public void Parse_is_independent_of_the_current_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            foreach (var vector in VectorFile.Load<DateCase>("config/http-date.json").Where(c => c.Expected is not null))
            {
                Assert.Equal(Instant(vector.Expected!), HttpDate.Parse(vector.Input));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_single_digit_day_is_accepted_and_a_three_digit_day_is_not()
    {
        Assert.True(HttpDate.TryParse("Thu, 1 Jan 2026 00:00:10 GMT", out _));
        Assert.False(HttpDate.TryParse("Thu, 100 Jan 2026 00:00:10 GMT", out _));
    }

    private sealed record DateCase(string Name, string Input, string? Expected, string? Note);
}
