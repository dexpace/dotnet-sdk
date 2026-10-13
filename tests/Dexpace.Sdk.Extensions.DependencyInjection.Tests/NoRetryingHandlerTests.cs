// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>XCUT-10's DI clause (P9-19): the package adds no retrying handler and no handler at all besides the primary.</summary>
[Trait("Category", "Unit")]
public sealed class NoRetryingHandlerTests
{
    [Fact]
    public async Task The_chain_holds_nothing_the_package_added_besides_the_primary()
    {
        var services = new ServiceCollection();
        var builder = TestClients.Add(services);
        using var spy = new ChainSpy();
        builder.HttpClient.AddHttpMessageHandler(() => spy);
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        // Below the test's own handler: only the SocketsHttpHandler. Above it, only the factory's lifetime tracking handler.
        Assert.Single(spy.Below());
        Assert.IsType<SocketsHttpHandler>(spy.Primary);
    }

    [Fact]
    public void The_package_registers_no_resilience_or_Polly_service()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services);

        Assert.DoesNotContain(
            services,
            d => (d.ServiceType.Assembly.GetName().Name ?? string.Empty).Contains("Resilience", StringComparison.Ordinal)
                || (d.ServiceType.Assembly.GetName().Name ?? string.Empty).Contains("Polly", StringComparison.Ordinal)
                || (d.ServiceType.FullName ?? string.Empty).Contains("Resilience", StringComparison.Ordinal));
    }
}
