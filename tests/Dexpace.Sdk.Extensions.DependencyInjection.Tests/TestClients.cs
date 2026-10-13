// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.DependencyInjection;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>The one place a test registers a client: a codec stub is installed here, once (F1).</summary>
internal static class TestClients
{
    /// <summary>The name every test client uses.</summary>
    internal const string Name = "github";

    /// <summary>Registers <paramref name="name"/> over an in-memory section holding <paramref name="values"/> and installs the stub codec.</summary>
    internal static DexpaceClientBuilder Add(IServiceCollection services, string name = Name, params (string Key, string? Value)[] values) =>
        services.AddDexpaceClient(name, Config.Section(values)).UseSerde(StubSerde.Instance);

    /// <summary>A provider with scope and build validation on, over one client.</summary>
    internal static ServiceProvider Provider(Action<DexpaceClientBuilder>? configure = null, params (string Key, string? Value)[] values)
    {
        var services = new ServiceCollection();
        var builder = Add(services, Name, values);
        configure?.Invoke(builder);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
