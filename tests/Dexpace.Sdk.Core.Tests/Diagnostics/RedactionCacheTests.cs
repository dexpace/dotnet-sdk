// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-12, P5b-10, XCUT-11: the per-call redactor and renderer holder.</summary>
[Trait("Category", "Unit")]
public sealed class RedactionCacheTests
{
    [Fact]
    public void The_same_options_instance_returns_the_same_redactor_and_renderer()
    {
        var cache = new RedactionCache();
        var options = new HttpLoggingOptions();

        var first = cache.Get(options);
        var second = cache.Get(options);

        Assert.Same(first, second);
        Assert.Same(first.Redactor, second.Redactor);
        Assert.Same(first.Renderer, second.Renderer);
    }

    [Fact]
    public void A_different_instance_rebuilds()
    {
        var cache = new RedactionCache();
        var first = cache.Get(new HttpLoggingOptions());

        var second = cache.Get(new HttpLoggingOptions());

        Assert.NotSame(first.Redactor, second.Redactor);
    }

    [Fact]
    public void A_with_copy_that_changes_AllowedQueryParameters_gets_a_redactor_that_honours_it()
    {
        var cache = new RedactionCache();
        var original = new HttpLoggingOptions();
        var copy = original with { AllowedQueryParameters = ["keep"] };

        var fromOriginal = cache.Get(original).Redactor.Redact("https://h/p?keep=1");
        var fromCopy = cache.Get(copy).Redactor.Redact("https://h/p?keep=1");

        Assert.Equal("https://h/p?keep=***", fromOriginal);
        Assert.Equal("https://h/p?keep=1", fromCopy);
    }

    [Fact]
    public void A_with_copy_that_changes_nothing_is_a_different_key_but_equivalent_output()
    {
        var cache = new RedactionCache();
        var original = new HttpLoggingOptions();
        var copy = original with { };

        var first = cache.Get(original);
        var second = cache.Get(copy);

        Assert.NotSame(first, second);
        Assert.Equal(first.Redactor.Redact("https://h/p?a=1&api-version=2"), second.Redactor.Redact("https://h/p?a=1&api-version=2"));
    }

    [Fact]
    public void Concurrent_callers_never_observe_a_torn_triple()
    {
        var cache = new RedactionCache();
        var a = new HttpLoggingOptions { AllowedQueryParameters = ["a"] };
        var b = new HttpLoggingOptions { AllowedQueryParameters = ["b"] };

        Parallel.For(0, 2000, i =>
        {
            var options = (i & 1) == 0 ? a : b;
            var entry = cache.Get(options);
            Assert.Same(options, entry.Key);
            var expectedKeep = (i & 1) == 0 ? "a" : "b";
            Assert.Equal($"https://h/?{expectedKeep}=1", entry.Redactor.Redact($"https://h/?{expectedKeep}=1"));
        });
    }

    [Fact]
    public void HttpLoggingOptions_has_no_derived_instance_field()
    {
        var fields = typeof(HttpLoggingOptions).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.DoesNotContain(fields, f => f.FieldType == typeof(UrlRedactor) || f.FieldType == typeof(HeaderLogRenderer));
    }
}
