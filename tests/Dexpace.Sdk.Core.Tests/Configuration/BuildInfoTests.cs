// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.InteropServices;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-36, P5a-22: the SDK version and runtime identity, resolved once, each falling back to "unknown", every token
// non-blank and header-safe.
[Trait("Category", "Unit")]
public sealed class BuildInfoTests
{
    [Fact]
    public void Every_field_is_non_blank_and_header_safe()
    {
        foreach (var token in new[] { BuildInfo.SdkVersion, BuildInfo.RuntimeVersion, BuildInfo.OSName })
        {
            Assert.False(string.IsNullOrWhiteSpace(token));
            Assert.True(HttpHeaderSyntax.IsValidName(token), token);
        }

        // The runtime description is free text ("Product 1.2.3"): only non-blank is promised.
        Assert.False(string.IsNullOrWhiteSpace(BuildInfo.RuntimeDescription));
    }

    [Fact]
    public void IdentityTokens_are_the_two_ordered_product_tokens()
    {
        var tokens = BuildInfo.IdentityTokens;

        Assert.Equal(["dexpace-dotnet/" + BuildInfo.SdkVersion, "dotnet/" + BuildInfo.RuntimeVersion], tokens);
        Assert.All(tokens, token =>
        {
            var parts = token.Split('/');
            Assert.Equal(2, parts.Length);
            Assert.True(HttpHeaderSyntax.IsValidName(parts[0]));
            Assert.True(HttpHeaderSyntax.IsValidName(parts[1]));
        });
        Assert.IsAssignableFrom<IReadOnlyList<string>>(tokens);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)tokens).Add("x"));
        Assert.Same(BuildInfo.IdentityTokens, tokens);
    }

    [Fact]
    public void The_sdk_version_carries_no_build_metadata()
    {
        Assert.DoesNotContain('+', BuildInfo.SdkVersion);
    }

    [Fact]
    public void The_runtime_version_is_Environment_Version()
    {
        Assert.Equal(Environment.Version.ToString(), BuildInfo.RuntimeVersion);
        Assert.Contains(' ', RuntimeInformation.FrameworkDescription);
        Assert.DoesNotContain(' ', BuildInfo.RuntimeVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1.0 beta")]
    [InlineData("1.\r\n0")]
    [InlineData("ünï")]
    [InlineData("a/b")]
    [InlineData("a;b")]
    public void ToToken_rejects_what_would_break_a_User_Agent(string? value)
    {
        Assert.Equal(BuildInfo.Unknown, BuildInfo.ToToken(value));
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("0.0.1-alpha.1", "0.0.1-alpha.1")]
    [InlineData("10.0.401", "10.0.401")]
    [InlineData(" 1.2.3 ", "1.2.3")]
    [InlineData("1.0\r\n", "1.0")]
    public void ToToken_keeps_a_valid_token_and_trims_surrounding_whitespace(string value, string expected)
    {
        Assert.Equal(expected, BuildInfo.ToToken(value));
    }

    [Fact]
    public void A_throwing_probe_yields_unknown_for_that_field_only()
    {
        Assert.Equal(BuildInfo.Unknown, BuildInfo.Probe(() => throw new InvalidOperationException("probe broke")));
        Assert.Equal("ok", BuildInfo.Probe(() => "ok"));
        Assert.Equal(BuildInfo.Unknown, BuildInfo.Probe(() => null));
    }

    [Fact]
    public void A_fatal_probe_failure_propagates()
    {
#pragma warning disable CA2201 // A fatal exception is the point.
        Assert.Throws<OutOfMemoryException>(() => BuildInfo.Probe(() => throw new OutOfMemoryException()));
#pragma warning restore CA2201
    }

    [Fact]
    public void OSName_maps_the_platform()
    {
        Assert.Equal("windows", BuildInfo.OsNameFor(p => p == OSPlatform.Windows));
        Assert.Equal("linux", BuildInfo.OsNameFor(p => p == OSPlatform.Linux));
        Assert.Equal("macos", BuildInfo.OsNameFor(p => p == OSPlatform.OSX));
        Assert.Equal("freebsd", BuildInfo.OsNameFor(p => p == OSPlatform.FreeBSD));
        Assert.Equal(BuildInfo.Unknown, BuildInfo.OsNameFor(_ => false));
    }

    [Fact]
    public void SdkVersion_internal_forwarder_agrees()
    {
        // 5c's ActivitySource and Meter versions read the same value.
        Assert.Equal(BuildInfo.SdkVersion, Dexpace.Sdk.Core.Internal.SdkVersion.Value);
    }
}
