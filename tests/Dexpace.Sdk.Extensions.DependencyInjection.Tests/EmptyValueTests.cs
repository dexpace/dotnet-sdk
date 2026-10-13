// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-2 (P9-7, section 10 entry 25): an empty or whitespace-only value is absent, so the record default applies and a lower provider's value is not consulted.</summary>
[Trait("Category", "Unit")]
public sealed class EmptyValueTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void An_empty_value_over_a_lower_providers_value_resolves_the_record_default(string empty)
    {
        var section = Config.Layered([("Retry:MaxRetryAttempts", "5")], [("Retry:MaxRetryAttempts", empty)]);

        var configuration = ConfigurationReader.Read(section);

        Assert.Empty(configuration.Failures);
        Assert.Equal(new RetryOptions().MaxRetryAttempts, configuration.Options.Retry.MaxRetryAttempts);
    }

    [Fact]
    public void Every_kind_of_key_treats_empty_as_absent()
    {
        var higher = new[]
        {
            ("UserAgent", ""),
            ("Retry:MaxRetryAttempts", ""),
            ("Retry:BaseDelay", " "),
            ("Retry:RetryableStatusCodes", ""),
        };
        var lower = new[]
        {
            ("UserAgent", "lower-agent"),
            ("Retry:MaxRetryAttempts", "7"),
            ("Retry:BaseDelay", "9s"),
            ("Retry:RetryableStatusCodes", "500"),
        };

        var configuration = ConfigurationReader.Read(Config.Layered([.. lower], [.. higher]));

        Assert.Empty(configuration.Failures);
        var defaults = new DexpaceClientOptions();
        Assert.Equal(defaults.UserAgent, configuration.Options.UserAgent);
        Assert.NotEqual(string.Empty, configuration.Options.UserAgent);
        Assert.Equal(defaults.Retry, configuration.Options.Retry);
    }
}
