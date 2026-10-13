// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>P9-9, P9-10, P9-11, P9-28 (V4): every failure reported at once as <c>"&lt;path&gt;: &lt;rule&gt;"</c>, value-free, at start or at first resolution.</summary>
[Trait("Category", "Unit")]
public sealed class ValidationTests
{
    [Fact]
    public void Every_failure_is_reported_at_once()
    {
        var host = StateHost.WithClient(
            static _ => { },
            ("Retry:MaxRetryAttempts", "abc"),
            ("Retry:HonorRetryAfter", "yes"),
            ("Retry:Jitter", "7"));

        var exception = Assert.Throws<OptionsValidationException>(() => host.Get());

        Assert.Equal(3, exception.Failures.Count());
        Assert.All(exception.Failures, f => Assert.StartsWith(Config.SectionPath + ":Retry:", f, StringComparison.Ordinal));
    }

    [Fact]
    public void No_configured_string_url_or_credential_is_quoted()
    {
        var host = StateHost.WithClient(
            static _ => { },
            ("BaseAddress", "https://u:s3cr3t@host.test/s3cr3t?x=s3cr3t#s3cr3t"),
            ("Retry:AttemptHeaderName", "s3cr3t header"),
            ("Redirect:AllowedMethods", "s3cr3t verb"),
            ("Auth:Basic:Username", "s3cr3t"),
            ("Transport:Proxy:Host", "proxy.test"),
            ("Transport:Proxy:Port", "s3cr3t"),
            ("Transport:Proxy:Password", "s3cr3t"));

        var exception = Assert.Throws<OptionsValidationException>(() => host.Get());

        Assert.DoesNotContain("s3cr3t", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", exception.ToString(), StringComparison.Ordinal);
        Assert.All(exception.Failures, f => Assert.DoesNotContain("s3cr3t", f, StringComparison.Ordinal));
    }

    [Fact]
    public void A_numeric_accessor_rejection_may_echo_the_number_and_nothing_else()
    {
        // P9-28: the BCL's range helpers print the rejected number; no numeric option carries a secret.
        var configuration = Config.Read(("Retry:MaxRetryAttempts", "-7"), ("UserAgent", "s3cr3t\u0001"));

        var failures = Config.Failures(configuration);
        Assert.Equal(2, failures.Length);
        Assert.Contains("-7", failures.Single(static f => f.Contains("MaxRetryAttempts", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3t", string.Join('|', failures), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_keys_fail_naming_the_key_in_the_configurations_spelling()
    {
        var host = StateHost.WithClient(static _ => { }, ("Retry:MaxRetries", "3"), ("Wat", "1"), ("RETRY:maxretryattempts", "2"));

        var exception = Assert.Throws<OptionsValidationException>(() => host.Get());

        Assert.Equal(2, exception.Failures.Count());
        Assert.Contains(Config.SectionPath + ":Retry:MaxRetries: unknown key", exception.Failures);
        Assert.Contains(Config.SectionPath + ":Wat: unknown key", exception.Failures);
    }

    [Fact]
    public void A_key_under_the_wrong_parent_or_below_a_scalar_is_unknown()
    {
        Assert.Single(Config.Failures(Config.Read(("Transport:Retry:MaxRetryAttempts", "1"))));
        Assert.Single(Config.Failures(Config.Read(("Retry:MaxRetryAttempts:Extra", "1"))));
    }

    [Theory]
    [InlineData("Transport:Proxy", "http://user:s3cr3tpw@proxy:8080")]
    [InlineData("Auth:ApiKey", "s3cr3tpw")]
    [InlineData("Auth", "Bearer s3cr3tpw")]
    [InlineData("Retry", "s3cr3tpw")]
    public void A_value_on_a_group_key_fails_without_quoting_it(string key, string value)
    {
        var failure = Assert.Single(Config.Failures(Config.Read((key, value))));

        Assert.Contains(key + ": a group of keys, not a value", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("s3cr3tpw", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_value_on_a_group_key_is_absent_not_a_failure()
    {
        Assert.Empty(Config.Failures(Config.Read(("Retry", ""), ("Auth", "  "))));
    }

    [Fact]
    public void A_null_section_binds_nothing()
    {
        var configuration = ConfigurationReader.Read(null);

        Assert.Empty(configuration.Failures);
        Assert.Equal(new Dexpace.Sdk.Core.Configuration.RetryOptions(), configuration.Options.Retry);
    }

    [Fact]
    public void A_root_section_is_rejected_with_a_pointed_message_and_reads_nothing()
    {
        var root = Config.Root(("PATH", "/usr/bin"), ("Retry:MaxRetryAttempts", "9")).GetSection(string.Empty);

        var configuration = ConfigurationReader.Read(root);

        Assert.Equal("pass the client's own section, not the configuration root", Assert.Single(Config.Failures(configuration)));
        Assert.Equal(new Dexpace.Sdk.Core.Configuration.RetryOptions(), configuration.Options.Retry);
    }

    [Fact]
    public void A_host_less_provider_fails_at_first_resolution()
    {
        var host = StateHost.WithClient(static _ => { }, ("Retry:MaxRetryAttempts", "abc"));

        Assert.Throws<OptionsValidationException>(() => host.Provider.GetRequiredService<IOptionsMonitor<DexpaceClientState>>().Get(StateHost.Name));
    }

    [Fact]
    public void IStartupValidator_runs_the_named_validators()
    {
        // R12: this is the service the hosting startup calls, so "fails at host start" is the same call.
        var host = StateHost.WithClient(static _ => { }, ("Retry:MaxRetryAttempts", "abc"));

        var validator = host.Provider.GetRequiredService<IStartupValidator>();

        var exception = Assert.Throws<OptionsValidationException>(validator.Validate);
        Assert.Contains("Retry:MaxRetryAttempts", Assert.Single(exception.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public void Two_names_validate_independently()
    {
        var services = new ServiceCollection();
        _ = StateHost.Register(services, "a", Config.Section(("Retry:MaxRetryAttempts", "abc")));
        _ = StateHost.Register(services, "b", Config.Section(("Retry:MaxRetryAttempts", "2")));
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<DexpaceClientState>>();

        Assert.Throws<OptionsValidationException>(() => monitor.Get("a"));
        Assert.Equal(2, monitor.Get("b").Options.Retry.MaxRetryAttempts);
    }

    [Fact]
    public void A_valid_client_validates_clean_and_once_per_name_registration_adds_one_validator_start()
    {
        var host = StateHost.WithClient(static _ => { }, ("Retry:MaxRetryAttempts", "2"));

        host.Provider.GetRequiredService<IStartupValidator>().Validate();

        Assert.Single(host.Provider.GetServices<IValidateOptions<DexpaceClientState>>());
    }

    [Fact]
    public void Cross_property_combinations_the_records_accept_do_not_fail()
    {
        // P9-10: BaseDelay above MaxDelay caps at MaxDelay (6a); an AttemptTimeout above OverallTimeout never fires; a transport timeout
        // below AttemptTimeout bounds the attempt at the transport. Failing them only under DI would give one record two validities.
        var host = StateHost.WithClient(
            static _ => { },
            ("Retry:BaseDelay", "10s"),
            ("Retry:MaxDelay", "1s"),
            ("OverallTimeout", "5s"),
            ("AttemptTimeout", "10s"),
            ("Transport:Timeout", "1s"));

        var state = host.Get();

        Assert.Equal(TimeSpan.FromSeconds(10), state.Options.Retry.BaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(1), state.Transport.Timeout);
    }

    [Fact]
    public void No_reload_reaches_a_resolved_state_and_no_change_token_source_is_registered()
    {
        var root = Config.Root((Config.SectionPath + ":UserAgent", "first"));
        var services = new ServiceCollection();
        _ = StateHost.Register(services, StateHost.Name, root.GetSection(Config.SectionPath));
        using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<DexpaceClientState>>();
        Assert.Equal("first", monitor.Get(StateHost.Name).Options.UserAgent);

        root[Config.SectionPath + ":UserAgent"] = "second";
        root.Reload();

        Assert.Equal("first", monitor.Get(StateHost.Name).Options.UserAgent);
        Assert.Empty(provider.GetServices<IOptionsChangeTokenSource<DexpaceClientState>>());
    }
}
