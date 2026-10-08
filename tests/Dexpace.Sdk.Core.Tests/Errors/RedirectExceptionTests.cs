// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>The redirect exception family (REDIR-6, REDIR-15; P6b-16).</summary>
[Trait("Category", "Unit")]
public sealed class RedirectExceptionTests
{
    private static readonly Uri s_from = new("https://u:p@api.example.com/a?sig=secret");
    private static readonly Uri s_to = new("http://other.example/b?sig=secret&x=1");

    [Fact]
    public void The_family_derives_from_SdkException_and_not_from_ServiceRequestException()
    {
        Assert.True(typeof(SdkException).IsAssignableFrom(typeof(RedirectException)));
        Assert.False(typeof(ServiceRequestException).IsAssignableFrom(typeof(RedirectException)));
        Assert.False(typeof(ServiceRequestException).IsAssignableFrom(typeof(RedirectSchemeDowngradeException)));
    }

    [Fact]
    public void Both_leaves_are_sealed_and_derive_from_RedirectException()
    {
        foreach (var leaf in new[] { typeof(RedirectSchemeDowngradeException), typeof(RedirectBodyNotReplayableException) })
        {
            Assert.True(leaf.IsSealed);
            Assert.Equal(typeof(RedirectException), leaf.BaseType);
        }
    }

    [Fact]
    public void Each_type_has_the_three_standard_constructors()
    {
        var inner = new InvalidOperationException("inner");

        Assert.Null(new RedirectException().InnerException);
        Assert.Equal("m", new RedirectException("m").Message);
        Assert.Same(inner, new RedirectException("m", inner).InnerException);
        Assert.Equal("m", new RedirectSchemeDowngradeException("m").Message);
        Assert.Same(inner, new RedirectSchemeDowngradeException("m", inner).InnerException);
        Assert.NotNull(new RedirectSchemeDowngradeException());
        Assert.Equal("m", new RedirectBodyNotReplayableException("m").Message);
        Assert.Same(inner, new RedirectBodyNotReplayableException("m", inner).InnerException);
        Assert.NotNull(new RedirectBodyNotReplayableException());
    }

    [Fact]
    public void The_downgrade_message_names_both_urls_redacted_and_the_opt_in()
    {
        var text = RedirectMessages.Downgrade(302, s_from, s_to);

        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("u:p", text, StringComparison.Ordinal);
        Assert.Contains("api.example.com", text, StringComparison.Ordinal);
        Assert.Contains("other.example", text, StringComparison.Ordinal);
        Assert.Contains("302", text, StringComparison.Ordinal);
        Assert.Contains("RedirectOptions.AllowHttpsToHttpDowngrade", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_replay_message_names_replayability_and_ToReplayableAsync()
    {
        var text = RedirectMessages.NotReplayable(307, s_to);

        Assert.Contains("not replayable", text, StringComparison.Ordinal);
        Assert.Contains("ToReplayableAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_leaf_exposes_a_Uri_or_string_url_property()
    {
        // XCUT-19: a structured logger that destructures exception properties must find no raw URL.
        foreach (var type in new[] { typeof(RedirectException), typeof(RedirectSchemeDowngradeException), typeof(RedirectBodyNotReplayableException) })
        {
            var declared = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            Assert.Empty(declared);
        }
    }

    [Fact]
    public void A_relative_url_input_renders_instead_of_throwing()
    {
        var relative = new Uri("/only/a/path", UriKind.Relative);

        var text = RedirectMessages.Downgrade(301, relative, relative);

        // UrlRedactor is total over any Uri: a degenerate input still yields a message.
        Assert.StartsWith("Refused to follow the 301 redirect", text, StringComparison.Ordinal);
    }
}
