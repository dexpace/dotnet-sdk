// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-38: every typed value reaches its parser through the one reader over the one <c>IConfiguration</c>.</summary>
[Trait("Category", "Unit")]
public sealed class TypedLookupTests
{
    [Fact]
    public void An_integer_a_boolean_and_a_duration_supplied_only_by_a_lower_provider_resolve()
    {
        var section = Config.Layered(
            [("Retry:MaxRetryAttempts", "4"), ("Retry:HonorRetryAfter", "false"), ("Retry:BaseDelay", "PT3S")],
            [("UserAgent", "higher")]);

        var configuration = ConfigurationReader.Read(section);

        Assert.Empty(configuration.Failures);
        Assert.Equal(4, configuration.Options.Retry.MaxRetryAttempts);
        Assert.False(configuration.Options.Retry.HonorRetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(3), configuration.Options.Retry.BaseDelay);
        Assert.Equal("higher", configuration.Options.UserAgent);
    }

    [Fact]
    public void The_record_default_applies_only_when_no_provider_has_the_key()
    {
        var configuration = Config.Read();

        Assert.Equal(new Dexpace.Sdk.Core.Configuration.RetryOptions(), configuration.Options.Retry);
    }

    [Fact]
    public void A_differently_cased_key_resolves_alike()
    {
        var configuration = Config.Read(("retry:maxretryattempts", "6"), ("RETRY:HONORRETRYAFTER", "false"));

        Assert.Empty(configuration.Failures);
        Assert.Equal(6, configuration.Options.Retry.MaxRetryAttempts);
        Assert.False(configuration.Options.Retry.HonorRetryAfter);
    }
}
