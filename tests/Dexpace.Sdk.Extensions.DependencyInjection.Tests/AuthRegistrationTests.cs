// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>P9-18: <c>UseAuth</c>, <c>UseAuthPolicy</c> and <c>ConfigureCredentials</c> over the redacting credentials.</summary>
[Trait("Category", "Unit")]
public sealed class AuthRegistrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly AuthDescriptor s_apiKey = new(new AuthRequirement(AuthScheme.ApiKey));

    [Fact]
    public async Task Auth_keys_and_UseAuth_install_a_policy_that_sends_the_configured_api_key()
    {
        var scripted = new ScriptedTransport(TestResponses.Create(Status.FromCode(200)));
        var services = new ServiceCollection();
        _ = TestClients.Add(services, TestClients.Name, ("Auth:ApiKey:Key", "k-123"), ("Auth:ApiKey:HeaderName", "X-Api-Key"))
            .UseTransport(scripted)
            .UseAuth(s_apiKey);
        await using var provider = services.BuildServiceProvider();

        using var response = await provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name).ExecuteAsync(Request.Get("https://api.test/x"), Ct);

        Assert.Equal("k-123", scripted.LastRequest!.Headers.Get("X-Api-Key"));
    }

    [Fact]
    public async Task ConfigureCredentials_adds_what_configuration_cannot_hold_after_the_configured_values()
    {
        var scripted = new ScriptedTransport(TestResponses.Create(Status.FromCode(200)));
        var services = new ServiceCollection();
        services.AddSingleton<TokenCredential>(new StaticToken());
        _ = TestClients.Add(services, TestClients.Name, ("Auth:ApiKey:Key", "k-123"))
            .UseTransport(scripted)
            .ConfigureCredentials(static (sp, credentials) => credentials with { Token = sp.GetRequiredService<TokenCredential>() })
            .UseAuth(new AuthDescriptor(new AuthRequirement(AuthScheme.OAuth2)));
        await using var provider = services.BuildServiceProvider();

        using var response = await provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name).ExecuteAsync(Request.Get("https://api.test/x"), Ct);

        Assert.Equal("Bearer tok-1", scripted.LastRequest!.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task UseAuthPolicy_installs_any_other_policy()
    {
        var scripted = new ScriptedTransport(TestResponses.Create(Status.FromCode(200)));
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseTransport(scripted).UseAuthPolicy(static _ => new ApiKeyAuthPolicy(new ApiKeyCredential("custom-key")));
        await using var provider = services.BuildServiceProvider();

        using var response = await provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name).ExecuteAsync(Request.Get("https://api.test/x"), Ct);

        Assert.Equal("custom-key", scripted.LastRequest!.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task Credentials_with_neither_UseAuth_nor_UseAuthPolicy_fail_validation_naming_the_client()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services, TestClients.Name, ("Auth:ApiKey:Key", "k-123")).UseTransport(new ScriptedTransport());
        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IStartupValidator>().Validate);

        var failure = Assert.Single(exception.Failures);
        Assert.StartsWith("github: credentials are configured but nothing sends them", failure, StringComparison.Ordinal);
        Assert.DoesNotContain("k-123", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void UseAuth_and_UseAuthPolicy_together_throw_at_the_second_call()
    {
        var builder = TestClients.Add(new ServiceCollection()).UseAuth(s_apiKey);
        Assert.Throws<InvalidOperationException>(() => builder.UseAuthPolicy(static _ => new ApiKeyAuthPolicy(new ApiKeyCredential("k"))));

        var other = TestClients.Add(new ServiceCollection()).UseAuthPolicy(static _ => new ApiKeyAuthPolicy(new ApiKeyCredential("k")));
        Assert.Throws<InvalidOperationException>(() => other.UseAuth(s_apiKey));
    }

    [Fact]
    public async Task UseAuth_with_no_credentials_is_legal_and_sends_nothing()
    {
        var scripted = new ScriptedTransport(TestResponses.Create(Status.FromCode(200)));
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseTransport(scripted).UseAuth(new AuthDescriptor(AuthRequirement.NoAuth));
        await using var provider = services.BuildServiceProvider();

        using var response = await provider.GetRequiredKeyedService<HttpPipeline>(TestClients.Name).ExecuteAsync(Request.Get("https://api.test/x"), Ct);

        Assert.Null(scripted.LastRequest!.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task A_ConfigureCredentials_returning_null_fails_naming_the_client()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseTransport(new ScriptedTransport()).ConfigureCredentials(static (_, _) => null!).UseAuth(s_apiKey);
        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IStartupValidator>().Validate);

        Assert.Contains("github: ConfigureCredentials returned null", exception.Failures);
    }

    private sealed class StaticToken : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            ValueTask.FromResult(new AccessToken("tok-1"));
    }
}
