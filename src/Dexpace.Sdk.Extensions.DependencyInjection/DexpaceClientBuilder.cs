// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Http.SystemNet;
using Microsoft.Extensions.DependencyInjection;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// Configures one named client registered by <see cref="DexpaceServiceCollectionExtensions.AddDexpaceClient(IServiceCollection, string)"/>
/// (CFG-1, CFG-37, SEAM-5, SEAM-6; P9-5, P9-6). Every method returns the builder, and <see cref="Services"/> chains back to the collection.
/// </summary>
/// <remarks>
/// A sealed class rather than an interface, so a third-party adapter extends it with extension methods. The transforms are applied after the
/// configuration values, in call order (CFG-1's fourth tier): the option records are immutable, so a transform returns a modified copy.
/// </remarks>
public sealed class DexpaceClientBuilder
{
    private readonly DexpaceClientRegistration _registration;

    internal DexpaceClientBuilder(DexpaceClientRegistration registration, IHttpClientBuilder httpClient)
    {
        _registration = registration;
        HttpClient = httpClient;
    }

    /// <summary>The client's name, which is also its service key.</summary>
    public string Name => _registration.Name;

    /// <summary>The collection the client was registered into.</summary>
    public IServiceCollection Services => _registration.Services;

    /// <summary>
    /// The <c>IHttpClientBuilder</c> of the client's handler chain, so <c>AddHttpMessageHandler</c> and <c>AddStandardResilienceHandler</c> compose
    /// underneath the transport. <c>ConfigureHttpClient</c> has no effect: the package uses the handler and never <c>CreateClient</c>. The
    /// package registers no retrying handler; to add the standard resilience handler without a second retry engine, set
    /// <c>Retry.ShouldHandle</c> to return <see langword="false"/> (XCUT-10).
    /// </summary>
    public IHttpClientBuilder HttpClient { get; }

