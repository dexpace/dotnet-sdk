// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-6 (P9-27, P9-29): only <c>true</c> and <c>false</c> in any case, after the reader's ASCII trim; the eight tokens of the Ruby table fail.</summary>
[Trait("Category", "Unit")]
public sealed class BooleanGrammarTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("False", false)]
    [InlineData("FaLsE", false)]
    public void Accepts_the_two_tokens_in_any_case(string text, bool expected)
    {
        Assert.True(ScalarGrammar.TryParseBoolean(text, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("on")]
    [InlineData("off")]
    [InlineData("t")]
    [InlineData("f")]
    [InlineData("truee")]
    [InlineData("tru")]
    [InlineData("")]
    public void Rejects_everything_else(string text)
    {
        Assert.False(ScalarGrammar.TryParseBoolean(text, out _, out var rule));
        Assert.Contains("only true or false", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void A_boolean_with_surrounding_whitespace_is_read_after_the_reader_trims_it()
    {
        // Ruby does not trim booleans; P9-27 does, in the reader (recorded as a section 11 resolution, P9-29).
        var configuration = Config.Read(("Retry:HonorRetryAfter", " false "));

        Assert.Empty(configuration.Failures);
        Assert.False(configuration.Options.Retry.HonorRetryAfter);
    }
}
