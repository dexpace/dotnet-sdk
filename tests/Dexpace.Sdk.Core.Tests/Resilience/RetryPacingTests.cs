// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Resilience;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Resilience;

/// <summary>The pacing-hint parser (RETRY-15 to RETRY-22, RECOV-22 to RECOV-25, RECOV-29).</summary>
[Trait("Category", "Unit")]
public sealed class RetryPacingTests
{
    private static readonly DateTimeOffset s_now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_year = TimeSpan.FromDays(365);

    public sealed record PacingCase(
        string Name,
        Dictionary<string, string> Headers,
        double U,
        long NowOffsetSeconds,
        long? ExpectedTicks);

    private static Headers HeadersOf(params (string Name, string Value)[] entries)
    {
        var builder = new Headers.Builder();
        foreach (var (name, value) in entries)
        {
            builder.AddInbound(name, value);
        }

        return builder.Build();
    }

    private static TimeSpan? Hint(string name, string value, double u = 0.0) =>
        RetryPacing.TryGetHint(HeadersOf((name, value)), s_now, () => u);

    [Fact]
    public void Matches_every_vector()
    {
        var cases = VectorFile.Load<PacingCase>("retry/pacing.json");
        Assert.NotEmpty(cases);

        foreach (var c in cases)
        {
            var headers = HeadersOf([.. c.Headers.Select(kv => (kv.Key, kv.Value))]);
            var actual = RetryPacing.TryGetHint(headers, s_now.AddSeconds(c.NowOffsetSeconds), () => c.U);

            Assert.True(c.ExpectedTicks == actual?.Ticks, $"{c.Name}: expected {c.ExpectedTicks}, got {actual?.Ticks}.");
        }
    }

