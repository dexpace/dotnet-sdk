// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Concurrent;
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests.Conformance;

/// <summary>
/// The invariant suite's subject for the dependency-injection package (phase 10, P10-5): the pipeline is the keyed <see cref="HttpPipeline"/> of
/// <c>AddDexpaceClient</c> over the transport each assertion builds (the options pass through <c>Configure</c>, the run's recording clock and the kit's
/// logger through the container), and <c>SeamResolution</c> is the package's own single-registration check (<c>XCUT-23</c>, P9-16): no codec fails the
/// start with the install hint, a different second install fails the call naming both, an explicit install wins. The containers are disposed with the
/// factory, asynchronously (a synchronous dispose throws for an async-only transport).
/// </summary>
internal sealed class InvariantSubjectFactory : IAsyncDisposable
{
    private const string Name = "kit";
    private readonly ConcurrentBag<ServiceProvider> _providers = [];

    /// <summary>The assertions this driver leaves not exercised, and why: they reach core internals only the core driver can supply (P10-28, plan reading R13).</summary>
    internal static IReadOnlyList<string> ExpectedNotExercised { get; } =
    [
        "xcut-14.bounded-under-burst",
        "xcut-14.drains-after-insert",
        "xcut-8.strict-factory-rejects",
        "xcut-9.cycle-safe-walk",
    ];

    /// <summary>Bounds the suite sensibly on a slow runner.</summary>
    internal static SuiteOptions Options { get; } = new() { AssertionTimeout = TimeSpan.FromSeconds(30) };

    /// <summary>The package's seam, as the three situations of <c>XCUT-23</c>; each builds and disposes its own container.</summary>
    internal static SeamResolution Resolution { get; } = new(
        ResolveWithNone: static () =>
        {
            var services = new ServiceCollection();
            _ = services.AddDexpaceClient(Name, Config.Section());
            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<IStartupValidator>().Validate();
        },
        ResolveWithTwo: static () =>
        {
            var builder = TestClients.Add(new ServiceCollection(), Name).UseTransport(new ScriptedTransport());
            _ = builder.UseTransport(new AsyncOnlyTransport());
        },
        ExplicitWins: static () =>
        {
            var explicitTransport = new ScriptedTransport();
            var services = new ServiceCollection();
            _ = TestClients.Add(services, Name).UseTransport(explicitTransport);
            using var provider = services.BuildServiceProvider();
            return ReferenceEquals(explicitTransport, provider.GetRequiredKeyedService<IAsyncHttpClient>(Name));
        });

    /// <summary>The subject: the DI-built pipeline, and the package's seam.</summary>
    internal InvariantSubject Create() =>
        new()
        {
            Name = "Dexpace.Sdk.Extensions.DependencyInjection (named client)",
            CreatePipeline = Build,
            SeamResolution = Resolution,
        };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var provider in _providers)
        {
            await provider.DisposeAsync();
        }
    }

    private HttpPipeline Build(IAsyncHttpClient transport, DexpaceClientOptions options, TimeProvider time, ILogger logger)
    {
        var services = new ServiceCollection();
        _ = TestClients.Add(services, Name).UseTransport(transport).Configure(_ => options);
        services.Replace(ServiceDescriptor.Singleton(time));
        services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(new SingleLoggerFactory(logger)));
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        _providers.Add(provider);
        return provider.GetRequiredKeyedService<HttpPipeline>(Name);
    }

    /// <summary>Hands every category the one logger the kit supplies, so the logging assertions see what the package's pipeline emits.</summary>
    private sealed class SingleLoggerFactory(ILogger logger) : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => logger;

        public void Dispose()
        {
        }
    }
}
