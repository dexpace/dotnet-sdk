// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// A transport that adapts <c>System.Net.Http.HttpClient</c> to the SDK's
/// <see cref="IAsyncHttpClient"/> and <see cref="IHttpClient"/> SPIs.
/// </summary>
/// <remarks>
/// <para>
/// The response body is delivered as a live stream (<see cref="HttpCompletionOption.ResponseHeadersRead"/>)
/// rather than buffered, so callers must dispose the returned <see cref="Response"/> to release the
/// connection.
/// </para>
/// <para>
/// <b>Ownership.</b> When constructed with a caller-supplied <c>HttpClient</c> the underlying client
/// is <em>not</em> disposed by this adapter; when constructed without one the adapter owns and disposes an
/// internally created client.
/// </para>
/// <para>
/// <b>Redirects (TRANSPORT-1).</b> The SDK pipeline's <c>RedirectPolicy</c> is the only redirect authority. The
/// internally created client never follows a redirect (<c>SocketsHttpHandler.AllowAutoRedirect</c> is
/// <see langword="false"/>), so a 3xx is returned as is. A caller-supplied client must be configured the same way:
/// if it follows a redirect anyway, the adapter disposes the response and throws a non-retryable
/// <see cref="SdkException"/> naming <c>AllowAutoRedirect</c>, because a credential may already have crossed an origin.
/// A followed redirect is detected when the final request URI differs from the one sent in scheme, host, port or
/// path; a handler that rewrites only the query or fragment (a <c>DelegatingHandler</c> adding <c>api-version</c>, for
/// example) is not mistaken for one. Two limits follow: a handler that itself rewrites the scheme, host, port or path
/// is reported as a redirect, and a redirect that ends on the URI originally sent goes undetected.
/// <b>Breaking</b> (phase 1): the parameterless constructor used to follow redirects, and a following caller-supplied
/// client used to succeed.
/// </para>
/// <para>
/// <b>Breaking</b> (phase 2b): <c>ExecuteAsync(Request, CancellationToken = default)</c> and <c>Execute(Request)</c> are
/// now <c>ExecuteAsync(Request, RequestOptions, CancellationToken)</c> and
/// <c>Execute(Request, RequestOptions, CancellationToken)</c>, with no parameter defaults. Callers that import
/// <c>Dexpace.Sdk.Core.Client</c> keep calling <c>ExecuteAsync(request, token)</c> through its option-less extension.
/// <see cref="RequestOptions"/> are ignored until phase 8b (SEAM-11), and the synchronous call is sync-over-async until
/// then.
/// </para>
/// <para>
/// <b>After dispose (SEAM-15).</b> An owned client throws <see cref="ObjectDisposedException"/> from every call, since
/// <c>HttpClient</c> does; a borrowed client keeps working, because it is never disposed. Disposal itself is idempotent
/// (SEAM-14, latched in 3b); phase 8b adds the <see cref="ObjectDisposedException"/> after dispose for both owned and
/// borrowed clients (SEAM-15).
/// </para>
/// <para>
/// <b>Outbound headers.</b> The framing headers the client computes itself — <c>Host</c>, <c>Content-Length</c>,
/// <c>Transfer-Encoding</c>, <c>Connection</c>, <c>Keep-Alive</c>, <c>Upgrade</c>, <c>TE</c> and <c>Expect</c> — are
/// dropped from the request, each with a <see cref="LogLevel.Debug"/> entry naming it (TRANSPORT-11). Every other
/// header is re-checked against the model's outbound rule at the wire boundary, and one that fails it is dropped
/// with a <see cref="LogLevel.Warning"/> naming it; values are never logged (XCUT-18, TRANSPORT-12).
/// </para>
/// <para>
/// <b>Inbound headers.</b> Response headers take the lenient inbound path (HTTP-19): obs-text is kept, and a header
/// carrying a control character is dropped. An unparseable <c>Content-Type</c> becomes "no media type"
/// (TRANSPORT-27), and if adapting the response throws, the native response is disposed first (TRANSPORT-22).
/// </para>
/// <para>
/// <b>Trace context (design §8.1, phase 5c P5c-11).</b> <c>InstrumentationPolicy</c> stamps the attempt span's
/// <c>traceparent</c> (and <c>tracestate</c>) on the request. When a <c>System.Net.Http</c> listener exists and runtime
/// propagation is on, this adapter drops that stamp, recognised by equality with <see cref="Activity.Current"/>'s id, so the
/// wire carries the runtime's recorded child span id; with no such listener the stamp stays and names the attempt span. A
/// <c>traceparent</c> the caller set that is not the current id is sent unchanged. The rule applies to owned and borrowed
/// clients alike; a borrowed client whose handler chain does not propagate (no <c>SocketsHttpHandler</c> at its root, or
/// an <c>ActivityHeadersPropagator</c> that injects nothing) sends no <c>traceparent</c> for a traced call while a
/// <c>System.Net.Http</c> listener exists. Enable the
/// <c>Dexpace.Sdk</c> meter or <c>System.Net.Http</c>'s, not both: each attempt is otherwise measured twice under
/// <c>http.client.request.duration</c>.
/// <b>Breaking</b> (phase 5c): the SDK's own <c>traceparent</c> no longer reaches the wire through this transport.
/// </para>
/// </remarks>
public sealed class SystemNetHttpClient : IAsyncHttpClient, IHttpClient
{
    // TRANSPORT-11: the framing set the native client computes from the body and the connection (design §3.2).
    private static readonly FrozenSet<string> s_framingHeaders = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "host",
        "content-length",
        "transfer-encoding",
        "connection",
        "keep-alive",
        "upgrade",
        "te",
        "expect");

    private static readonly Action<ILogger, string, Exception?> s_framingHeaderDropped = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(1, "FramingHeaderDropped"),
        "Dropped the caller-set '{HeaderName}' header: the transport computes it (TRANSPORT-11).");

    private static readonly Action<ILogger, string, Exception?> s_unsafeHeaderDropped = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "UnsafeHeaderDropped"),
        "Dropped the '{HeaderName}' request header: it is not valid on the wire (XCUT-18, TRANSPORT-12).");

    private static readonly Action<ILogger, string, Exception?> s_inboundHeaderDropped = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(3, "InboundHeaderDropped"),
        "Dropped the '{HeaderName}' response header: its value carries a control character (HTTP-19, XCUT-18).");

    private readonly SystemHttpClient _client;
    private readonly bool _ownsClient;
    private readonly ILogger _logger;
    private int _disposed;
    private int _releases;

    /// <summary>
    /// Creates a transport backed by an internally owned <c>HttpClient</c> that does not follow redirects.
    /// </summary>
    public SystemNetHttpClient()
        : this(CreateOwnedClient(), ownsClient: true, NullLogger.Instance)
    {
    }

    /// <summary>
    /// Creates a transport backed by an internally owned <c>HttpClient</c> that does not follow redirects, logging
    /// header drops to <paramref name="logger"/>.
    /// </summary>
    /// <param name="logger">
    /// Receives an entry per dropped header, naming it and never its value: <see cref="LogLevel.Debug"/> for a framing
    /// header the transport computes itself, <see cref="LogLevel.Warning"/> for a header that is not valid on the
    /// wire, and <see cref="LogLevel.Debug"/> for a response header dropped on the inbound path.
    /// </param>
    public SystemNetHttpClient(ILogger logger)
        : this(CreateOwnedClient(), ownsClient: true, logger)
    {
    }

    /// <summary>
    /// Creates a transport backed by a caller-supplied <c>HttpClient</c>. The supplied client is not
    /// disposed when this adapter is disposed, and its handler must not follow redirects: a followed redirect (a final
    /// request URI whose scheme, host, port or path differs from the one sent) fails the call (see remarks).
    /// </summary>
    /// <param name="client">The HTTP client to wrap.</param>
    public SystemNetHttpClient(SystemHttpClient client)
        : this(client, ownsClient: false, NullLogger.Instance)
    {
    }

    /// <summary>
    /// Creates a transport backed by a caller-supplied <c>HttpClient</c>, logging header drops to
    /// <paramref name="logger"/>. The supplied client is not disposed when this adapter is disposed, and its handler
    /// must not follow redirects: a followed redirect (a final request URI whose scheme, host, port or path differs
    /// from the one sent) fails the call (see remarks).
    /// </summary>
    /// <param name="client">The HTTP client to wrap.</param>
    /// <param name="logger">
    /// Receives an entry per dropped header, naming it and never its value: <see cref="LogLevel.Debug"/> for a framing
    /// header the transport computes itself, <see cref="LogLevel.Warning"/> for a header that is not valid on the
    /// wire, and <see cref="LogLevel.Debug"/> for a response header dropped on the inbound path.
    /// </param>
    public SystemNetHttpClient(SystemHttpClient client, ILogger logger)
        : this(client, ownsClient: false, logger)
    {
    }

    /// <summary>
    /// Test seam: the SDK-managed construction, with <paramref name="configureOwnedHandler"/> applied to the owned
    /// handler after the SDK's own settings, so a wire test can pin environment-dependent settings (the proxy) without
    /// leaving the owned-client path.
    /// </summary>
    internal SystemNetHttpClient(Action<SocketsHttpHandler> configureOwnedHandler)
        : this(CreateOwnedClient(configureOwnedHandler), ownsClient: true, NullLogger.Instance)
    {
    }

    private SystemNetHttpClient(SystemHttpClient client, bool ownsClient, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _ownsClient = ownsClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="RequestOptions"/> are accepted and read by nothing until phase 8b wires
    /// <see cref="RequestOptions.Timeout"/> (TRANSPORT-5), which SEAM-11 permits.
    /// </remarks>
    /// <exception cref="SdkException">
    /// A caller-supplied client followed a redirect itself (TRANSPORT-1): the final request URI differs from the one
    /// sent in scheme, host, port or path. The response has been disposed.
    /// </exception>
    public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        using var message = ToHttpRequestMessage(request);

        // The handler rewrites message.RequestUri in place when it follows a redirect, so keep what was sent.
        var sentUri = message.RequestUri!;
        HttpResponseMessage response;
        try
        {
            response = await _client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Cancellation not requested by the caller ⇒ the client's internal timeout fired.
            throw new ServiceRequestTimeoutException("The request timed out before a response was received.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ServiceRequestException("The request could not be sent to the server.", ex);
        }

        // TRANSPORT-22: once the native response is live, any throw while adapting it disposes it first.
        try
        {
            ThrowIfRedirected(sentUri, response);
            return ToResponse(response, request);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    // The transport's documented sync bridge (design §3.3). CA2000 reads the awaited Task as an undisposed
    // IDisposable; a Task needs no disposal (it holds no wait handle unless one is requested).
#pragma warning disable RS0030, CA2000
    public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        ExecuteAsync(request, options, cancellationToken).GetAwaiter().GetResult();
#pragma warning restore RS0030, CA2000

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_ownsClient)
        {
            Interlocked.Increment(ref _releases);
            _client.Dispose();
        }
    }

    // How many times the owned client was released (at most one, by the latch); lets a test see the latch, which the
    // HttpClient's own idempotent Dispose would otherwise hide.
    internal int OwnedClientReleases => Volatile.Read(ref _releases);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // TRANSPORT-1: the SDK-managed client never follows a redirect; the pipeline's RedirectPolicy does.
#pragma warning disable RS0030 // The transport's owned-client factory: the one sanctioned construction (styleguide 13.8).
    private static SystemHttpClient CreateOwnedClient(Action<SocketsHttpHandler>? configure = null)
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        configure?.Invoke(handler);
        return new(handler);
    }
