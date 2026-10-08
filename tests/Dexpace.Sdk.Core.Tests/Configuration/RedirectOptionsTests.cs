// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

/// <summary>The redirect options (REDIR-3, REDIR-5, REDIR-17, REDIR-20, REDIR-26, REDIR-27; P6b-3, P6b-21).</summary>
[Trait("Category", "Unit")]
public sealed class RedirectOptionsTests
{
    [Fact]
    public void Defaults_are_three_hops_no_downgrade_no_303_and_GET_HEAD()
    {
        var options = new RedirectOptions();

        Assert.Equal(3, options.MaxRedirects);
        Assert.Equal(new HashSet<Method> { Method.Get, Method.Head }, options.AllowedMethods.ToHashSet());
        Assert.False(options.FollowSeeOther);
        Assert.Null(options.Predicate);
        Assert.False(options.AllowHttpsToHttpDowngrade);
    }

    [Fact]
    public void A_negative_MaxRedirects_throws_at_init()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new RedirectOptions { MaxRedirects = -1 });
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void Zero_is_accepted() => Assert.Equal(0, new RedirectOptions { MaxRedirects = 0 }.MaxRedirects);

    [Fact]
    public void AllowedMethods_is_copied_at_init()
    {
        var mine = new HashSet<Method> { Method.Post };
        var options = new RedirectOptions { AllowedMethods = mine };

        mine.Add(Method.Put);

        Assert.Equal([Method.Post], options.AllowedMethods.ToArray());
        Assert.NotSame(mine, options.AllowedMethods);
    }

    [Fact]
    public void A_null_set_or_a_null_element_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new RedirectOptions { AllowedMethods = null! });
        Assert.ThrowsAny<ArgumentException>(() => new RedirectOptions { AllowedMethods = new List<Method> { Method.Get, null! }.ToHashSet() });
    }

    [Fact]
    public void An_empty_set_is_accepted() => Assert.Empty(new RedirectOptions { AllowedMethods = new HashSet<Method>() }.AllowedMethods);

    [Fact]
    public void The_default_set_is_one_shared_frozen_instance() =>
        Assert.Same(new RedirectOptions().AllowedMethods, new RedirectOptions().AllowedMethods);

    [Fact]
    public void Equality_compares_the_set_by_content_and_the_predicate_by_delegate()
    {
        var a = new RedirectOptions { AllowedMethods = new HashSet<Method> { Method.Get, Method.Post } };
        var b = new RedirectOptions { AllowedMethods = new HashSet<Method> { Method.Post, Method.Get } };
        var c = new RedirectOptions { AllowedMethods = new HashSet<Method> { Method.Get } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);

        Func<RedirectCondition, bool> same = static _ => true;
        Assert.Equal(new RedirectOptions { Predicate = same }, new RedirectOptions { Predicate = same });
        Assert.NotEqual(new RedirectOptions { Predicate = static _ => true }, new RedirectOptions { Predicate = static _ => true });
        Assert.NotEqual(new RedirectOptions { MaxRedirects = 4 }, new RedirectOptions());
        Assert.NotEqual(new RedirectOptions { FollowSeeOther = true }, new RedirectOptions());
        Assert.NotEqual(new RedirectOptions { AllowHttpsToHttpDowngrade = true }, new RedirectOptions());
    }

    [Fact]
    public void ToString_lists_every_member_by_name()
    {
        var text = new RedirectOptions().ToString();

        foreach (var name in new[] { "MaxRedirects", "AllowHttpsToHttpDowngrade", "FollowSeeOther", "AllowedMethods", "Predicate" })
        {
            Assert.Contains(name, text, StringComparison.Ordinal);
        }

        Assert.Contains("GET, HEAD", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RedirectOptions_has_no_LocationHeader_member() =>
        // REDIR-27 (MAY) is declined for v1 (P6b-21): the Location header name is not configurable.
        Assert.DoesNotContain(typeof(RedirectOptions).GetProperties(), p => p.Name == "LocationHeader");

    [Fact]
    public void StripSensitiveHeadersOnCrossOrigin_no_longer_exists() =>
        // REDIR-7 and REDIR-9 are not switchable: the property was removed in phase 6b.
        Assert.DoesNotContain(typeof(RedirectOptions).GetProperties(), p => p.Name == "StripSensitiveHeadersOnCrossOrigin");
}
