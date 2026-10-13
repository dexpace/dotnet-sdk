// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net.Http;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Http.SystemNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// The pieces of one named client, built from the container (P9-5, P9-12, P9-13, P9-14). Every factory reads the validated
/// <see cref="DexpaceClientState"/> first, so a client that fails validation fails the resolution of each of its keyed services (V4, design D).
/// </summary>
internal static class NamedClient
{
    private const string HandlerNamePrefix = "Dexpace.";
    private const string PipelineCategory = "Dexpace.Sdk";
    private const string TransportCategory = "Dexpace.Sdk.Http.SystemNet";

    /// <summary>Registers the named <c>HttpClient</c> whose handler chain is the transport's (E): a redirect-off primary, no factory loggers, an infinite lifetime.</summary>
    internal static IHttpClientBuilder AddHandlerChain(DexpaceClientRegistration registration)
    {
        var name = registration.Name;
        return registration.Services
            .AddHttpClient(HandlerNamePrefix + name)
            .ConfigurePrimaryHttpMessageHandler(provider => CreatePrimary(provider, registration))
            .RemoveAllLoggers()
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
    }

    /// <summary>Registers the keyed lease, default transport, options and pipeline of the client.</summary>
    internal static void AddKeyedServices(DexpaceClientRegistration registration)
    {
        var name = registration.Name;
        var services = registration.Services;
        services.AddKeyedSingleton(name, static (_, _) => new PrimaryHandlerLease());
        var transport = ServiceDescriptor.KeyedSingleton<IAsyncHttpClient>(name, (provider, _) => CreateTransport(provider, registration));
        services.Add(transport);
        registration.Transport.Descriptor = transport;
        services.AddKeyedSingleton(name, (provider, _) => StateOf(provider, name).Options);
        services.AddKeyedSingleton(name, (provider, _) => CreatePipeline(provider, registration));
    }

    private static DexpaceClientState StateOf(IServiceProvider provider, string name) =>
        provider.GetRequiredService<IOptionsMonitor<DexpaceClientState>>().Get(name);

    private static ILogger LoggerFor(IServiceProvider provider, string category) =>
        provider.GetRequiredService<ILoggerFactory>().CreateLogger(category);

    private static TimeProvider ClockOf(IServiceProvider provider) => provider.GetService<TimeProvider>() ?? TimeProvider.System;

    private static SocketsHttpHandler CreatePrimary(IServiceProvider provider, DexpaceClientRegistration registration)
    {
        var state = StateOf(provider, registration.Name);
        var logger = LoggerFor(provider, TransportCategory);

        // CFG-28: the environment is read only when the caller asked, and only here, when the primary is built (P9-15).
        var proxy = registration.EnvironmentProxy is { } environment ? environment(logger) : state.Transport.Proxy;
        var handler = SystemNetHttpClient.CreatePrimaryHandler(state.Transport with { Proxy = proxy, Logger = logger, TimeProvider = ClockOf(provider) });
        handler.PooledConnectionLifetime = state.PooledConnectionLifetime;
        provider.GetRequiredKeyedService<PrimaryHandlerLease>(registration.Name).Track(handler);
        return handler;
    }

    private static SystemNetHttpClient CreateTransport(IServiceProvider provider, DexpaceClientRegistration registration)
    {
        // The lease first: it is created, so disposed, after the transport (V8); then the validated state; then the chain.
        _ = provider.GetRequiredKeyedService<PrimaryHandlerLease>(registration.Name);
        var state = StateOf(provider, registration.Name);
        var chain = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(HandlerNamePrefix + registration.Name);
        var options = state.Transport with { Proxy = null, Logger = LoggerFor(provider, TransportCategory), TimeProvider = ClockOf(provider) };
        return new SystemNetHttpClient(chain, options);
    }

    private static HttpPipeline CreatePipeline(IServiceProvider provider, DexpaceClientRegistration registration)
    {
        var state = StateOf(provider, registration.Name);
        var transport = provider.GetRequiredKeyedService<IAsyncHttpClient>(registration.Name);
        var clock = ClockOf(provider);
        var auth = CreateAuthPolicy(provider, registration, state, clock);
#pragma warning disable CA2000 // The seed pipeline owns nothing (PIPE-27): it is flattened into the builder and dropped, never disposed.
        var seed = DexpacePipeline.CreateDefault(transport, auth, LoggerFor(provider, PipelineCategory), clock);
#pragma warning restore CA2000
        var builder = PipelineBuilder.Flatten(seed);
        foreach (var policy in registration.Policies)
        {
            builder.Add(policy(provider));
        }

        return builder.Build(transport, state.Options);
    }

    private static HttpPipelinePolicy? CreateAuthPolicy(IServiceProvider provider, DexpaceClientRegistration registration, DexpaceClientState state, TimeProvider clock)
    {
        if (registration.AuthPolicyFactory is { } factory)
        {
            return factory(provider);
        }

        return registration.AuthDescriptor is { } descriptor
            ? new MultiSchemeAuthPolicy(descriptor, state.Credentials ?? new Dexpace.Sdk.Core.Auth.AuthCredentials(), clock)
            : null;
    }
}
