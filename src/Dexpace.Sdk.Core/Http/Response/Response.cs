// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// An immutable HTTP response returned by a transport (HTTP-1, HTTP-4, HTTP-6, HTTP-11).
/// </summary>
/// <remarks>
/// The <see cref="Body"/> is not pre-buffered — callers own its lifecycle and must dispose the
/// response (which disposes the body) to release the underlying connection. The metadata
/// (<see cref="Request"/>, <see cref="Status"/>, <see cref="Headers"/>, <see cref="Protocol"/>,
/// <see cref="ReasonPhrase"/>) is immutable and safe to share, but the body carries single-use read state.
/// <para>
/// A response is derived only through <see cref="WithBody"/>: a <c>WithHeaders</c> would leave two
/// <see cref="Response"/> objects owning one body, and both would dispose it. Every other field is re-derivable
/// through the constructor from the accessors.
/// </para>
/// </remarks>
public sealed class Response : IAsyncDisposable, IDisposable
{
    /// <summary>Creates a response.</summary>
    /// <remarks>
    /// <b>Breaking:</b> the constructor was <c>(Status, Headers?, ResponseBody?, Protocol = Http11)</c>. It now takes the
    /// <paramref name="request"/> that produced the response first, and <paramref name="protocol"/> is required: the
    /// silent <c>Http11</c> default is gone (HTTP-4).
    /// </remarks>
    /// <param name="request">The request that produced this response.</param>
    /// <param name="status">The status code.</param>
    /// <param name="protocol">The negotiated protocol version; must be a defined <see cref="Protocol"/>.</param>
    /// <param name="headers">The response headers (defaults to empty).</param>
    /// <param name="body">The response body, optional at construction (defaults to an empty buffered body).</param>
    /// <param name="reasonPhrase">
    /// The reason phrase, or <see langword="null"/>. It must pass <see cref="HttpHeaderSyntax.IsValidInboundValue"/>:
    /// no control character other than HTAB, and no DEL.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is not a defined value.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="reasonPhrase"/> holds a control character; the message names it by code point and never echoes
    /// the phrase.
    /// </exception>
    public Response(
        Request.Request request,
        Status status,
        Protocol protocol,
        Headers? headers = null,
        ResponseBody? body = null,
        string? reasonPhrase = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(protocol))
        {
            throw new ArgumentOutOfRangeException(nameof(protocol), "The protocol is not a defined Protocol value.");
        }

