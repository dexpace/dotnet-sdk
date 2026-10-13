// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Http.SystemNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// What the builder records for one named client: its section, its transforms and the collection it registered into (P9-5, P9-6). It exposes
/// no removal member: a transform is code, and a record has no removal (CFG-10).
/// </summary>
/// <param name="name">The client's name, which is also its key.</param>
/// <param name="services">The collection the client registered into; the validator scans it for duplicate keyed descriptors (SEAM-5).</param>
/// <param name="section">The client's configuration section, or <see langword="null"/>.</param>
internal sealed class DexpaceClientRegistration(string name, IServiceCollection services, IConfigurationSection? section)
{
    /// <summary>The client's name.</summary>
    internal string Name { get; } = name;

    /// <summary>The collection registered into.</summary>
    internal IServiceCollection Services { get; } = services;

    /// <summary>The client's section, or <see langword="null"/>.</summary>
    internal IConfigurationSection? Section { get; } = section;

    /// <summary>The <c>Configure</c> transforms, in call order (CFG-1's fourth tier).</summary>
    internal List<Func<DexpaceClientOptions, DexpaceClientOptions>> OptionsTransforms { get; } = [];

    /// <summary>The <c>ConfigureTransport</c> transforms, in call order.</summary>
    internal List<Func<SystemNetHttpClientOptions, SystemNetHttpClientOptions>> TransportTransforms { get; } = [];

    /// <summary>The <c>ConfigureCredentials</c> transforms, in call order.</summary>
    internal List<Func<IServiceProvider, AuthCredentials, AuthCredentials>> CredentialTransforms { get; } = [];

    /// <summary>The transport's slot: the package's SystemNet default, or the explicit install that replaced it (SEAM-5, SEAM-6).</summary>
    internal ServiceSlot Transport { get; } = new("transport");

    /// <summary>The codec's slot: there is no default, so it starts empty (SEAM-5).</summary>
    internal ServiceSlot Serde { get; } = new("codec");

    /// <summary>The auth descriptor the package builds a <c>MultiSchemeAuthPolicy</c> from, or <see langword="null"/>.</summary>
    internal AuthDescriptor? AuthDescriptor { get; set; }

    /// <summary>The factory of a caller's own auth policy, or <see langword="null"/>.</summary>
    internal Func<IServiceProvider, HttpPipelinePolicy>? AuthPolicyFactory { get; set; }

    /// <summary>The caller's extra policies, in call order (PIPE-35).</summary>
    internal List<Func<IServiceProvider, HttpPipelinePolicy>> Policies { get; } = [];

    /// <summary>
    /// Resolves the proxy the caller asked for from the environment, or <see langword="null"/> when none was asked for (CFG-28, P9-15). Set only
    /// by the two <c>UseProxyFromEnvironment</c> overloads; resolved when the primary handler is built, never at registration.
    /// </summary>
    internal Func<ILogger, ProxyOptions?>? EnvironmentProxy { get; set; }

    /// <summary>The builder this registration belongs to, so a second <c>AddDexpaceClient</c> for the name returns it.</summary>
    internal DexpaceClientBuilder? Builder { get; set; }
}
