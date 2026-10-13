// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-11, CFG-28 (P9-15): explicit keys or an explicit <c>UseProxyFromEnvironment</c>, never both, never a configuration switch, never at registration.</summary>
[Trait("Category", "Unit")]
public sealed class ProxyRegistrationTests
{
    private static readonly Uri s_destination = new("http://example.test/");

    private static (ServiceCollection Services, DexpaceClientBuilder Builder, ChainSpy Spy) Client(params (string, string?)[] values)
    {
        var services = new ServiceCollection();
        var builder = TestClients.Add(services, TestClients.Name, values);
        var spy = new ChainSpy();
        builder.HttpClient.AddHttpMessageHandler(() => spy);
        return (services, builder, spy);
    }

    [Fact]
    public async Task Explicit_keys_reach_the_primary_handler()
    {
        var (services, _, spy) = Client(("Transport:Proxy:Host", "proxy.test"), ("Transport:Proxy:Port", "3128"));
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        var primary = Assert.IsType<SocketsHttpHandler>(spy.Primary);
        Assert.True(primary.UseProxy);
        Assert.Equal("http://proxy.test:3128/", primary.Proxy!.GetProxy(s_destination)!.AbsoluteUri);
    }

    [Fact]
    public async Task UseProxyFromEnvironment_with_a_lookup_resolves_lazily_through_the_callers_lookup()
    {
        var asked = new List<string>();
        var (services, builder, spy) = Client();
        _ = builder.UseProxyFromEnvironment(key =>
        {
            asked.Add(key);
            return key == "HTTPS_PROXY" ? "http://envproxy.test:3128" : null;
        });
        Assert.Empty(asked);
        await using var provider = services.BuildServiceProvider();
        Assert.Empty(asked);

        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        Assert.NotEmpty(asked);
        var primary = Assert.IsType<SocketsHttpHandler>(spy.Primary);
        Assert.Equal("http://envproxy.test:3128/", primary.Proxy!.GetProxy(s_destination)!.AbsoluteUri);
    }

    [Fact]
    public async Task The_documented_IConfiguration_form_serves_HTTPS_PROXY_from_any_provider()
    {
        var configuration = Config.Root(("HTTPS_PROXY", "http://fromconfig.test:8080"));
        var (services, builder, spy) = Client();
        _ = builder.UseProxyFromEnvironment(key => configuration[key]);
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        Assert.Equal("http://fromconfig.test:8080/", Assert.IsType<SocketsHttpHandler>(spy.Primary).Proxy!.GetProxy(s_destination)!.AbsoluteUri);
    }

    [Fact]
    public async Task Two_proxy_sources_fail_validation()
    {
        var (services, builder, _) = Client(("Transport:Proxy:Host", "proxy.test"), ("Transport:Proxy:Port", "3128"));
        _ = builder.UseProxyFromEnvironment(static _ => null);
        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IStartupValidator>().Validate);

        Assert.Contains("two proxy sources", Assert.Single(exception.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfigureTransport_setting_Proxy_counts_as_an_explicit_source()
    {
        var (services, builder, _) = Client();
        _ = builder
            .ConfigureTransport(static o => o with { Proxy = new Dexpace.Sdk.Core.Configuration.ProxyOptions { Host = "proxy.test", Port = 1 } })
            .UseProxyFromEnvironment(static _ => null);
        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IStartupValidator>().Validate);

        Assert.Contains("two proxy sources", Assert.Single(exception.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_client_with_no_proxy_source_goes_direct()
    {
        var (services, _, spy) = Client();
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        var primary = Assert.IsType<SocketsHttpHandler>(spy.Primary);
        Assert.False(primary.UseProxy);
        Assert.Null(primary.Proxy);
    }
}
