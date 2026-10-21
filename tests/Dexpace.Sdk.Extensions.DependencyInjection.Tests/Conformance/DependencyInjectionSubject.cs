// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Http.SystemNet.Tests.Conformance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests.Conformance;

/// <summary>
/// The transport <c>AddDexpaceClient</c> builds from <c>IHttpClientFactory</c> as a conformance subject (phase 9, P9-22; the 8a hand-off): a fresh container per
/// transport, the kit's logger forwarded through a single-logger factory, the hooks added as handlers of the named client and the proxy hook through the
/// <c>Transport:Proxy:*</c> keys, so <c>CreatePrimaryHandler</c>'s proxy path is exercised. <c>CreateBorrowed</c> is not supplied: the package never borrows a
/// client, so <c>transport-15.borrowed-survives</c> stays <c>NotExercised</c>.
/// </summary>
internal static class DependencyInjectionSubject
{
    private const string Name = "kit";

    /// <summary>The subject, on both faces, with the hooks.</summary>
    internal static TransportSubject Create() => new()
    {
        Name = "Dexpace.Sdk.Extensions.DependencyInjection (named client)",
        CreateAsync = settings => Build(settings),
        CreateBlocking = settings => Build(settings),
        CreateWithFaultingAdaptation = settings => Build(settings, static b => b.HttpClient.AddHttpMessageHandler(static () => new FaultingAdaptationHandler())),
        CreateWithInternalCancel = InternalCancel,
        CreateWithNativeResend = settings => Build(settings, static b => b.HttpClient.AddHttpMessageHandler(static () => new ResendingHandler())),
        CreateWithProxy = (settings, proxy) => Build(
            settings,
            configure: null,
            ("Transport:Proxy:Host", proxy.Host),
            ("Transport:Proxy:Port", proxy.Port.ToString(CultureInfo.InvariantCulture)),
            ("Transport:Proxy:UserName", proxy.UserName),
            ("Transport:Proxy:Password", proxy.Password)),
        AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,
    };

    /// <summary>
    /// The kit's bounds and the one permanent assertion-scoped waiver of the SystemNet drivers that applies here (design section 10 entry 32),
    /// turning stale if its assertion passes. <c>TRANSPORT-2</c>'s body-less clause is not waived: the named client is built over
    /// <c>SystemNetHttpClient.CreatePrimaryHandler</c>, whose stop filter ends the native re-send (phase 11, P11-12).
    /// </summary>
    internal static TransportSuiteOptions Options { get; } = new()
    {
        AssertionTimeout = TimeSpan.FromSeconds(20),
        ReleaseTimeout = TimeSpan.FromSeconds(5),
        Waivers =
        [
            new("TRANSPORT-14", "SocketsHttpHandler rejects the whole response on a malformed header name (a space, a control byte or a non-ASCII byte) before the adapter sees any header, and has no leniency switch; the only repair is a hand-written HTTP/1.1 head rewriter in the byte path of every response, a second parser ahead of the runtime's, which the transport declines. Unmet on that stated domain: design section 10 entry 32 (the value and obs-text clauses are met and unwaived)")
            {
                Assertion = "transport-14.name-malformed-dropped",
            },
        ],
    };

    private static InternalCancellation InternalCancel(TransportSettings settings)
    {
        CancelOnDemandHandler? trigger = null;
        var transport = Build(settings, b => b.HttpClient.AddHttpMessageHandler(() => trigger = new CancelOnDemandHandler()));
        return new InternalCancellation(transport, () => trigger!.CancelInFlight());
    }

    private static ProviderBackedTransport Build(TransportSettings settings, Action<DexpaceClientBuilder>? configure = null, params (string Key, string? Value)[] values)
    {
        var services = new ServiceCollection();
        var builder = TestClients.Add(services, Name, values.Where(static v => v.Value is not null).ToArray());
        configure?.Invoke(builder);
        services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(new SingleLoggerFactory(settings.Logger)));
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        return new ProviderBackedTransport(provider, (Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient)provider.GetRequiredKeyedService<IAsyncHttpClient>(Name));
    }

    /// <summary>A transport that disposes its container with itself, so the pooled connections do not outlive the run (P9-13).</summary>
    private sealed class ProviderBackedTransport(ServiceProvider provider, Dexpace.Sdk.Http.SystemNet.SystemNetHttpClient inner) : IAsyncHttpClient, IHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            inner.ExecuteAsync(request, options, cancellationToken);

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            inner.Execute(request, options, cancellationToken);

        public void Dispose() => provider.Dispose();

        public ValueTask DisposeAsync() => provider.DisposeAsync();
    }

    /// <summary>Hands every category the one logger the kit supplies, so its assertions on header-drop and proxy events see them.</summary>
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
