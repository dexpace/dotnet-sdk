// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.ServerSentEvents;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.ServerSentEvents;

/// <summary>The mapper's verdict: SSE-34 (P7b-16).</summary>
[Trait("Category", "Unit")]
public class SseMapResultTests
{
    [Fact]
    public void Value_wraps_a_value_with_kind_Value()
    {
        var result = SseMapResult.Value(42);

        Assert.Equal(SseMapResultKind.Value, result.Kind);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Skip_and_Done_convert_implicitly_to_any_generic_result()
    {
        SseMapResult<int> skip = SseMapResult.Skip;
        SseMapResult<int> done = SseMapResult.Done;
        SseMapResult<string> text = SseMapResult.Skip;
        SseMapResult<int?> nullable = SseMapResult.Done;

        Assert.Equal(SseMapResultKind.Skip, skip.Kind);
        Assert.Equal(SseMapResultKind.Done, done.Kind);
        Assert.Equal(SseMapResultKind.Skip, text.Kind);
        Assert.Equal(SseMapResultKind.Done, nullable.Kind);
    }

    [Fact]
    public void ToSseMapResult_is_the_named_alternative_to_the_conversion()
    {
        Assert.Equal(SseMapResultKind.Skip, SseMapResult.Skip.ToSseMapResult<int>().Kind);
        Assert.Equal(SseMapResultKind.Done, SseMapResult.Done.ToSseMapResult<string>().Kind);
        Assert.Throws<ArgumentException>(() => default(SseMapResult).ToSseMapResult<int>());
    }

    [Fact]
    public void Reading_Value_on_a_signal_throws_InvalidOperationException()
    {
        SseMapResult<int> skip = SseMapResult.Skip;
        SseMapResult<int> done = SseMapResult.Done;

        Assert.Throws<InvalidOperationException>(() => skip.Value);
        Assert.Throws<InvalidOperationException>(() => done.Value);
    }

    [Fact]
    public void The_default_result_has_Kind_zero_which_is_not_a_defined_kind()
    {
        // P7b-16: default must not mean Skip (silently dropping every event) or Done (silently ending the stream).
        var result = default(SseMapResult<int>);

        Assert.Equal((SseMapResultKind)0, result.Kind);
        Assert.False(Enum.IsDefined(result.Kind));
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Converting_the_default_signal_fails_loudly()
    {
        var signal = default(SseMapResult);

        Assert.Equal((SseMapResultKind)0, signal.Kind);
        Assert.Throws<ArgumentException>(() =>
        {
            SseMapResult<int> converted = signal;
            return converted;
        });
    }

    [Fact]
    public void Results_have_value_equality()
    {
        Assert.Equal(SseMapResult.Value(1), SseMapResult.Value(1));
        Assert.NotEqual(SseMapResult.Value(1), SseMapResult.Value(2));
        Assert.Equal(SseMapResult.Skip, SseMapResult.Skip);
        Assert.NotEqual(SseMapResult.Skip, SseMapResult.Done);
        Assert.Equal(SseMapResult.Value("a").GetHashCode(), SseMapResult.Value("a").GetHashCode());

        SseMapResult<int> skip = SseMapResult.Skip;
        SseMapResult<int> otherSkip = SseMapResult.Skip;
        SseMapResult<int> done = SseMapResult.Done;
        Assert.Equal(skip, otherSkip);
        Assert.NotEqual(skip, done);
    }

    [Fact]
    public void ToString_never_throws_even_for_a_result_without_a_value()
    {
        SseMapResult<int> skip = SseMapResult.Skip;

        Assert.Contains("Skip", skip.ToString(), StringComparison.Ordinal);
        Assert.Contains("Done", SseMapResult.Done.ToString(), StringComparison.Ordinal);
        Assert.Contains("42", SseMapResult.Value(42).ToString(), StringComparison.Ordinal);
        Assert.NotNull(default(SseMapResult<int>).ToString());
    }

    [Fact]
    public void The_enum_values_are_explicit()
    {
        Assert.Equal(1, (int)SseMapResultKind.Value);
        Assert.Equal(2, (int)SseMapResultKind.Skip);
        Assert.Equal(3, (int)SseMapResultKind.Done);
    }

    [Fact]
    public void A_mapper_can_be_unit_tested_without_the_stream()
    {
        Func<string?, string, SseMapResult<int>> mapper = (_, data) =>
        {
            if (data == "[DONE]")
            {
                return SseMapResult.Done;
            }

            if (data.Length == 0)
            {
                return SseMapResult.Skip;
            }

            return SseMapResult.Value(data.Length);
        };

        Assert.Equal(SseMapResult.Value(3), mapper(null, "abc"));
        Assert.Equal(SseMapResultKind.Skip, mapper("e", string.Empty).Kind);
        Assert.Equal(SseMapResultKind.Done, mapper(null, "[DONE]").Kind);
    }
}
