// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>OBS-35 (section 10 entry 26, P9-23): the log level is read by name only, from the caller's section, with no baked key.</summary>
[Trait("Category", "Unit")]
public sealed class LogLevelBindingTests
{
    [Theory]
    [InlineData("Body", HttpLogLevel.Body)]
    [InlineData("body", HttpLogLevel.Body)]
    [InlineData(" Headers ", HttpLogLevel.Headers)]
    [InlineData("none", HttpLogLevel.None)]
    public void A_name_binds_in_any_case(string value, HttpLogLevel expected)
    {
        Assert.Equal(expected, Config.Read(("Logging:Level", value)).Options.Logging.Level);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("7")]
    [InlineData("Verbose")]
    public void A_number_or_an_unknown_name_fails_naming_the_key_and_listing_the_names(string value)
    {
        // V1: the binder admits both a numeric and an undefined level; this reader does not.
        var failure = Assert.Single(Config.Failures(Config.Read(("Logging:Level", value))));

        Assert.StartsWith(Config.SectionPath + ":Logging:Level: ", failure, StringComparison.Ordinal);
        Assert.Contains("None, Headers, Body", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void The_section_is_always_the_callers_argument()
    {
        var other = Config.Root(("Acme:Http:Logging:Level", "Body")).GetSection("Acme:Http");
        Assert.Equal(HttpLogLevel.Body, ConfigurationReader.Read(other).Options.Logging.Level);

        var unrelated = Config.Root(("Dexpace:Logging:Level", "Body")).GetSection("Acme:Http");
        Assert.Equal(HttpLogLevel.None, ConfigurationReader.Read(unrelated).Options.Logging.Level);
    }
}
