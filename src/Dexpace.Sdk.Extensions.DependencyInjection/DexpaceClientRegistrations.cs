// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>The registration of a client's options state: the setup, the validator and <c>ValidateOnStart</c> (P9-11; R3, R12).</summary>
internal static class DexpaceClientRegistrations
{
    /// <summary>
    /// Registers the named options of <paramref name="name"/> with the setup, the validator and <c>ValidateOnStart</c>, and no
    /// <c>IOptionsChangeTokenSource</c>: the snapshot is taken once, at first resolution, so a reload never reaches a built client.
    /// </summary>
    /// <param name="services">The collection.</param>
    /// <param name="name">The client's name.</param>
    /// <param name="section">The client's section, or <see langword="null"/>.</param>
    /// <returns>The registration the builder records into.</returns>
    internal static DexpaceClientRegistration AddClientState(IServiceCollection services, string name, IConfigurationSection? section)
    {
        var registration = new DexpaceClientRegistration(name, services, section);
        services.AddKeyedSingleton(name, registration);
        services.AddSingleton<IConfigureOptions<DexpaceClientState>>(provider => new DexpaceClientStateSetup(registration, provider));
        services.AddSingleton<IValidateOptions<DexpaceClientState>>(new DexpaceClientValidator(registration));
        services.AddOptions<DexpaceClientState>(name).ValidateOnStart();
        return registration;
    }
}
