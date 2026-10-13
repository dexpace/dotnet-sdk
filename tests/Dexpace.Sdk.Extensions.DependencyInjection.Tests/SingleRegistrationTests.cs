// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>SEAM-5, SEAM-6, XCUT-22, XCUT-23 (P9-16; V4): explicit installs replace the default, a different second install throws at the call, duplicates fail at start.</summary>
[Trait("Category", "Unit")]
public sealed class SingleRegistrationTests
{
    private static int Count<T>(IServiceCollection services) =>
        services.Count(static d => d.ServiceType == typeof(T) && Equals(d.ServiceKey, TestClients.Name));

    [Fact]
    public async Task An_explicit_transport_instance_replaces_the_default_and_is_never_disposed_by_the_container()
    {
        var services = new ServiceCollection();
        var scripted = new ScriptedTransport();
        _ = TestClients.Add(services).UseTransport(scripted);
        var provider = services.BuildServiceProvider();

        var resolved = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);
        var lease = provider.GetRequiredKeyedService<PrimaryHandlerLease>(TestClients.Name);
        await provider.DisposeAsync();

        Assert.Same(scripted, resolved);
        Assert.Empty(lease.Tracked);
        Assert.False(scripted.IsDisposed);
        Assert.Equal(1, Count<IAsyncHttpClient>(services));
    }

    [Fact]
    public async Task An_explicit_transport_factory_is_a_factory_descriptor_and_the_container_disposes_what_it_returns()
    {
        var services = new ServiceCollection();
        var scripted = new ScriptedTransport();
        _ = TestClients.Add(services).UseTransport(_ => scripted);
        var provider = services.BuildServiceProvider();

        Assert.Same(scripted, provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name));
        await provider.DisposeAsync();

        Assert.True(scripted.IsDisposed);
    }

    [Fact]
    public void A_synchronous_dispose_throws_for_an_async_only_transport()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseTransport(_ => new AsyncOnlyTransport());
        var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name);

        Assert.Throws<InvalidOperationException>(provider.Dispose);
    }

    [Fact]
    public async Task UseSerde_installs_an_instance_or_a_factory_the_same_way()
    {
        var services = new ServiceCollection();
        var serde = new Dexpace.Sdk.TestSupport.Serialization.BytesSerde(Dexpace.Sdk.Core.Http.Common.CommonMediaTypes.ApplicationJson, []);
        _ = services.AddDexpaceClient(TestClients.Name, Config.Section()).UseSerde(serde);
        var provider = services.BuildServiceProvider();

        Assert.Same(serde, provider.GetRequiredKeyedService<ISerde>(TestClients.Name));
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task No_codec_fails_validation_with_the_install_hint_and_fails_every_keyed_resolution()
    {
        var services = new ServiceCollection();
        _ = services.AddDexpaceClient(TestClients.Name, Config.Section());
        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IStartupValidator>().Validate);

        Assert.Equal(
            "client 'github' has no ISerde: call UseSerde(...) on AddDexpaceClient(\"github\")",
            Assert.Single(exception.Failures));
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredKeyedService<IAsyncHttpClient>(TestClients.Name));
    }

    [Fact]
    public void The_same_instance_or_the_same_delegate_again_is_a_no_op()
    {
        var services = new ServiceCollection();
        var scripted = new ScriptedTransport();
        Func<IServiceProvider, IAsyncHttpClient> factory = _ => scripted;
        var builder = TestClients.Add(services);

        _ = builder.UseTransport(scripted).UseTransport(scripted);
        Assert.Equal(1, Count<IAsyncHttpClient>(services));
        _ = builder.UseSerde(StubSerde.Instance);
        Assert.Equal(1, Count<ISerde>(services));

        var other = new ServiceCollection();
        _ = TestClients.Add(other).UseTransport(factory).UseTransport(factory);
        Assert.Equal(1, Count<IAsyncHttpClient>(other));
    }

    [Fact]
    public void A_different_second_install_throws_at_the_call_naming_the_client_and_both_implementations()
    {
        var services = new ServiceCollection();
        var builder = TestClients.Add(services).UseTransport(new ScriptedTransport());

        var transport = Assert.Throws<InvalidOperationException>(() => builder.UseTransport(new AsyncOnlyTransport()));
        var codec = Assert.Throws<InvalidOperationException>(() => builder.UseSerde(new Dexpace.Sdk.TestSupport.Serialization.BytesSerde(Dexpace.Sdk.Core.Http.Common.CommonMediaTypes.ApplicationJson, [])));

        Assert.Contains("'github'", transport.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ScriptedTransport), transport.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(AsyncOnlyTransport), transport.Message, StringComparison.Ordinal);
        Assert.Contains("'github'", codec.Message, StringComparison.Ordinal);
        Assert.Contains("BytesSerde", codec.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_raw_duplicate_keyed_registration_fails_at_start_listing_every_implementation()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseTransport(new ScriptedTransport());
        services.AddKeyedSingleton<IAsyncHttpClient>(TestClients.Name, new AsyncOnlyTransport());
        services.AddKeyedSingleton<ISerde>(TestClients.Name, (_, _) => StubSerde.Instance);
        await using var provider = services.BuildServiceProvider();

        // V4: GetRequiredKeyedService would silently resolve the last registration of each; the validator lists them.
        var exception = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IStartupValidator>().Validate);

        Assert.Equal(2, exception.Failures.Count());
        Assert.Contains(exception.Failures, f => f.Contains(nameof(ScriptedTransport), StringComparison.Ordinal) && f.Contains(nameof(AsyncOnlyTransport), StringComparison.Ordinal));
        Assert.Contains(exception.Failures, f => f.Contains("ISerde", StringComparison.Ordinal) && f.Contains("a factory", StringComparison.Ordinal) && f.Contains("BytesSerde", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_raw_descriptor_under_another_clients_key_is_not_counted()
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services).UseTransport(new ScriptedTransport());
        services.AddKeyedSingleton<IAsyncHttpClient>("other", new AsyncOnlyTransport());
        services.AddKeyedSingleton<ISerde>("other", StubSerde.Instance);
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStartupValidator>().Validate();
    }
}
