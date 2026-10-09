// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance.Tests.RawSocket;

/// <summary>
/// A deliberately small HTTP/1.1 client on raw sockets, used only by the conformance kit's tests (D3, P8a-2): it shares no
/// code with <c>HttpClient</c>, so an assertion that passes on this and on <c>SystemNetHttpClient</c> is about the contract
/// and not about <c>SocketsHttpHandler</c>. One connection per call, no pooling, no proxy, no TLS, no redirects, no retry and
/// no re-send of a body. It owns nothing a caller could dispose: a connection belongs to its response.
/// </summary>
/// <param name="forwardCallerFraming">
/// Writes the caller's Host, Content-Length and Transfer-Encoding verbatim instead of computing them: the deliberately
/// broken variant that is the negative control of <c>transport-11</c>.
/// </param>
/// <remarks>
/// Failures follow the SDK taxonomy: no response is a retryable <see cref="ServiceRequestException"/> with the I/O cause, a
/// per-call <see cref="RequestOptions.Timeout"/> is a <see cref="ServiceRequestTimeoutException"/>, a cancelled caller token
/// is an <see cref="OperationCanceledException"/> carrying that token (decided by the caller's token, never by exception
/// type, the lesson of F3), and an unparseable status line is a <see cref="ServiceResponseException"/>.
/// </remarks>
internal sealed class RawSocketHttpClient(bool forwardCallerFraming = false) : IAsyncHttpClient
{
    /// <inheritdoc/>
    public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        using var timeout = options.Timeout is { } limit ? new CancellationTokenSource(limit) : null;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout?.Token ?? CancellationToken.None);
        var exchange = new RawSocketExchange();
        try
        {
            // Cancellation reaches blocked I/O by closing the socket; the registration ends with the call, so a cancel
            // after delivery leaves the response readable (ASYNC-20).
            using var abort = linked.Token.Register(static state => ((RawSocketExchange)state!).Abort(), exchange);
            return await SendAsync(request, exchange, forwardCallerFraming, linked.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not SdkException)
        {
            exchange.Dispose();
            throw Map(ex, timeout, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<Response> SendAsync(Request request, RawSocketExchange exchange, bool forwardCallerFraming, CancellationToken cancellationToken)
    {
        await exchange.ConnectAsync(request.Url.Host, request.Url.Port, cancellationToken).ConfigureAwait(false);
        await RawSocketRequestWriter.WriteAsync(request, exchange.Stream, forwardCallerFraming, cancellationToken).ConfigureAwait(false);
        return await RawSocketResponseReader.ReadAsync(request, new RawSocketReader(exchange.Stream), exchange, cancellationToken).ConfigureAwait(false);
    }

    private static Exception Map(Exception failure, CancellationTokenSource? timeout, CancellationToken callerToken)
    {
        if (callerToken.IsCancellationRequested)
        {
            return new OperationCanceledException("The call was cancelled by its caller.", failure, callerToken);
        }

        return failure switch
        {
            _ when timeout?.IsCancellationRequested == true => new ServiceRequestTimeoutException("The request timed out before a response was received.", failure),
            RawSocketResponseReader.InvalidStatusLineException => new ServiceResponseException("The server sent an unparseable response.", failure),
            _ => new ServiceRequestException("The request could not be sent to the server.", failure),
        };
    }
}
