// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Conformance;

/// <summary>
/// The reference transport as conformance subjects: the owned form (<see cref="Create"/>) and the handler-taking form
/// (<see cref="CreateViaHandler"/>), each on both faces with every capability hook that can be built test-side. Phase 8b
/// supplies <c>CreateWithProxy</c> on both, so <c>transport-30</c> is exercised rather than <c>NotExercised</c>.
/// </summary>
internal static class SystemNetSubject
{
    /// <summary>The names of the two subjects, the theory key the driver passes around.</summary>
    internal static string[] Kinds { get; } = ["owned", "handler"];

    /// <summary>The subject for <paramref name="kind"/>, one of <see cref="Kinds"/>.</summary>
    internal static TransportSubject For(string kind) => kind switch
    {
        "owned" => Create(),
        "handler" => CreateViaHandler(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown subject kind."),
    };

    /// <summary>The owned transport on both faces, with the hooks.</summary>
    internal static TransportSubject Create() => new()
    {
        Name = "Dexpace.Sdk.Http.SystemNet",
        CreateAsync = settings => new SystemNetHttpClient(settings.Logger),
        CreateBlocking = settings => new SystemNetHttpClient(settings.Logger),
        CreateBorrowed = Borrowed,
        CreateWithFaultingAdaptation = settings => Over(settings, inner => new FaultingAdaptationHandler(inner)),
        CreateWithInternalCancel = InternalCancel,
        CreateWithNativeResend = settings => Over(settings, inner => new ResendingHandler(inner)),
        CreateWithProxy = (settings, proxy) => new SystemNetHttpClient(new SystemNetHttpClientOptions { Logger = settings.Logger, Proxy = proxy }),
        AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,
    };

    /// <summary>
    /// The handler-taking construction on both faces (phase 8b, P8b-3): the transport builds an <c>HttpClient</c> over a caller's
    /// <c>SocketsHttpHandler</c> and borrows the handler, so the subject's transport also disposes the handler it was given (the
    /// caller's job, here the wrapper's). Its proxy hook configures the caller's handler, because a handler plus
    /// <c>SystemNetHttpClientOptions.Proxy</c> is refused.
    /// </summary>
    internal static TransportSubject CreateViaHandler() => new()
    {
        Name = "Dexpace.Sdk.Http.SystemNet (handler constructor)",
        CreateAsync = settings => OverHandler(settings),
        CreateBlocking = settings => OverHandler(settings),
        CreateBorrowed = Borrowed,
        CreateWithFaultingAdaptation = settings => OverHandler(settings, inner => new FaultingAdaptationHandler(inner)),
        CreateWithInternalCancel = HandlerInternalCancel,
        CreateWithNativeResend = settings => OverHandler(settings, inner => new ResendingHandler(inner)),
        CreateWithProxy = HandlerProxy,
        AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,
    };

    // A native client with no proxy (the socket must be the fixture's) and no redirect following (TRANSPORT-1: the SDK is the
    // only redirect authority), optionally behind a handler that changes what the transport sees.
#pragma warning disable CA2000 // Ownership passes to NativeBackedTransport, which disposes the client with the transport.
    private static NativeBackedTransport Over(TransportSettings settings, Func<HttpMessageHandler, DelegatingHandler>? wrap = null)
    {
        HttpMessageHandler handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        var native = new HttpClient(wrap is null ? handler : wrap(handler));
        return new NativeBackedTransport(native, new SystemNetHttpClient(native, settings.Logger));
    }

    private static BorrowedTransport Borrowed(TransportSettings settings)
    {
        var native = new HttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
        return new BorrowedTransport(
            new SystemNetHttpClient(native, settings.Logger),
            async (url, cancellationToken) =>
            {
                using var response = await native.GetAsync(url, cancellationToken);
                _ = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            },
            new HttpClientOwner(native));
    }

    private static InternalCancellation InternalCancel(TransportSettings settings)
    {
        var transport = Over(settings);
        return new InternalCancellation(transport, transport.Native.CancelPendingRequests);
    }

    private static HandlerBackedTransport OverHandler(TransportSettings settings, Func<HttpMessageHandler, DelegatingHandler>? wrap = null)
    {
        HttpMessageHandler primary = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        return HandlerBacked(primary, wrap, settings);
    }

    private static HandlerBackedTransport HandlerBacked(HttpMessageHandler primary, Func<HttpMessageHandler, DelegatingHandler>? wrap, TransportSettings settings)
    {
        var chain = wrap is null ? primary : wrap(primary);
        return new HandlerBackedTransport(chain, new SystemNetHttpClient(chain, new SystemNetHttpClientOptions { Logger = settings.Logger }));
    }

    // TRANSPORT-8 on the handler construction: the cancellation comes from inside the handler chain, not from the caller's token
    // and not from a deadline, the same shape HttpClient.CancelPendingRequests produces on a borrowed client.
    private static InternalCancellation HandlerInternalCancel(TransportSettings settings)
    {
        CancelOnDemandHandler? trigger = null;
        var transport = OverHandler(settings, inner => trigger = new CancelOnDemandHandler(inner));
        return new InternalCancellation(transport, trigger!.CancelInFlight);
    }

    // TRANSPORT-30 on a handler the caller owns: a test-local WebProxy with credentials, since a handler plus SystemNetHttpClientOptions.Proxy is refused.
    private static HandlerBackedTransport HandlerProxy(TransportSettings settings, ProxyOptions proxy)
    {
        var webProxy = new WebProxy(new Uri($"http://{proxy.Host}:{proxy.Port}/")) { Credentials = new NetworkCredential(proxy.UserName, proxy.Password) };
        HttpMessageHandler primary = new SocketsHttpHandler { UseProxy = true, Proxy = webProxy, AllowAutoRedirect = false };
        return HandlerBacked(primary, wrap: null, settings);
    }
#pragma warning restore CA2000

    /// <summary>
    /// The bounds of the driver and its permanent waivers, per subject (phase 8b, P8b-14, P8b-T2; phase 11, P11-12). Phase 8b deleted every
    /// waiver that carried <c>Owner = "8b"</c> in the change that closed its row; what remains is a decision, narrowed to the one assertion it
    /// covers. <c>TRANSPORT-14</c>'s malformed-name clause (design section 10 entry 32) is waived for both subjects. <c>TRANSPORT-2</c>'s
    /// body-less clause is met by the owned construction since phase 11, because the handler the SDK builds carries the stop filter, so
    /// only the handler-taking subject keeps its waiver: it is given a <c>SocketsHttpHandler</c> the caller built, whose native retry is the
    /// caller's (design section 11 item 71). A waiver whose assertion starts to pass fails the run.
    /// </summary>
    /// <param name="kind">One of <see cref="Kinds"/>.</param>
    internal static TransportSuiteOptions OptionsFor(string kind) => kind switch
    {
        "owned" => s_ownedOptions,
        "handler" => s_handlerOptions,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown subject kind."),
    };

    private static readonly ConformanceWaiver s_malformedName = new("TRANSPORT-14", "SocketsHttpHandler rejects the whole response on a malformed header name (a space, a control byte or a non-ASCII byte) before the adapter sees any header, and has no leniency switch; the only repair is a hand-written HTTP/1.1 head rewriter in the byte path of every response, a second parser ahead of the runtime's, which the transport declines. Unmet on that stated domain: design section 10 entry 32 (the value and obs-text clauses are met and unwaived)")
    {
        Assertion = "transport-14.name-malformed-dropped",
    };

    private static readonly ConformanceWaiver s_callersHandlerRetry = new("TRANSPORT-2", "A handler the caller built is the caller's native client: SocketsHttpHandler has no public switch for its own connection-failure retry, and re-sends a request with no content whose connection ends before any response byte up to three more times. The handler the SDK builds (SystemNetHttpClient.CreatePrimaryHandler, which the owned construction and the DI named client use) carries a pass-through stream filter that stops it, so the owned subject is unwaived; this subject borrows a SocketsHttpHandler it does not build and never changes, so the retry stays the caller's. Outside TRANSPORT-2's 'SDK-managed transport': design section 11 item 71")
    {
        Assertion = "transport-2.bodyless-not-resent",
    };

    private static readonly TransportSuiteOptions s_ownedOptions = new()
    {
        AssertionTimeout = TimeSpan.FromSeconds(20),
        ReleaseTimeout = TimeSpan.FromSeconds(5),
        Waivers = [s_malformedName],
    };

    private static readonly TransportSuiteOptions s_handlerOptions = new()
    {
        AssertionTimeout = TimeSpan.FromSeconds(20),
        ReleaseTimeout = TimeSpan.FromSeconds(5),
        Waivers = [s_callersHandlerRetry, s_malformedName],
    };

    /// <summary>A transport that disposes the native client it was built over together with itself, so a hook's client does not outlive its run.</summary>
    internal sealed class NativeBackedTransport(HttpClient native, SystemNetHttpClient inner) : IAsyncHttpClient
    {
        internal HttpClient Native { get; } = native;

        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            inner.ExecuteAsync(request, options, cancellationToken);

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            Native.Dispose();
        }
    }

    /// <summary>Releases a borrowed <c>HttpClient</c> when the suite is done with it.</summary>
    internal sealed class HttpClientOwner(HttpClient native) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            native.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// A transport over a caller's handler chain on both faces, which also releases that chain when it is disposed: the handler
    /// is the caller's, and here the caller is the driver, so an idle pooled connection does not outlive its run.
    /// </summary>
    internal sealed class HandlerBackedTransport(HttpMessageHandler chain, SystemNetHttpClient inner) : IAsyncHttpClient, IHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            inner.ExecuteAsync(request, options, cancellationToken);

        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            inner.Execute(request, options, cancellationToken);

        public void Dispose()
        {
            inner.Dispose();
            chain.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            chain.Dispose();
        }
    }
}