        ValidateReasonPhrase(reasonPhrase);
        Request = request;
        Status = status;
        Protocol = protocol;
        Headers = headers ?? Headers.Empty;
        Body = body ?? ResponseBody.FromReplayableBytes([], null);
        ReasonPhrase = reasonPhrase;
    }

    /// <summary>The request that produced this response.</summary>
    public Request.Request Request { get; }

    /// <summary>The response status code.</summary>
    public Status Status { get; }

    /// <summary>The response headers; may be empty but never <see langword="null"/>.</summary>
    public Headers Headers { get; }

    /// <summary>
    /// The response body; never <see langword="null"/>: an absent body is an empty buffered body (P2a-4).
    /// </summary>
    public ResponseBody Body { get; }

    /// <summary>The negotiated protocol version.</summary>
    public Protocol Protocol { get; }

    /// <summary>The reason phrase the server sent, or <see langword="null"/> when none was sent or it was not safe.</summary>
    public string? ReasonPhrase { get; }

    /// <summary>True when the status is informational (100–199).</summary>
    public bool IsInformational => Status.IsInformational;

    /// <summary>True when the status is a redirect (3xx).</summary>
    public bool IsRedirect => Status.IsRedirect;

    /// <summary>True when the status is a client error (4xx).</summary>
    public bool IsClientError => Status.IsClientError;

    /// <summary>True when the status is a server error (5xx).</summary>
    public bool IsServerError => Status.IsServerError;

    /// <summary>True when the status is a client or server error (400–599).</summary>
    public bool IsError => Status.IsError;

    /// <summary>
    /// Returns a new response that owns <paramref name="body"/> and keeps everything else; this response keeps its own
    /// body and must still be disposed (HTTP-3).
    /// </summary>
    /// <param name="body">The replacement body.</param>
    /// <returns>A new <see cref="Response"/>.</returns>
    public Response WithBody(ResponseBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return new Response(Request, Status, Protocol, Headers, body, ReasonPhrase);
    }

    /// <summary>Shorthand for <c>Status.IsSuccess</c>.</summary>
    public bool IsSuccess => Status.IsSuccess;

    /// <summary>
    /// Throws <see cref="HttpResponseException"/> if the status is an error: 400–599.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only client and server errors map to an exception (BODY-31, RECOV-15). Every other status — 1xx, 2xx, a
    /// <c>304 Not Modified</c> or a 3xx that no redirect policy followed — returns normally with the body untouched.
    /// </para>
    /// <para>
    /// For an error, the response body is drained up to <see cref="MaxBufferedErrorBytes"/> into an in-memory copy
    /// that can be read any number of times, and that copy is attached to the thrown exception so that
    /// <see cref="HttpResponseException.GetErrorAsync{T}"/> can read it. The cap guards against oversized error
    /// pages consuming unbounded memory; bytes beyond it are dropped. This response is disposed before the method
    /// completes, whether or not the drain succeeded, so the connection behind it is released (HTTP-52, BODY-30).
    /// </para>
    /// <para>
    /// <b>Breaking change (phase 1, S8):</b> when this method throws, it has already disposed this response. Do not
    /// read <see cref="Body"/> after catching the exception; read the error body through the exception's
    /// <see cref="HttpResponseException.Response"/> or <see cref="HttpResponseException.GetErrorAsync{T}"/> instead.
    /// Disposing this response again, for example from an enclosing <c>using</c>, is safe.
    /// </para>
    /// <para>
    /// <b>Breaking:</b> a dispose failure after a failed drain is attached to the drain's exception
    /// (<see cref="ExceptionTrail.AddSuppressed"/>) instead of replacing it (RECOV-16, P4b-16). The capture itself is the
    /// one error-body buffer in core, shared with <c>ErrorMappingStep</c>.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">A token that can cancel the body-drain operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the check has been performed.</returns>
    /// <exception cref="HttpResponseException">
    /// The response status is in the 400–599 range. The exception carries a replayable buffered copy of the error
    /// body (up to <see cref="MaxBufferedErrorBytes"/> bytes).
    /// </exception>
    public async ValueTask EnsureSuccessAsync(CancellationToken cancellationToken = default)
    {
        if (!Status.IsClientError && !Status.IsServerError)
        {
            return;
        }

        throw ErrorMapping.ToException(
            await ErrorBodyBuffer.CaptureAsync(this, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Throws <see cref="HttpResponseException"/> if the status is an error: 400–599. The synchronous twin of
    /// <see cref="EnsureSuccessAsync"/> over the same bounded error-body buffer (RECOV-16).
    /// </summary>
    /// <remarks>
    /// When this method throws, it has already disposed this response, exactly as <see cref="EnsureSuccessAsync"/>
    /// does; read the error body through the exception's <see cref="HttpResponseException.Response"/>.
    /// </remarks>
    /// <param name="cancellationToken">A token that can cancel the body-drain operation.</param>
    /// <exception cref="HttpResponseException">
    /// The response status is in the 400–599 range. The exception carries a replayable buffered copy of the error
    /// body (up to <see cref="MaxBufferedErrorBytes"/> bytes).
    /// </exception>
    public void EnsureSuccess(CancellationToken cancellationToken = default)
    {
        if (!Status.IsClientError && !Status.IsServerError)
        {
            return;
        }

        throw ErrorMapping.ToException(ErrorBodyBuffer.Capture(this, cancellationToken));
    }

    /// <summary>
    /// The maximum number of bytes buffered from an error response body by
    /// <see cref="EnsureSuccessAsync"/>. Larger bodies are silently truncated to this limit.
    /// </summary>
    public const int MaxBufferedErrorBytes = 1024 * 1024; // 1 MiB

    private static void ValidateReasonPhrase(string? reasonPhrase)
    {
        if (reasonPhrase is null)
        {
            return;
        }

        for (var i = 0; i < reasonPhrase.Length; i++)
        {
            if (!HttpHeaderSyntax.IsValidInboundValue(reasonPhrase.AsSpan(i, 1)))
            {
                throw new ArgumentException(
                    $"The reason phrase contains the invalid character {HeaderSyntax.CodePoint(reasonPhrase[i])} at index {i}; "
                    + "it must not hold a control character.",
                    nameof(reasonPhrase));
            }
        }
    }

    private readonly Lock _exchangeGate = new();
    private readonly List<ExchangeContext> _exchanges = [];

    /// <summary>
    /// Attaches the exchange link a pipeline promoted for this response, so disposing the response closes it
    /// (design §5.4). A pipeline used as another pipeline's transport attaches a second link; every link is closed, in
    /// reverse order of attachment, when the response is disposed.
    /// </summary>
    internal void AttachExchange(ExchangeContext exchange)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        lock (_exchangeGate)
        {
            _exchanges.Add(exchange);
        }
    }

    private void CloseExchanges()
    {
        ExchangeContext[] links;
        lock (_exchangeGate)
        {
            if (_exchanges.Count == 0)
            {
                return;
            }

            links = [.. _exchanges];
            _exchanges.Clear();
        }

        for (var i = links.Length - 1; i >= 0; i--)
        {
            links[i].Close();
        }
    }

    private int _disposed;

    /// <summary>Disposes the response and its body, at most once across <see cref="Dispose"/> and <see cref="DisposeAsync"/>.</summary>
    /// <remarks>
    /// <b>Breaking:</b> disposing is now latched (HTTP-43, P3b-2). The first call releases the body; a release that throws
    /// propagates from that call only, and every later call is a no-op.
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CloseExchanges();
        Body.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the response and its body asynchronously, sharing <see cref="Dispose"/>'s latch.</summary>
    /// <remarks>
    /// <b>Breaking:</b> disposing is now latched (HTTP-43, P3b-2); see <see cref="Dispose"/>.
    /// </remarks>
    /// <returns>A task that completes when the body has been released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CloseExchanges();
        await Body.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
