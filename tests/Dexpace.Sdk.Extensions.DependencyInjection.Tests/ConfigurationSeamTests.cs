// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>CFG-11: the seam is a lookup, and the recording lookup observes every proxy variable the resolver reads and no other.</summary>
[Trait("Category", "Unit")]
public sealed class ConfigurationSeamTests
{
    [Fact]
    public async Task The_lookup_observes_exactly_the_proxy_variables_the_resolver_reads()
    {
        var asked = new List<string>();
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseProxyFromEnvironment(key =>
        {
            asked.Add(key);
            return null;
        });
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        // 5a's resolver: the upper- and lower-case spellings of the two proxy variables; NO_PROXY and GATEWAY_INTERFACE only once a proxy was found.
        Assert.Equal(
            new HashSet<string> { "HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy" },
            asked.ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public async Task When_a_proxy_is_found_the_additional_reads_are_the_documented_ones()
    {
        var asked = new List<string>();
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseProxyFromEnvironment(key =>
        {
            asked.Add(key);
            return key == "HTTPS_PROXY" ? "http://proxy.test:3128" : null;
        });
        await using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        var universe = new HashSet<string> { "HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy", "NO_PROXY", "no_proxy" };
        Assert.Subset(universe, asked.ToHashSet(StringComparer.Ordinal));
        Assert.Contains("NO_PROXY", asked);
    }
}
