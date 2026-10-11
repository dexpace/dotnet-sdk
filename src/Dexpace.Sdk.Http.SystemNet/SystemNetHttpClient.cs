// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
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
/// <b>Construction and ownership (TRANSPORT-1, TRANSPORT-15).</b> What the constructor takes states who owns what:
/// </para>
/// <list type="table">
/// <listheader><term>Constructor</term><description>Native client, handler, proxy</description></listheader>
/// <item><term><c>()</c>, <c>(ILogger)</c>, <c>(SystemNetHttpClientOptions)</c></term><description>
/// Owned: the transport builds a <c>SocketsHttpHandler</c> with <c>AllowAutoRedirect = false</c> and disposes it with the
/// transport. <see cref="SystemNetHttpClientOptions.Proxy"/> installs the proxy; with none the connection is direct.</description></item>
/// <item><term><c>(HttpMessageHandler)</c>, <c>(HttpMessageHandler, SystemNetHttpClientOptions)</c></term><description>
/// The transport owns the <c>HttpClient</c> it builds over your handler and never disposes or changes the handler. A chain
/// whose primary handler follows redirects is rejected with <see cref="ArgumentException"/>. The handler keeps its own proxy.</description></item>
/// <item><term><c>(HttpClient)</c>, <c>(HttpClient, ILogger)</c>, <c>(HttpClient, SystemNetHttpClientOptions)</c></term><description>
/// Borrowed: your client is never disposed or changed. Its handler keeps its own proxy.</description></item>
/// </list>
/// <para>
/// A <see cref="SystemNetHttpClientOptions.Proxy"/> passed with a handler or a client throws <see cref="ArgumentException"/>:
/// the proxy of something you own is yours to configure.
/// <b>Breaking</b> (phase 8b): the parameterless and logger constructors no longer inherit the runtime's process-wide proxy,
/// which reads <c>HTTP_PROXY</c>, <c>HTTPS_PROXY</c> and <c>NO_PROXY</c> on first use (CFG-28: the environment is consulted only
/// when you ask). To keep it, resolve it yourself: <c>new SystemNetHttpClientOptions { Proxy = ProxyOptions.FromEnvironment() }</c>.
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
/// The synchronous call is genuinely synchronous as of phase 8b (below).
/// </para>
/// <para>
/// <b>Deadlines and failures (TRANSPORT-4, -5, -6, -8, -20; XCUT-1, XCUT-2).</b> A call's deadline is
/// <see cref="RequestOptions.Timeout"/> when it sets one, else <see cref="SystemNetHttpClientOptions.Timeout"/> (100 seconds unless
/// configured). It is armed per call on <see cref="SystemNetHttpClientOptions.TimeProvider"/>, linked with the caller's token, and it
/// never changes the shared <c>HttpClient</c>. It bounds the exchange until the response headers arrive: reading the body is bounded
/// by the token the caller passes to the read. A deadline below one millisecond is raised to one, and one above 49 days lowered to
/// 49 days. The outcome is classified by what the transport knows first and the shape of the runtime's exception second:
/// the caller's cancellation is an <see cref="OperationCanceledException"/>; a fired deadline, or a borrowed client's own
/// <c>Timeout</c>, is a retryable <see cref="ServiceRequestTimeoutException"/>; a failure to reach the server is a retryable
/// <see cref="ServiceRequestException"/>; and a cancellation the native client raised from inside (<c>CancelPendingRequests</c>, the
/// native client or this transport disposed mid-call) is a new, terminal <see cref="OperationCanceledException"/> that the SDK's
/// retry does not repeat, the native exception kept on its <see cref="ExceptionTrail"/>. A borrowed client's own <c>Timeout</c> still
/// bounds a longer per-call value, since a client shared with others cannot be lengthened without changing it.
/// <b>Breaking</b> (phase 8b): <see cref="RequestOptions.Timeout"/> was ignored; the transport's own <c>HttpClient</c> now has an
/// infinite <c>Timeout</c> and the 100 second default above replaces it; and an internal cancellation used to surface as a retryable
/// <see cref="ServiceRequestTimeoutException"/>.
/// </para>
/// <para>
/// <b>After dispose (SEAM-15).</b> Every call made after <c>Dispose</c> or <c>DisposeAsync</c> throws
/// <see cref="ObjectDisposedException"/> for every construction, a borrowed <c>HttpClient</c> included: the asynchronous face
/// returns a faulted task and never throws from the call (TRANSPORT-21). Your own <c>HttpClient</c> keeps working. Disposal is
/// idempotent (SEAM-14) and never waits for a call in flight (TRANSPORT-16).
/// <b>Breaking</b> (phase 8b): a transport over a borrowed client used to keep serving after it was disposed.
/// </para>
/// <para>
/// <b>Outbound headers (TRANSPORT-10, -11, -12, -13, -26; OBS-19).</b> The framing headers the client computes itself —
/// <c>Host</c>, <c>Content-Length</c>, <c>Transfer-Encoding</c>, <c>Connection</c>, <c>Keep-Alive</c>, <c>Upgrade</c>, <c>TE</c> and
/// <c>Expect</c> — are dropped from the request, each with a <see cref="LogLevel.Debug"/> entry naming it (event 111). Every other
/// header is re-checked against the model's outbound rule at the wire boundary, and one that fails it, or that the native client
/// cannot carry, is dropped alone and logged by name under <see cref="SystemNetHttpClientOptions.HeaderDropLogging"/> (event 110: a
/// <see cref="LogLevel.Warning"/> for the first drop of a name, a <see cref="LogLevel.Debug"/> after, by default). A drop is never
/// silent and a value is never logged (XCUT-18). The caller's <c>Content-Type</c> is the one sent; the body's media type is used only
/// when the caller set none. A content header (<c>Allow</c>, <c>Content-*</c>, <c>Expires</c>, <c>Last-Modified</c>) on a request that may
/// carry no content (<c>GET</c>, <c>HEAD</c>, <c>TRACE</c>) is dropped and logged, because attaching content would break TRANSPORT-26; on a
/// body-less <c>POST</c>, <c>PUT</c>, <c>PATCH</c>, <c>DELETE</c> or <c>OPTIONS</c> it travels on an empty content, which the wire shows as
/// <c>Content-Length: 0</c>.
/// <b>Breaking</b> (phase 8b): the body's <c>Content-Type</c> used to override the caller's header; a content header on a body-less
/// request used to vanish unlogged; and the drop events are now ids 110 to 112 with <c>http.transport.*</c> names where they were 1 to 3
/// (a repeated drop of one name is a <see cref="LogLevel.Debug"/>, not another <see cref="LogLevel.Warning"/>).
/// </para>
/// <para>
/// <b>Native retry (TRANSPORT-2).</b> <c>SocketsHttpHandler</c> has no switch for its own connection-failure retry, which re-sends a
/// request that has no content when its connection ends before any response byte. A body-less <c>POST</c>, <c>PUT</c>, <c>PATCH</c> or
/// extension-method request is therefore given a zero-length content (the same bytes on the wire) and is sent once. A body-less
/// <c>GET</c>, <c>HEAD</c>, <c>OPTIONS</c>, <c>DELETE</c> or <c>TRACE</c> is still re-sent by the native client, up to three more times:
/// all are idempotent, and attaching content to them would break TRANSPORT-26 or RFC 9110 section 8.6.
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
    private static readonly Action<ILogger, string, Exception?> s_inboundHeaderDropped = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(3, "InboundHeaderDropped"),
        "Dropped the '{HeaderName}' response header: its value carries a control character (HTTP-19, XCUT-18).");

    private readonly SystemHttpClient _client;
    private readonly bool _ownsClient;
    private readonly SendSettings _settings;
    private readonly ILogger _logger;
    private readonly HeaderDropLog _drops;
    private readonly OutboundHeaderMapper _mapper;
    private int _disposed;
    private int _releases;
    private int _captures;

    /// <summary>
    /// Creates a transport backed by an internally owned <c>HttpClient</c> that does not follow redirects and goes direct: no
    /// proxy, and no environment variable is read (CFG-28).
    /// </summary>
    public SystemNetHttpClient()
        : this(new SystemNetHttpClientOptions())
    {
    }

    /// <summary>
    /// Creates a transport backed by an internally owned <c>HttpClient</c> that does not follow redirects, logging header
    /// drops to <paramref name="logger"/>.
    /// </summary>
    /// <param name="logger">
    /// Receives an entry per dropped header, naming it and never its value: <see cref="LogLevel.Debug"/> for a framing
    /// header the transport computes itself, <see cref="LogLevel.Warning"/> for a header that is not valid on the
    /// wire, and <see cref="LogLevel.Debug"/> for a response header dropped on the inbound path.
    /// </param>
    public SystemNetHttpClient(ILogger logger)
        : this(OptionsFor(logger))
    {
    }

    /// <summary>
    /// Creates a transport backed by an internally owned <c>HttpClient</c> that does not follow redirects, configured by
    /// <paramref name="options"/>. The options are read once, here.
    /// </summary>
    /// <param name="options">The transport's settings, including the optional proxy to install (TRANSPORT-30).</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public SystemNetHttpClient(SystemNetHttpClientOptions options)
        : this(CreateOwnedClient(options, configure: null), Construction.Owned, options)
    {
    }

    /// <summary>
    /// Creates a transport that owns an <c>HttpClient</c> it builds over a caller's <paramref name="handler"/>. The handler is
    /// borrowed: it is never disposed or changed, so its owner decides its proxy, certificates and lifetime (TRANSPORT-15).
    /// </summary>
    /// <param name="handler">
    /// The outermost handler of the caller's chain. Its primary handler must not follow redirects.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The chain's primary handler follows redirects (TRANSPORT-1).</exception>
    public SystemNetHttpClient(HttpMessageHandler handler)
        : this(handler, new SystemNetHttpClientOptions())
    {
    }

    /// <summary>
    /// Creates a transport that owns an <c>HttpClient</c> it builds over a caller's <paramref name="handler"/>, configured by
    /// <paramref name="options"/>. The handler is borrowed: it is never disposed or changed (TRANSPORT-15).
    /// </summary>
    /// <param name="handler">
    /// The outermost handler of the caller's chain. Its primary handler must not follow redirects.
    /// </param>
    /// <param name="options">The transport's settings. <see cref="SystemNetHttpClientOptions.Proxy"/> must be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The chain's primary handler follows redirects (TRANSPORT-1), or <paramref name="options"/> carries a proxy.
    /// </exception>
    public SystemNetHttpClient(HttpMessageHandler handler, SystemNetHttpClientOptions options)
        : this(CreateHandlerClient(handler, options), Construction.Handler, options)
    {
    }

    /// <summary>
    /// Creates a transport backed by a caller-supplied <c>HttpClient</c>. The supplied client is not
    /// disposed when this adapter is disposed, and its handler must not follow redirects: a followed redirect (a final
    /// request URI whose scheme, host, port or path differs from the one sent) fails the call (see remarks).
    /// </summary>
    /// <param name="client">The HTTP client to wrap.</param>
    public SystemNetHttpClient(SystemHttpClient client)
        : this(client, new SystemNetHttpClientOptions())
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
        : this(client, OptionsFor(logger))
    {
    }

    /// <summary>
    /// Creates a transport backed by a caller-supplied <c>HttpClient</c>, configured by <paramref name="options"/>. The
    /// supplied client is not disposed or changed, and its handler must not follow redirects (see remarks).
    /// </summary>
    /// <param name="client">The HTTP client to wrap.</param>
    /// <param name="options">The transport's settings. <see cref="SystemNetHttpClientOptions.Proxy"/> must be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> carries a proxy: the client's owner decides it (TRANSPORT-15).</exception>
    public SystemNetHttpClient(SystemHttpClient client, SystemNetHttpClientOptions options)
        : this(client, Construction.Borrowed, RequireNoProxy(options))
    {
    }

    /// <summary>
    /// Test seam: the SDK-managed construction, with <paramref name="configureOwnedHandler"/> applied to the owned
    /// handler after the SDK's own settings, so a wire test can pin settings without leaving the owned-client path.
    /// </summary>
    internal SystemNetHttpClient(Action<SocketsHttpHandler> configureOwnedHandler)
        : this(configureOwnedHandler, new SystemNetHttpClientOptions())
    {
    }

    /// <summary>The seam with the options a test wants to construct the owned transport with.</summary>
    internal SystemNetHttpClient(Action<SocketsHttpHandler> configureOwnedHandler, SystemNetHttpClientOptions options)
        : this(CreateOwnedClient(options, configureOwnedHandler), Construction.Owned, options)
    {
    }

    private SystemNetHttpClient(SystemHttpClient client, Construction construction, SystemNetHttpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _ownsClient = construction != Construction.Borrowed;
        _logger = options.Logger;
        _drops = new HeaderDropLog(options.Logger, options.HeaderDropLogging);
        _mapper = new OutboundHeaderMapper(_drops, CreateCapture);
        _settings = new SendSettings(
            options.Logger,
            options.Timeout,
            options.HeaderDropLogging,
            options.ResendBufferLimit,
            options.TimeProvider,
            ResendCapable: construction != Construction.Owned || CarriesProxyCredentials(options.Proxy));
        if (construction == Construction.Owned && options.Proxy is { } proxy)
        {
            ProxyWarnings.Emit(options.Logger, proxy);
        }
    }

    // What built the native client decides whether it can re-send a request behind the transport's back (TRANSPORT-18).
    private enum Construction
    {
        Owned,
        Handler,
        Borrowed,
    }

    // The settings the transport resolved from its options when it was constructed.
    internal SendSettings Settings => _settings;

    // How many replay captures this transport built: zero where the native layer cannot re-send (an owned client with no proxy
    // credentials) and for every replayable body. A per-instance counter, so parallel tests never share it.
    internal int CapturesCreated => Volatile.Read(ref _captures);

    // The native client's own Timeout: infinite for a client the transport builds (its deadline replaces it), the caller's for a borrowed one.
    internal TimeSpan NativeClientTimeout => _client.Timeout;

    /// <inheritdoc/>
    /// <remarks>
    /// The call's deadline is <see cref="RequestOptions.Timeout"/>, else <see cref="SystemNetHttpClientOptions.Timeout"/>, and it
    /// bounds the exchange until the response headers arrive (TRANSPORT-5, TRANSPORT-6; see the class remarks for the failure
    /// classification).
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The transport was disposed (SEAM-15); delivered through the returned task.</exception>
    /// <exception cref="SdkException">
    /// A caller-supplied client followed a redirect itself (TRANSPORT-1): the final request URI differs from the one
    /// sent in scheme, host, port or path. The response has been disposed.
    /// </exception>
    public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var message = _mapper.Map(request);

        // The handler rewrites message.RequestUri in place when it follows a redirect, so keep what was sent.
        var sentUri = message.RequestUri!;
        HttpResponseMessage native;
        using (var deadline = CallDeadline.Start(CallDeadline.Effective(options, _settings.Timeout), _settings.TimeProvider, cancellationToken))
        {
            try
            {
                native = await _client
                    .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (SendFailures.IsTransportFailure(ex))
            {
                var mapped = SendFailures.Map(ex, deadline.Fired, cancellationToken);
                if (ReferenceEquals(mapped, ex))
                {
                    throw;
                }

                throw mapped;
            }
        }

        return AdaptGuarded(native, sentUri, request);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Genuinely synchronous (SEAM-11, PIPE-28): it calls <c>HttpClient.Send</c> on the calling thread and writes the request body
    /// through the body's own <see cref="RequestBody.WriteTo"/>, with no sync-over-async anywhere. The deadline, the failure
    /// classification and the guarded adaptation are the asynchronous face's (see <see cref="ExecuteAsync"/>).
    /// </para>
    /// <para>
    /// <b>The handler hazard.</b> <c>HttpClient.Send</c> needs a handler that implements <c>Send</c>. The runtime's own handlers
    /// do. A custom primary handler that overrides only <c>SendAsync</c> makes this method throw
    /// <see cref="NotSupportedException"/> (loudly), and a <see cref="DelegatingHandler"/> in a chain that overrides only
    /// <c>SendAsync</c> is skipped silently on this face, because the runtime forwards the synchronous call straight to its inner
    /// handler. Implement <c>Send</c> in such a handler, or call <see cref="ExecuteAsync"/>.
    /// <b>Breaking</b> (phase 8b): <c>Execute</c> used to block on the asynchronous send, which honoured a <c>SendAsync</c>-only handler.
    /// </para>
    /// <para>
    /// A <see cref="RequestBody"/> subclass that does not override <see cref="RequestBody.WriteTo"/> fails the send with
    /// <see cref="NotSupportedException"/>; every body the SDK creates overrides it.
    /// </para>
    /// </remarks>
    /// <exception cref="ObjectDisposedException">The transport was disposed (SEAM-15).</exception>
    /// <exception cref="NotSupportedException">The primary handler implements no <c>Send</c>, or the body implements no synchronous write.</exception>
    /// <exception cref="SdkException">
    /// A caller-supplied client followed a redirect itself (TRANSPORT-1), as <see cref="ExecuteAsync"/> documents.
    /// </exception>
    public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var message = _mapper.Map(request);

        var sentUri = message.RequestUri!;
        HttpResponseMessage native;
        using (var deadline = CallDeadline.Start(CallDeadline.Effective(options, _settings.Timeout), _settings.TimeProvider, cancellationToken))
        {
            try
            {
                native = _client.Send(message, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            }
            catch (Exception ex) when (SendFailures.IsTransportFailure(ex))
            {
                var mapped = SendFailures.Map(ex, deadline.Fired, cancellationToken);
                if (ReferenceEquals(mapped, ex))
                {
                    throw;
                }

                throw mapped;
            }
        }

        return AdaptGuarded(native, sentUri, request);
    }

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

    // TRANSPORT-18 (P8b-10): a single-use body is teed into a bounded capture, but only where the native layer can send the request
    // again behind the transport's back, and never for a body that can be written twice anyway.
    private ResendCapture? CreateCapture(RequestBody body)
    {
        if (!_settings.ResendCapable || body.IsReplayable)
        {
            return null;
        }

        Interlocked.Increment(ref _captures);
        return new ResendCapture(_settings.ResendBufferLimit);
    }

    // TRANSPORT-22: once the native response is live, any throw while adapting it disposes it first, and a throwing dispose never
    // replaces the failure in flight (NativeDisposal).
    private Response AdaptGuarded(HttpResponseMessage native, Uri sentUri, Request request)
    {
        try
        {
            ThrowIfRedirected(sentUri, native);
            return ToResponse(native, request);
        }
        catch (Exception ex)
        {
            NativeDisposal.DisposeQuietly(native, ex);
            throw;
        }
    }

    // TRANSPORT-1: the SDK-managed client never follows a redirect; the pipeline's RedirectPolicy does. With no proxy in the
    // options the handler goes direct (UseProxy = false, Proxy = null), so the runtime's process-wide default proxy, which reads
    // HTTP(S)_PROXY and NO_PROXY, is never consulted (CFG-28, P8b-4); with one, an adapter over ProxyOptions is installed.
    // The handler's own Credentials stay unset, so a proxy credential can only answer a 407 from that proxy (TRANSPORT-30).
#pragma warning disable RS0030 // The transport's owned-client factory: the one sanctioned construction (styleguide 13.8, P8b-3).
    private static SystemHttpClient CreateOwnedClient(SystemNetHttpClientOptions options, Action<SocketsHttpHandler>? configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        var proxy = options.Proxy is { } configured ? new ProxyOptionsWebProxy(configured) : null;
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = proxy is not null, Proxy = proxy };
        configure?.Invoke(handler);
        return new(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    // The same factory over a caller's handler: the client is the transport's, the handler is not (disposeHandler: false).
    private static SystemHttpClient CreateHandlerClient(HttpMessageHandler handler, SystemNetHttpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);
        RequireNoProxy(options);
        HandlerChain.ThrowIfFollowsRedirects(handler);
        return new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }
#pragma warning restore RS0030

    private static SystemNetHttpClientOptions OptionsFor(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return new SystemNetHttpClientOptions { Logger = logger };
    }

    // TRANSPORT-15: a proxy in the options is meaningful only for the handler the transport builds itself.
    private static SystemNetHttpClientOptions RequireNoProxy(SystemNetHttpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Proxy is null
            ? options
            : throw new ArgumentException(
                "SystemNetHttpClientOptions.Proxy applies only to the handler the transport creates itself. A transport over your "
                + "HttpClient or handler leaves its proxy to its owner (TRANSPORT-15); configure the proxy on that client or handler.",
                nameof(options));
    }

    // Only an HTTP proxy that is given credentials challenges with a 407, which the native client answers by sending the same
    // request again (TRANSPORT-18, design G5); a SOCKS proxy authenticates inside its handshake, with no request to re-send.
    private static bool CarriesProxyCredentials(ProxyOptions? proxy) =>
        proxy is { Type: ProxyType.Http } and ({ UserName: not null } or { ChallengeCredentials: not null });

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
