// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-8, CFG-9, PIPE-17 (immutability half): the options are sealed records with init accessors, derived with `with`.
[Trait("Category", "Unit")]
public class OptionsImmutabilityTests
{
    [Fact]
    public void With_derives_a_copy_and_leaves_the_source_unchanged()
    {
        var options = new DexpaceClientOptions();
        var derived = options with { Retry = options.Retry with { MaxRetryAttempts = 5 } };

        Assert.Equal(3, options.Retry.MaxRetryAttempts);
        Assert.Equal(5, derived.Retry.MaxRetryAttempts);
        Assert.NotSame(options.Retry, derived.Retry);
        Assert.Same(options.Redirect, derived.Redirect);
    }

    [Fact]
    public void Two_equal_options_are_equal_and_hash_alike()
    {
        var a = new DexpaceClientOptions { UserAgent = "x/1", Retry = new RetryOptions { MaxRetryAttempts = 1 } };
        var b = new DexpaceClientOptions { UserAgent = "x/1", Retry = new RetryOptions { MaxRetryAttempts = 1 } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a == b);
    }

    [Fact]
    public void A_differing_nested_member_makes_options_unequal()
    {
        var a = new DexpaceClientOptions();
        var b = a with { Redirect = new RedirectOptions { MaxRedirects = 1 } };

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Theory]
    [InlineData(typeof(DexpaceClientOptions))]
    [InlineData(typeof(RetryOptions))]
    [InlineData(typeof(RedirectOptions))]
    public void Records_are_sealed_and_expose_init_only_properties(Type type)
    {
        Assert.True(type.IsSealed);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.Name == "EqualityContract")
            {
                continue;
            }

            var setter = property.SetMethod;
            Assert.NotNull(setter);
            Assert.Contains(setter.ReturnParameter.GetRequiredCustomModifiers(), m => m == typeof(IsExternalInit));
        }
    }

    [Fact]
    public void Nested_options_cannot_be_null()
    {
        var retry = Assert.Throws<ArgumentNullException>(() => new DexpaceClientOptions { Retry = null! });
        var redirect = Assert.Throws<ArgumentNullException>(() => new DexpaceClientOptions { Redirect = null! });

        Assert.Equal("value", retry.ParamName);
        Assert.Equal("value", redirect.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void UserAgent_accepts_blank(string blank)
    {
        Assert.Equal(blank, new DexpaceClientOptions { UserAgent = blank }.UserAgent);
    }

    [Fact]
    public void UserAgent_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => new DexpaceClientOptions { UserAgent = null! });
    }
}
