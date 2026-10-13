// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>A provider over one client's options state, registered through the internal seam <c>AddClientState</c> (R3).</summary>
internal sealed class StateHost
{
    /// <summary>The name every test client uses.</summary>
    internal const string Name = "github";

    private StateHost(ServiceProvider provider, DexpaceClientRegistration registration)
    {
        Provider = provider;
        Registration = registration;
    }

    internal ServiceProvider Provider { get; }

    internal DexpaceClientRegistration Registration { get; }

    /// <summary>Builds a provider, lets <paramref name="configure"/> edit the registration, and keeps the provider alive for the test.</summary>
    internal static StateHost WithClient(Action<DexpaceClientRegistration> configure, params (string Key, string? Value)[] values)
    {
        var services = new ServiceCollection();
        var registration = Register(services, Name, values.Length == 0 ? null : Config.Section(values));
        configure(registration);
        return new StateHost(services.BuildServiceProvider(), registration);
    }

    /// <summary>
    /// Registers a client's options state through the internal seam and gives it a stub transport and codec descriptor, because the validator now fails a client
    /// with no codec (F1) and these tests are not about registration.
    /// </summary>
    internal static DexpaceClientRegistration Register(IServiceCollection services, string name, Microsoft.Extensions.Configuration.IConfigurationSection? section)
    {
        var registration = DexpaceClientRegistrations.AddClientState(services, name, section);
        services.AddKeyedSingleton(name, StubSerde.Instance);
        services.AddKeyedSingleton<Dexpace.Sdk.Core.Client.IAsyncHttpClient>(name, new Dexpace.Sdk.TestSupport.Transports.ScriptedTransport());
        return registration;
    }

    /// <summary>Resolves the validated state; throws <see cref="OptionsValidationException"/> when validation fails.</summary>
    internal DexpaceClientState Get() => Provider.GetRequiredService<IOptionsMonitor<DexpaceClientState>>().Get(Name);
}