#pragma warning restore RS0030

    // TRANSPORT-1's last line for a borrowed client (design §3.2, §6.2): after an automatic redirect the handler leaves
    // the final URI on RequestMessage.RequestUri. Only scheme, host, port and path are compared, in Uri's normalised
    // form (lower-case host, explicit port): a DelegatingHandler that rewrites the query (api-version) or fragment is
    // not a redirect. No URI goes into the message: it may carry a secret in its query.
    private static void ThrowIfRedirected(Uri sentUri, HttpResponseMessage response)
    {
        const UriComponents Target = UriComponents.Scheme | UriComponents.Host | UriComponents.StrongPort
            | UriComponents.Path;
        if (response.RequestMessage?.RequestUri is { IsAbsoluteUri: true } finalUri
            && Uri.Compare(sentUri, finalUri, Target, UriFormat.UriEscaped, StringComparison.Ordinal) != 0)
        {
            throw new SdkException(
                "The HttpClient passed to SystemNetHttpClient followed a redirect itself, so the SDK's redirect policy "
                + "could not strip credentials from it. Configure the client's primary handler with "
                + "AllowAutoRedirect = false (TRANSPORT-1).");
        }
    }

    private static bool IsWireSafe(string name, IReadOnlyList<string> values)
    {
        if (!HttpHeaderSyntax.IsValidName(name))
        {
            return false;
        }

        foreach (var value in values)
        {
            if (!HttpHeaderSyntax.IsValidOutboundValue(value))
            {
                return false;
            }
        }

        return true;
    }

    private HttpRequestMessage ToHttpRequestMessage(Request request)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method.Name), request.Url);
        if (request.Body is { } body)
        {
            message.Content = new RequestBodyContent(body);
        }

        var (stripTraceparent, stripTracestate) = StripTraceContext(request);
        foreach (var (name, values) in request.Headers)
        {
            // Design §8.1, P5c-11: the SDK's own stamp for the current attempt is dropped so the runtime writes its child
            // span's id. Nothing the caller intended is lost, so the drop is silent.
            if ((stripTraceparent && TraceContextStripping.IsTraceparent(name))
                || (stripTracestate && TraceContextStripping.IsTracestate(name)))
            {
                continue;
            }

            if (s_framingHeaders.Contains(name))
            {
                s_framingHeaderDropped(_logger, name, null);
                continue;
            }

            // Content-Type is owned by the content (set in RequestBodyContent); skip it here.
            if (string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // XCUT-18 defence in depth: the model validated this header, but a value accepted on the lenient inbound
            // path can be re-used on a request. Drop it rather than let HttpClient write it or fail the whole send.
            if (!IsWireSafe(name, values))
            {
                s_unsafeHeaderDropped(_logger, HttpHeaderSyntax.EscapeName(name), null);
                continue;
            }

            foreach (var value in values)
            {
                if (!message.Headers.TryAddWithoutValidation(name, value))
                {
                    message.Content?.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }

        return message;
    }

    // Decided once, before the header loop: the loop order is not guaranteed, and the tracestate decision follows the
    // traceparent's. Nothing is allocated when there is no ambient activity.
    private static (bool Traceparent, bool Tracestate) StripTraceContext(Request request)
    {
        if (Activity.Current is not { Id: { } currentId } current
            || request.Headers.Get("traceparent") is not { } carried
            || !TraceContextStripping.ShouldStripTraceparent(carried, currentId, TraceContextStripping.RuntimeInjects()))
        {
            return (false, false);
        }

        var tracestate = request.Headers.Get("tracestate");
        return (true, tracestate is not null && TraceContextStripping.ShouldStripTracestate(tracestate, current.TraceStateString, traceparentStripped: true));
    }

    private Response ToResponse(HttpResponseMessage message, Request request)
    {
        var headersBuilder = new Headers.Builder();
        AddInbound(headersBuilder, message.Headers);
        AddInbound(headersBuilder, message.Content.Headers);

        // Everything that can throw is computed before the body wraps the message, so the body is handed to
        // the Response the moment it exists (CA2000).
        var status = Status.FromCode((int)message.StatusCode);
        var headers = headersBuilder.Build();
        var protocol = MapProtocol(message.Version);

        // HTTP-6: the reason phrase is carried only when it is header-safe; an unsafe one is dropped, never thrown on.
        var reason = message.ReasonPhrase is { } phrase && HttpHeaderSyntax.IsValidInboundValue(phrase) ? phrase : null;
        return new Response(request, status, protocol, headers, new HttpResponseMessageBody(message), reason);
    }

    // HTTP-19 / XCUT-18: received headers take the lenient path; one that fails even that is dropped on its own
    // (TRANSPORT-14), never failing the response.
    private void AddInbound(Headers.Builder builder, System.Net.Http.Headers.HttpHeaders source)
    {
        foreach (var (name, values) in source)
        {
            foreach (var value in values)
            {
                try
                {
                    builder.AddInbound(name, value);
                }
                catch (ArgumentException)
                {
                    s_inboundHeaderDropped(_logger, HttpHeaderSyntax.EscapeName(name), null);
                }
            }
        }
    }

    private static Protocol MapProtocol(Version version) => version switch
    {
        { Major: 1, Minor: 0 } => Protocol.Http10,
        { Major: 1, Minor: 1 } => Protocol.Http11,
        { Major: 2 } => Protocol.Http2,
        { Major: 3 } => Protocol.Quic,
        _ => Protocol.Http11,
    };
}
