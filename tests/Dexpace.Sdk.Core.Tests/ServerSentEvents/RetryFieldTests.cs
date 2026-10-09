// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The <c>retry</c> value grammar (SSE-11, P7b-6).</summary>
[Trait("Category", "Unit")]
public class RetryFieldTests
{
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("5000", 5000L)]
    [InlineData("0005", 5L)]
    [InlineData("2147483647", 2147483647L)]
    public void Accepts_ascii_digits(string value, long expectedMilliseconds)
    {
        Assert.True(RetryField.TryParse(value, out var retry));
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), retry);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" 5")]
    [InlineData("5 ")]
    [InlineData("+5")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("5e3")]
    [InlineData("0x10")]
    [InlineData("٣")] // Arabic-Indic three: char.IsDigit admits it, SSE-11 does not.
    [InlineData("５")] // Fullwidth five.
    public void Rejects_non_digits(string value)
    {
        Assert.False(RetryField.TryParse(value, out var retry));
        Assert.Equal(TimeSpan.Zero, retry);
    }

    [Fact]
    public void Rejects_over_cap_without_overflow()
    {
        var clock = Stopwatch.StartNew();

        Assert.False(RetryField.TryParse("2147483648", out _));
        Assert.False(RetryField.TryParse(new string('9', 1000), out _));
        Assert.False(RetryField.TryParse("99999999999999999999", out _));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Leading_zeros_do_not_count_toward_the_cap()
    {
        Assert.True(RetryField.TryParse(new string('0', 50) + "7", out var retry));
        Assert.Equal(TimeSpan.FromMilliseconds(7), retry);
    }
}
