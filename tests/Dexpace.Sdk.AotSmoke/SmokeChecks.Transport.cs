// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Linq;
using System.Net;
using System.Net.Sockets;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Http.SystemNet;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.AotSmoke;

// Phase 8b: the reference transport's hardening, in the published binary. The handler-taking construction, the real synchronous
// send, the per-call deadline and its classification, the header-drop policy and the proxy adapter (which the owned construction
// builds from a ProxyOptions) all run under trimming and NativeAOT with no reflection.
internal static partial class SmokeChecks
{
    private static async Task CheckPhase8bAsync()
    {
        var request = Request.Get(s_endpoint.OriginalString);

        // The handler constructor over a handler that implements both Send and SendAsync, on both faces.
        using var handler = new CannedHandler();
        await using var viaHandler = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions());
        using var syncResponse = viaHandler.Execute(request, RequestOptions.Empty, CancellationToken.None);
        using var asyncResponse = await viaHandler.ExecuteAsync(request, RequestOptions.Empty, CancellationToken.None);
        Expect(syncResponse.Status == Status.Created && asyncResponse.Status == Status.Created, "the handler constructor serves both faces");
        Expect(syncResponse.Headers.Get("X-Echo-Method") == "GET", "the real synchronous send maps response headers");

        // A deadline that fires is a retryable timeout, on both faces.
        using var silent = new SilentHandler();
        await using var timed = new SystemNetHttpClient(silent, new SystemNetHttpClientOptions { Timeout = TimeSpan.FromMilliseconds(50) });
        var asyncTimeout = await CaptureAsync(() => timed.ExecuteAsync(request, RequestOptions.Empty, CancellationToken.None));
        Expect(asyncTimeout is ServiceRequestTimeoutException { IsRetryable: true }, "an elapsed deadline is a retryable timeout (async)");
        var syncTimeout = await CaptureAsync(() => Task.Run(() => timed.Execute(request, new RequestOptions { Timeout = TimeSpan.FromMilliseconds(50) }, CancellationToken.None)));
        Expect(syncTimeout is ServiceRequestTimeoutException { IsRetryable: true }, "an elapsed deadline is a retryable timeout (sync)");

        // A dropped header is logged under the configured policy, by name.
        var logger = new SmokeLogger();
        await using var logging = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions { Logger = logger, HeaderDropLogging = HeaderDropLogging.Every });
        using var dropped = await logging.ExecuteAsync(request.WithHeader("Content-Language", "en"), RequestOptions.Empty, CancellationToken.None);
        Expect(logger.Events.Any(e => e.Id == DexpaceLogEvents.TransportHeaderDroppedId && e.Text.Contains("Content-Language", StringComparison.Ordinal)), "a header drop is logged under event 110");

        // The owned construction builds the proxy adapter and its Basic-only credentials from a ProxyOptions; a send through a proxy
        // nothing listens on fails as a transport failure, which proves the installed handler works under trimming.
        var closedPort = FreeLoopbackPort();
        var proxyOptions = new SystemNetHttpClientOptions { Proxy = new ProxyOptions { Host = "127.0.0.1", Port = closedPort, UserName = "smoke", Password = "smoke" } };
        await using var proxied = new SystemNetHttpClient(proxyOptions);
        var proxied1 = await CaptureAsync(() => proxied.ExecuteAsync(request, RequestOptions.Empty, CancellationToken.None));
        Expect(proxied1 is ServiceRequestException, "a send through an unreachable proxy is a transport failure");

        // After dispose every call throws.
        var disposed = new SystemNetHttpClient(handler, new SystemNetHttpClientOptions());
        await disposed.DisposeAsync();
        var afterDispose = await CaptureAsync(() => disposed.ExecuteAsync(request, RequestOptions.Empty, CancellationToken.None));
        Expect(afterDispose is ObjectDisposedException, "a call after dispose throws ObjectDisposedException");
    }

    private static async Task<Exception?> CaptureAsync(Func<Task<Response>> call)
    {
        try
        {
            using var response = await call();
            return null;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            return ex;
        }
    }

    private static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // Waits on the token it is given, on either face, so only a deadline or a cancellation ends the call.
    private sealed class SilentHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