    [Fact]
    public void Retry_After_delta_seconds_integer_and_fractional()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), Hint("Retry-After", "5"));
        Assert.Equal(TimeSpan.FromSeconds(1.5), Hint("Retry-After", "1.5"));
        Assert.Equal(TimeSpan.Zero, Hint("Retry-After", "0"));
        Assert.Equal(TimeSpan.FromTicks(1), Hint("Retry-After", "0.0000001"));
        Assert.Equal(TimeSpan.FromTicks(1), Hint("Retry-After", "0.00000019"));
        Assert.Equal(TimeSpan.FromSeconds(7), Hint("Retry-After", "\t 7 \t"));
        Assert.Equal(TimeSpan.FromSeconds(3), RetryPacing.TryGetHint(HeadersOf(("Retry-After", "3"), ("Retry-After", "9")), s_now, () => 0));
    }

    [Theory]
    [InlineData("+5")]
    [InlineData("-5")]
    [InlineData("5 5")]
    [InlineData("30d")]
    [InlineData("0x1p4")]
    [InlineData("1e3")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData(".5")]
    [InlineData("5.")]
    [InlineData("1,5")]
    [InlineData("٣")]
    [InlineData("")]
    public void The_strict_grammar_rejects_what_the_BCL_would_accept(string value)
    {
        Assert.Null(Hint("Retry-After", value));
    }

    [Fact]
    public void A_thirty_digit_numeral_saturates_to_365_days()
    {
        Assert.Equal(s_year, Hint("Retry-After", new string('9', 30)));
        Assert.Equal(s_year, Hint("Retry-After", int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(s_year, Hint("Retry-After", "86400000"));
        Assert.Equal(s_year, Hint("Retry-After", "31536000"));
        Assert.Equal(TimeSpan.FromDays(60), Hint("Retry-After", "5184000"));
        Assert.Equal(s_year, Hint("retry-after-ms", new string('9', 30)));
        Assert.Equal(s_year, Hint("X-RateLimit-Reset", new string('9', 30)));
    }

    [Fact]
    public void Retry_After_http_date_uses_HttpDate_and_a_past_date_is_zero()
    {
        Assert.Equal(TimeSpan.Zero, RetryPacing.TryGetHint(HeadersOf(("Retry-After", "Thu, 01 Jan 2026 00:00:00 GMT")), s_now.AddSeconds(5), () => 0));
        Assert.Equal(TimeSpan.FromSeconds(10), Hint("Retry-After", "Thu, 01 Jan 2026 00:00:10 GMT"));
        Assert.Null(Hint("Retry-After", "Thursday, 01-Jan-26 00:00:10 GMT"));
        Assert.Null(Hint("Retry-After", "Thu Jan  1 00:00:10 2026"));
    }

    [Fact]
    public void Retry_after_ms_and_x_ms_retry_after_ms_are_digits_only_milliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(250), Hint("retry-after-ms", "250"));
        Assert.Equal(TimeSpan.FromMilliseconds(250), Hint("x-ms-retry-after-ms", "250"));
        Assert.Null(Hint("retry-after-ms", "25.5"));
        Assert.Equal(
            TimeSpan.FromMilliseconds(5),
            RetryPacing.TryGetHint(HeadersOf(("Retry-After", "nonsense"), ("retry-after-ms", "5"), ("x-ms-retry-after-ms", "9")), s_now, () => 0));
        Assert.Equal(
            TimeSpan.FromMilliseconds(9),
            RetryPacing.TryGetHint(HeadersOf(("retry-after-ms", "x"), ("x-ms-retry-after-ms", "9")), s_now, () => 0));
    }

    [Fact]
    public void X_RateLimit_Reset_is_an_epoch_with_positive_jitter()
    {
        var reset = (s_now.ToUnixTimeSeconds() + 10).ToString(System.Globalization.CultureInfo.InvariantCulture);

        foreach (var u in new[] { 0.0, 0.5, 0.999 })
        {
            var hint = Hint("X-RateLimit-Reset", reset, u)!.Value.TotalSeconds;
            Assert.InRange(hint, 10.0, 12.0);
            Assert.True(Math.Abs(hint - (10 * (1 + (0.2 * u)))) < 1e-6);
        }

        var consumed = 0;
        var past = (s_now.ToUnixTimeSeconds() - 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var zero = RetryPacing.TryGetHint(HeadersOf(("X-RateLimit-Reset", past)), s_now, () =>
        {
            consumed++;
            return 1.0;
        });
        Assert.Equal(TimeSpan.Zero, zero);
        Assert.Equal(0, consumed);

        Assert.Null(Hint("X-RateLimit-Reset", "+" + reset));
        Assert.Null(Hint("X-RateLimit-Reset", reset + ".5"));
    }

    [Fact]
    public void No_header_or_only_malformed_headers_yield_null()
    {
        Assert.Null(RetryPacing.TryGetHint(HeadersOf(), s_now, () => 0));
        Assert.Null(RetryPacing.TryGetHint(HeadersOf(("Retry-After", "soon"), ("retry-after-ms", "-1"), ("X-RateLimit-Reset", "tomorrow")), s_now, () => 0));
    }

    [Fact]
    public void The_parser_is_total()
    {
        var random = new Random(20261008);
        const string Alphabet = "0123456789.+-eE xX:,\t٣½ GMTMonJan\0\u0001\u007f";
        for (var i = 0; i < 10_000; i++)
        {
            var length = i % 50 == 0 ? 10_000 : random.Next(0, 40);
            var chars = new char[length];
            for (var j = 0; j < length; j++)
            {
                chars[j] = Alphabet[random.Next(Alphabet.Length)];
            }

            var value = new string(chars);
            foreach (var name in new[] { "Retry-After", "retry-after-ms", "x-ms-retry-after-ms", "X-RateLimit-Reset" })
            {
                Headers headers;
                try
                {
                    headers = HeadersOf((name, value));
                }
                catch (ArgumentException)
                {
                    // The header model refuses controls at construction; the parser is only reachable with what it admits.
                    continue;
                }

                var hint = RetryPacing.TryGetHint(headers, s_now, () => 0.5);
                if (hint is { } h)
                {
                    Assert.InRange(h, TimeSpan.Zero, s_year);
                }
            }
        }
    }

    [Fact]
    public void No_result_exceeds_365_days_in_ticks()
    {
        foreach (var value in new[] { "99999999999999999999", "31536001", "31536000.9999999" })
        {
            Assert.True(Hint("Retry-After", value)!.Value.Ticks <= RetryBackoff.MaxClampTicks);
        }
    }

    [Fact]
    public void The_hint_is_not_given_symmetric_jitter()
    {
        foreach (var u in new[] { 0.0, 0.3, 0.9, 1.0 })
        {
            Assert.Equal(TimeSpan.FromSeconds(7), Hint("Retry-After", "7", u));
        }
    }

    [Fact]
    public void Resilience_contains_no_double_Parse()
    {
        var directory = FindResilienceDirectory();
        var files = Directory.GetFiles(directory, "*.cs");
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var banned in new[] { "double.Parse", "double.TryParse", "decimal.Parse", "Convert.ToDouble" })
            {
                Assert.DoesNotContain(banned, text, StringComparison.Ordinal);
            }
        }
    }

    private static string FindResilienceDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Dexpace.Sdk.Core", "Resilience");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("The repository's src/Dexpace.Sdk.Core/Resilience directory was not found.");
    }
}