    /// <summary>Applies <paramref name="configure"/> to the client options after the configuration values, in call order (CFG-1).</summary>
    /// <param name="configure">A transform returning the modified options; returning <see langword="null"/> fails validation naming the client.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public DexpaceClientBuilder Configure(Func<DexpaceClientOptions, DexpaceClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _registration.OptionsTransforms.Add(configure);
        return this;
    }

    /// <summary>Applies <paramref name="configure"/> to the transport options after the configuration values, in call order. Setting <c>Proxy</c> counts as an explicit proxy source.</summary>
    /// <param name="configure">A transform returning the modified options; the container supplies the logger and the clock.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public DexpaceClientBuilder ConfigureTransport(Func<SystemNetHttpClientOptions, SystemNetHttpClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _registration.TransportTransforms.Add(configure);
        return this;
    }

    /// <summary>Applies <paramref name="configure"/> to the credentials read from <c>Auth:*</c>, with the container available, for what configuration cannot hold (a <c>TokenCredential</c>).</summary>
    /// <param name="configure">A transform from the container and the configured credentials to the credentials to use.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public DexpaceClientBuilder ConfigureCredentials(Func<IServiceProvider, AuthCredentials, AuthCredentials> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _registration.CredentialTransforms.Add(configure);
        return this;
    }

    /// <summary>
    /// Resolves the proxy from the process environment (<c>HTTPS_PROXY</c>, <c>HTTP_PROXY</c>, <c>NO_PROXY</c>) when the client's transport is first
    /// resolved (CFG-28, P9-15). The one explicit environment read; nothing else in the package reads the environment, and no configuration value
    /// turns it on. A second proxy source (explicit <c>Transport:Proxy:*</c> keys) fails validation.
    /// </summary>
    /// <returns>This builder.</returns>
    public DexpaceClientBuilder UseProxyFromEnvironment()
    {
        _registration.EnvironmentProxy = static logger => ProxyOptions.FromEnvironment(logger);
        return this;
    }

    /// <summary>
    /// Resolves the proxy through <paramref name="lookup"/> when the client's transport is first resolved, for example
    /// <c>key =&gt; configuration[key]</c> to serve <c>HTTPS_PROXY</c> from any configuration provider (CFG-11, CFG-28).
    /// </summary>
    /// <param name="lookup">Returns the value of a variable name, or <see langword="null"/>.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lookup"/> is <see langword="null"/>.</exception>
    public DexpaceClientBuilder UseProxyFromEnvironment(Func<string, string?> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        _registration.EnvironmentProxy = logger => ProxyOptions.FromEnvironment(lookup, logger);
        return this;
    }

    /// <summary>
    /// Installs <paramref name="transport"/> as the client's transport, replacing the package's default (SEAM-5). The container never disposes an
    /// instance it did not create (XCUT-22). Installing the same instance again is a no-op; a different one throws (SEAM-6).
    /// </summary>
    /// <param name="transport">The transport.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A different transport is already installed for this client.</exception>
    public DexpaceClientBuilder UseTransport(IAsyncHttpClient transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        _registration.Transport.Install(
            _registration,
            transport,
            transport.GetType().FullName ?? transport.GetType().Name,
            ServiceDescriptor.KeyedSingleton<IAsyncHttpClient>(Name, transport));
        return this;
    }

    /// <summary>
    /// Installs the transport <paramref name="factory"/> builds, replacing the package's default (SEAM-5). The factory runs on the root provider and the
    /// container disposes what it returns. The same delegate again is a no-op; a different one throws (SEAM-6).
    /// </summary>
    /// <param name="factory">Builds the transport from the container.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A different transport is already installed for this client.</exception>
    public DexpaceClientBuilder UseTransport(Func<IServiceProvider, IAsyncHttpClient> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _registration.Transport.Install(
            _registration,
            factory,
            ImplementationNames.Describe(factory),
            ServiceDescriptor.KeyedSingleton<IAsyncHttpClient>(Name, (provider, _) => factory(provider)));
        return this;
    }

    /// <summary>
    /// Installs <paramref name="serde"/> as the client's codec (SEAM-5). A codec is required: the package has no default, so a client without one fails
    /// validation with an install hint. The container never disposes an instance it did not create.
    /// </summary>
    /// <param name="serde">The codec.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="serde"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A different codec is already installed for this client.</exception>
    public DexpaceClientBuilder UseSerde(ISerde serde)
    {
        ArgumentNullException.ThrowIfNull(serde);
        _registration.Serde.Install(
            _registration,
            serde,
            serde.GetType().FullName ?? serde.GetType().Name,
            ServiceDescriptor.KeyedSingleton<ISerde>(Name, serde));
        return this;
    }

    /// <summary>Installs the codec <paramref name="factory"/> builds (SEAM-5). The factory runs on the root provider and the container disposes what it returns.</summary>
    /// <param name="factory">Builds the codec from the container.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A different codec is already installed for this client.</exception>
    public DexpaceClientBuilder UseSerde(Func<IServiceProvider, ISerde> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _registration.Serde.Install(
            _registration,
            factory,
            ImplementationNames.Describe(factory),
            ServiceDescriptor.KeyedSingleton<ISerde>(Name, (provider, _) => factory(provider)));
        return this;
    }

    /// <summary>
    /// Installs a <c>MultiSchemeAuthPolicy</c> over <paramref name="descriptor"/> and the credentials read from <c>Auth:*</c> (P9-18). The descriptor
    /// is what a generated SDK owns. Cannot be combined with <see cref="UseAuthPolicy"/>.
    /// </summary>
    /// <param name="descriptor">The client's auth requirements.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="UseAuthPolicy"/> was called, or a different descriptor is already installed.</exception>
    public DexpaceClientBuilder UseAuth(AuthDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (_registration.AuthPolicyFactory is not null)
        {
            throw new InvalidOperationException("Client '" + Name + "' already installs an auth policy with UseAuthPolicy; UseAuth and UseAuthPolicy cannot be combined (SEAM-6).");
        }

        if (_registration.AuthDescriptor is { } incumbent && !incumbent.Equals(descriptor))
        {
            throw new InvalidOperationException("Client '" + Name + "' already has a different auth descriptor (SEAM-6).");
        }

        _registration.AuthDescriptor = descriptor;
        return this;
    }

    /// <summary>Installs the auth policy <paramref name="factory"/> builds (for example a <c>ChallengeAuthPolicy</c>). Cannot be combined with <see cref="UseAuth"/>.</summary>
    /// <param name="factory">Builds the policy from the container.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><see cref="UseAuth"/> was called, or a different policy factory is already installed.</exception>
    public DexpaceClientBuilder UseAuthPolicy(Func<IServiceProvider, HttpPipelinePolicy> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (_registration.AuthDescriptor is not null)
        {
            throw new InvalidOperationException("Client '" + Name + "' already installs an auth policy with UseAuth; UseAuth and UseAuthPolicy cannot be combined (SEAM-6).");
        }

        if (_registration.AuthPolicyFactory is { } incumbent && !incumbent.Equals(factory))
        {
            throw new InvalidOperationException("Client '" + Name + "' already has a different auth policy factory (SEAM-6).");
        }

        _registration.AuthPolicyFactory = factory;
        return this;
    }

    /// <summary>Adds a policy built by <paramref name="factory"/> to the client's pipeline; the stage sort places it (PIPE-35).</summary>
    /// <param name="factory">Builds the policy from the container.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public DexpaceClientBuilder AddPolicy(Func<IServiceProvider, HttpPipelinePolicy> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _registration.Policies.Add(factory);
        return this;
    }
}
