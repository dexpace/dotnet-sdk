// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-1 (section 10 entry 25) and CFG-10: the record initializer, the caller's providers, then the <c>Configure</c> transform; there is no removal.</summary>
[Trait("Category", "Unit")]
public sealed class ConfigurationPrecedenceTests
{
    private static readonly (string, string?)[] s_appsettings = [("UserAgent", "appsettings")];
    private static readonly (string, string?)[] s_environment = [("UserAgent", "environment")];
    private static readonly (string, string?)[] s_override = [("UserAgent", "override")];

    [Fact]
    public void Removing_each_provider_from_the_top_resolves_the_next()
    {
        Assert.Equal("override", UserAgent(s_appsettings, s_environment, s_override));
        Assert.Equal("environment", UserAgent(s_appsettings, s_environment));
        Assert.Equal("appsettings", UserAgent(s_appsettings));
        Assert.Equal(new DexpaceClientOptions().UserAgent, UserAgent());
    }

    [Fact]
    public void The_Configure_transform_is_applied_last_in_call_order()
    {
        var services = StateHost.WithClient(
            registration =>
            {
                registration.OptionsTransforms.Add(static o => o with { UserAgent = "a" });
                registration.OptionsTransforms.Add(static o => o with { UserAgent = "b" });
            },
            ("UserAgent", "from-configuration"));

        Assert.Equal("b", services.Get().Options.UserAgent);
    }

    [Fact]
    public void A_transform_overrides_a_configuration_value_and_removing_it_resolves_the_configuration()
    {
        var with = StateHost.WithClient(r => r.OptionsTransforms.Add(static o => o with { UserAgent = "code" }), ("UserAgent", "config"));
        var without = StateHost.WithClient(static _ => { }, ("UserAgent", "config"));

        Assert.Equal("code", with.Get().Options.UserAgent);
        Assert.Equal("config", without.Get().Options.UserAgent);
    }

    [Fact]
    public void A_transform_returning_null_fails_naming_the_client()
    {
        var provider = StateHost.WithClient(r => r.OptionsTransforms.Add(static _ => null!));

        var exception = Assert.Throws<OptionsValidationException>(() => provider.Get());

        Assert.Equal("github: Configure returned null", Assert.Single(exception.Failures));
    }

    [Fact]
    public void A_transform_that_throws_is_a_failure_naming_the_client_and_never_the_message()
    {
        var provider = StateHost.WithClient(r => r.OptionsTransforms.Add(static _ => throw new InvalidOperationException("s3cr3t")));

        var exception = Assert.Throws<OptionsValidationException>(() => provider.Get());

        var failure = Assert.Single(exception.Failures);
        Assert.StartsWith("github: Configure threw InvalidOperationException", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void Precedence_has_no_removal_operation()
    {
        // CFG-10 (N/A, vacuous): a transform is code and a record has no removal, so the registration exposes nothing to remove with.
        var members = typeof(DexpaceClientRegistration).GetMembers(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        Assert.DoesNotContain(members, static m => m.Name.StartsWith("Remove", StringComparison.Ordinal) || m.Name.StartsWith("Clear", StringComparison.Ordinal));
    }

    private static string UserAgent(params (string, string?)[][] layers) => ConfigurationReader.Read(Config.Layered(layers)).Options.UserAgent;
}
