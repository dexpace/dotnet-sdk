// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

/// <summary>AUTH-29: the origin comparison the authorization step uses.</summary>
[Trait("Category", "Unit")]
public sealed class AuthOriginTests
{
    [Fact]
    public void Same_scheme_host_and_port_are_the_same_origin()
    {
        Assert.True(AuthOrigin.Same(new Uri("https://a/x"), new Uri("https://a/y?q=1")));
    }

    [Fact]
    public void Default_ports_are_equal_to_explicit_ones()
    {
        Assert.True(AuthOrigin.Same(new Uri("https://a/"), new Uri("https://a:443/")));
        Assert.True(AuthOrigin.Same(new Uri("http://a/"), new Uri("http://a:80/")));
    }

    [Fact]
    public void Host_and_scheme_compare_case_insensitively()
    {
        Assert.True(AuthOrigin.Same(new Uri("HTTPS://Example.COM/"), new Uri("https://example.com/")));
    }

    [Fact]
    public void A_different_port_is_a_different_origin()
    {
        Assert.False(AuthOrigin.Same(new Uri("https://a/"), new Uri("https://a:8443/")));
        Assert.False(AuthOrigin.Same(new Uri("https://a/"), new Uri("http://a/")));
        Assert.False(AuthOrigin.Same(new Uri("https://a/"), new Uri("https://b/")));
    }
}
