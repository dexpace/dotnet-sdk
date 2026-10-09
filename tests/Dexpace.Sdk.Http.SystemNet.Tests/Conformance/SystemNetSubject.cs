// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Conformance;

/// <summary>
/// The reference transport as a conformance subject (phase 8a, P8a-22): the owned form on both faces, and every capability
/// hook that can be built test-side over a borrowed <c>HttpClient</c>, so phase 8a changes no line of
/// <c>Dexpace.Sdk.Http.SystemNet</c>. The one hook it does not supply is <c>CreateWithProxy</c>: the transport has no
/// proxy-taking construction until phase 8b, so <c>transport-30</c> is <c>NotExercised</c>.
/// </summary>
internal static class SystemNetSubject
{
    // The owned transport with a logger (SystemNetHttpClient(ILogger)) builds its handler with the default proxy, which honours
    // HTTP_PROXY and http_proxy. The wire tests pin that per instance (UseProxy = false through an internal constructor that
    // takes no logger); the kit needs the logger, so the process-wide default proxy is pinned to "direct" instead, once, before
    // any owned transport sends: the fixture's socket must be the fixture's whatever the environment says. Nothing else in this
    // test project relies on a default proxy (every wire test sets UseProxy = false).
    static SystemNetSubject() => HttpClient.DefaultProxy = new WebProxy();

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
#pragma warning restore CA2000

    /// <summary>
    /// The bounds of the driver and the waivers of phase 8a's first run against the reference transport: exactly the failing set,
    /// each a requirement owned by phase 8b, each named for the fact that explains it. <c>TRANSPORT-2</c>'s is narrowed to the
    /// body-less assertion (the single-use clause passes), as is <c>TRANSPORT-14</c>'s to its name clause. Phase 8b closes a row
    /// by deleting its waiver; a waiver left behind after its row is fixed fails the run.
    /// </summary>
    internal static TransportSuiteOptions Options { get; } = new()
    {
        AssertionTimeout = TimeSpan.FromSeconds(20),
        ReleaseTimeout = TimeSpan.FromSeconds(5),
        Waivers =
        [
            new("TRANSPORT-2", "SocketsHttpHandler has no public switch for its own connection-failure retry: a body-less request reset before any response byte is re-sent up to three more times, on a pooled connection and on a new one, so the pipeline's retry is not the only one; 8b finds a handler-level way to turn it off or records the clause as unmet on a stated domain (a design section 10 entry)")
            {
                Owner = "8b",
                Assertion = "transport-2.bodyless-not-resent",
            },
            new("TRANSPORT-4", "RequestOptions.Timeout is accepted and read by nothing until 8b wires the per-call deadline (SystemNetHttpClientTests.Options_are_accepted_and_ignored_until_8b)") { Owner = "8b" },
            new("TRANSPORT-5", "RequestOptions.Timeout is ignored until 8b: a call to a server that never answers is not cut") { Owner = "8b" },
            new("TRANSPORT-6", "RequestOptions.Timeout is ignored until 8b, so a sub-resolution timeout cannot be clamped either") { Owner = "8b" },
            new("TRANSPORT-8", "F3: an internal cancel of a borrowed HttpClient (CancelPendingRequests, Dispose) is reported as a retryable ServiceRequestTimeoutException; 8b classifies by state first") { Owner = "8b" },
            new("TRANSPORT-10", "F7: RequestBodyContent stamps the body's media type and the caller's Content-Type header is skipped, so the body's type wins on the wire") { Owner = "8b" },
            new("TRANSPORT-12", "F7: a content-class header (Content-Language, Content-Encoding, Allow) on a body-less request is dropped without a log entry") { Owner = "8b" },
            new("TRANSPORT-13", "no drop-logging policy yet: framing drops log at Debug every time and a refused header is not logged (phase 1 and 5b hand-offs: OBS-19 and TRANSPORT-13 are 8b's)") { Owner = "8b" },
            new("TRANSPORT-14", "F1: SocketsHttpHandler fails the whole response on a malformed header name before the adapter sees it; 8b dispositions it (a design section 10 entry if no handler-level workaround exists)")
            {
                Owner = "8b",
                Assertion = "transport-14.name-malformed-dropped",
            },
            new("TRANSPORT-18", "a native re-send of a single-use body fails with StreamConsumedException instead of replaying identical bytes (design section 11 item 18); 8b's bounded capture closes it") { Owner = "8b" },
            new("SEAM-15", "a transport over a borrowed HttpClient does not throw ObjectDisposedException after dispose until 8b's latch for borrowed clients; the owned transport already does")
            {
                Owner = "8b",
                Face = TransportFace.Async,
            },
        ],
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
    /// Takes the live response from the inner handler and swaps its content for one that fails the length computation the
    /// adapter does, so adapting it throws after the response is live. The failing content disposes the original content, which
    /// owns the connection, so a transport that disposes the response on a failed adaptation releases it (TRANSPORT-22).
    /// </summary>
    private sealed class FaultingAdaptationHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var live = await base.SendAsync(request, cancellationToken);
            live.Content = new ExplodingContent(live.Content);
            return live;
        }
    }

    private sealed class ExplodingContent(HttpContent original) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

        protected override bool TryComputeLength(out long length) => throw new InvalidOperationException("adaptation failed");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                original.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>Sends every request twice through the inner handler, the native re-send of design section 11 item 18.</summary>
    private sealed class ResendingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using var first = await base.SendAsync(request, cancellationToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
