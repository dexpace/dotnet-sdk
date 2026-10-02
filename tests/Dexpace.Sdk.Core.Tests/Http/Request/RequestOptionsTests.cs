// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>HTTP-34, HTTP-35, HTTP-3 (this type): the per-call options record.</summary>
[Trait("Category", "Unit")]
public class RequestOptionsTests
{
    public static TheoryData<TimeSpan> NonPositiveTimeouts =>
        [TimeSpan.Zero, TimeSpan.FromTicks(-1), Timeout.InfiniteTimeSpan];

    [Fact]
    public void Empty_overrides_nothing_and_has_no_tags()
    {
        Assert.Null(RequestOptions.Empty.Timeout);
        Assert.Null(RequestOptions.Empty.MaxRetries);
        Assert.NotNull(RequestOptions.Empty.Tags);
        Assert.Empty(RequestOptions.Empty.Tags);
    }

    [Fact]
    public void A_new_instance_equals_Empty()
    {
        Assert.Equal(RequestOptions.Empty, new RequestOptions());
        Assert.True(new RequestOptions() == RequestOptions.Empty);
    }

    [Theory]
    [MemberData(nameof(NonPositiveTimeouts))]
    public void Timeout_that_is_not_positive_is_rejected(TimeSpan timeout)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestOptions { Timeout = timeout });
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestOptions.Empty with { Timeout = timeout });
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    [InlineData(null)]
    public void A_positive_or_null_timeout_is_accepted(long? ticks)
    {
        // Pins that the upper bound is 8b's TRANSPORT-5, not a model rule.
        TimeSpan? timeout = ticks is null ? null : TimeSpan.FromTicks(ticks.Value);
        Assert.Equal(timeout, new RequestOptions { Timeout = timeout }.Timeout);
        Assert.Equal(timeout, (RequestOptions.Empty with { Timeout = timeout }).Timeout);
        Assert.Equal(TimeSpan.MaxValue, new RequestOptions { Timeout = TimeSpan.MaxValue }.Timeout);
    }

    [Fact]
    public void MaxRetries_below_zero_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestOptions { MaxRetries = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestOptions.Empty with { MaxRetries = -1 });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(null)]
    [InlineData(5)]
    public void MaxRetries_of_zero_or_null_is_accepted(int? retries)
    {
        Assert.Equal(retries, new RequestOptions { MaxRetries = retries }.MaxRetries);
        Assert.Equal(retries, (RequestOptions.Empty with { MaxRetries = retries }).MaxRetries);
    }

    [Fact]
    public void Tags_default_is_never_null()
    {
        Assert.NotNull(new RequestOptions().Tags);
        Assert.Empty(new RequestOptions().Tags);
    }

    [Fact]
    public void Assigning_null_to_Tags_throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RequestOptions { Tags = null! });
        Assert.Throws<ArgumentNullException>(() => RequestOptions.Empty with { Tags = null! });
    }

    [Fact]
    public void Tags_built_from_an_ImmutableDictionary_builder_are_unaffected_by_later_builder_edits()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();
        builder["a"] = "1";
        var options = new RequestOptions { Tags = builder.ToImmutable() };
        builder["b"] = "2";
        builder["a"] = "changed";

        Assert.Single(options.Tags);
        Assert.Equal("1", options.Tags["a"]);
    }

    [Fact]
    public void Tag_keys_are_ordinal()
    {
        var insensitive = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.OrdinalIgnoreCase)
            .Add("a", "lower");
        Assert.True(insensitive.ContainsKey("A"));

        var options = new RequestOptions { Tags = insensitive };

        Assert.True(options.Tags.ContainsKey("a"));
        Assert.False(options.Tags.ContainsKey("A"));
        Assert.Same(StringComparer.Ordinal, options.Tags.KeyComparer);
    }

    [Fact]
    public void Equality_compares_tags_by_content_and_hashes_agree()
    {
        var first = new RequestOptions { Tags = ImmutableDictionary<string, string>.Empty.Add("a", "1").Add("b", "2") };
        var second = new RequestOptions { Tags = ImmutableDictionary<string, string>.Empty.Add("b", "2").Add("a", "1") };

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Different_timeouts_retries_or_tags_are_unequal()
    {
        var baseline = new RequestOptions
        {
            Timeout = TimeSpan.FromSeconds(1),
            MaxRetries = 1,
            Tags = ImmutableDictionary<string, string>.Empty.Add("a", "1"),
        };

        Assert.NotEqual(baseline, baseline with { Timeout = TimeSpan.FromSeconds(2) });
        Assert.NotEqual(baseline, baseline with { MaxRetries = 2 });
        Assert.NotEqual(baseline, baseline with { Tags = ImmutableDictionary<string, string>.Empty.Add("a", "2") });
        Assert.NotEqual(baseline, baseline with { Tags = ImmutableDictionary<string, string>.Empty.Add("b", "1") });
        Assert.NotEqual(baseline, baseline with { Tags = ImmutableDictionary<string, string>.Empty });
    }

    [Fact]
    public void WithTag_adds_and_replaces_without_mutating_the_original()
    {
        var original = RequestOptions.Empty.WithTag("a", "1");
        var added = original.WithTag("b", "2");
        var replaced = original.WithTag("a", "9");

        Assert.Single(original.Tags);
        Assert.Equal("1", original.Tags["a"]);
        Assert.Equal(2, added.Tags.Count);
        Assert.Equal("9", replaced.Tags["a"]);
        Assert.Empty(RequestOptions.Empty.Tags);
    }

    [Fact]
    public void WithTag_validates_the_key_and_value_are_not_null()
    {
        Assert.Throws<ArgumentNullException>(() => RequestOptions.Empty.WithTag(null!, "v"));
        Assert.Throws<ArgumentNullException>(() => RequestOptions.Empty.WithTag("k", null!));
    }
}
