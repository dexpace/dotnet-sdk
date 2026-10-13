// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>Registers named dexpace clients with a container (CFG-37, SEAM-5, SEAM-6; P9-5).</summary>
public static class DexpaceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the client <paramref name="name"/> with the defaults of every option record. Call <see cref="DexpaceClientBuilder.Configure"/>
    /// and the other builder methods to configure it; resolve it as the keyed services <c>HttpPipeline</c>, <c>IAsyncHttpClient</c>, <c>ISerde</c>
    /// and <c>DexpaceClientOptions</c> under <paramref name="name"/>.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="name">The client's name, which is also its service key. Case-sensitive.</param>
    /// <returns>The builder; <see cref="DexpaceClientBuilder.Services"/> chains back to the collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">The name is already registered with a different configuration section.</exception>
    public static DexpaceClientBuilder AddDexpaceClient(this IServiceCollection services, string name)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return AddClient(services, name, section: null);
    }

    /// <summary>
    /// Registers the client <paramref name="name"/>, reading its settings from <paramref name="configuration"/> (CFG-1 to CFG-7, OBS-35). The keys
    /// are relative to the section, case-insensitive and <c>:</c>-separated; a value that does not parse, a record that rejects it and an unknown
    /// key each fail the host's start naming the key and never the value. The section is a snapshot taken at first resolution: a reload does not reach
    /// a built client.
    /// </summary>
    /// <param name="services">The collection to register into.</param>
    /// <param name="name">The client's name, which is also its service key. Case-sensitive.</param>
    /// <param name="configuration">
    /// The client's own section, for example <c>configuration.GetSection("Dexpace:GitHub")</c>. Never the configuration root: a root holds every
    /// process variable, so it could never boot.
    /// </param>
    /// <returns>The builder; <see cref="DexpaceClientBuilder.Services"/> chains back to the collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="name"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank, or <paramref name="configuration"/> is the configuration root.</exception>
    /// <exception cref="InvalidOperationException">The name is already registered with a different configuration section.</exception>
    public static DexpaceClientBuilder AddDexpaceClient(this IServiceCollection services, string name, IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Path.Length == 0)
        {
            throw new ArgumentException(ConfigurationReader.RootRule, nameof(configuration));
        }

        return AddClient(services, name, configuration);
    }

    private static DexpaceClientBuilder AddClient(IServiceCollection services, string name, IConfigurationSection? section)
    {
        // SEAM-6 applied to the client: one name registers once; the same call again returns the builder, a different one fails.
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(DexpaceClientRegistration) && Equals(d.ServiceKey, name));
        if (existing?.KeyedImplementationInstance is DexpaceClientRegistration registered)
        {
            return ReferenceEquals(registered.Section, section)
                ? registered.Builder!
                : throw new InvalidOperationException(
                    "The client '" + name + "' is already registered with a different configuration section (SEAM-6). Register a client once, or use another name.");
        }

        var registration = DexpaceClientRegistrations.AddClientState(services, name, section);
        NamedClient.AddKeyedServices(registration);
        var builder = new DexpaceClientBuilder(registration, NamedClient.AddHandlerChain(registration));
        registration.Builder = builder;
        return builder;
    }
}
