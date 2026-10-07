// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-7: truncation of logged strings.</summary>
[Trait("Category", "Unit")]
public sealed class LogTextTests
{
    private const string Suffix = "…[truncated]";

    [Fact]
    public void A_value_of_exactly_8192_chars_is_unchanged()
    {
        var value = new string('a', 8192);

        Assert.Same(value, LogText.Truncate(value));
    }

    [Fact]
    public void A_value_of_8193_chars_is_cut_to_8192_plus_the_suffix()
    {
        var truncated = LogText.Truncate(new string('a', 8193));

        Assert.Equal(new string('a', 8192) + Suffix, truncated);
    }

    [Fact]
    public void A_cut_never_splits_a_surrogate_pair()
    {
        // The high surrogate lands at index 8191, its low half at 8192: the pair is dropped whole.
        var value = new string('a', 8191) + "😀" + new string('b', 10);

        var truncated = LogText.Truncate(value)!;

        Assert.Equal(new string('a', 8191) + Suffix, truncated);
        Assert.DoesNotContain('\ud83d', truncated);
    }

    [Fact]
    public void Null_and_empty_pass_through()
    {
        Assert.Null(LogText.Truncate(null));
        Assert.Equal(string.Empty, LogText.Truncate(string.Empty));
    }
}
