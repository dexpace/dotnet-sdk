// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// The one classification of a native send failure, shared by the asynchronous and the blocking face (<c>TRANSPORT-3</c>,
/// <c>-4</c>, <c>-5</c>, <c>-8</c>, <c>-20</c>, <c>XCUT-1</c>, <c>XCUT-2</c>; design §C, phase 8b P8b-7). The transport's own
/// state decides first and the shape of the runtime's exception second, so a deadline the transport armed is recognised as
/// that, not guessed from an exception message.
/// </summary>
/// <remarks>
/// <para>
/// Checked in order; the first match decides:
/// </para>
/// <list type="number">
/// <item><description>The caller's token is signalled: a cancellation that carries the caller's token, never a failure the pipeline would retry.</description></item>
/// <item><description>The transport's own deadline fired: <see cref="ServiceRequestTimeoutException"/> (retryable).</description></item>
/// <item><description>A cancellation whose cause is a <see cref="TimeoutException"/> (a borrowed client's <c>HttpClient.Timeout</c>): <see cref="ServiceRequestTimeoutException"/>.</description></item>
/// <item><description>Any other cancellation (<c>CancelPendingRequests</c>, the native client disposed mid-call): a new, terminal <see cref="OperationCanceledException"/>.</description></item>
/// <item><description>A native request failure: <see cref="ServiceRequestException"/> (retryable).</description></item>
/// </list>
/// <para>
/// <b>Why row 4 keeps the native exception off <see cref="Exception.InnerException"/>.</b> The retry classifier walks the cause
/// chain and calls any <see cref="IOException"/> in it retryable, and the runtime's cancellation carries one
/// (<c>TaskCanceledException</c> over <c>IOException</c>), so a terminal cancellation with that cause would be retried. The native
/// exception is attached to the <see cref="ExceptionTrail"/> instead, where diagnostics find it and the classifier does not look.
/// </para>
/// <para>
/// <b>Row 1 and the linked token.</b> When a deadline is armed the native client is given a token linked to the caller's, so the
/// <see cref="OperationCanceledException"/> it throws carries the linked token, not the caller's. A caller that filters on
/// <c>ex.CancellationToken == token</c> (<c>TRANSPORT-3</c>, <c>XCUT-1</c>) must still recognise its own cancellation, so such an
/// exception is replaced by one carrying the caller's token, the native one on its trail. An exception that already carries the
/// caller's token (no deadline armed) is returned as it is.
/// </para>
/// <para>No message carries a URL, a header value or a credential (<c>XCUT-18</c>).</para>
/// </remarks>
internal static class SendFailures
{
    /// <summary>Whether <paramref name="exception"/> is one the classification covers: a cancellation or a native request failure.</summary>
    /// <param name="exception">The exception the native send threw.</param>
    internal static bool IsTransportFailure(Exception exception) => exception is OperationCanceledException or HttpRequestException;

    /// <summary>
    /// Maps <paramref name="native"/> to the exception the call fails with. Returns <paramref name="native"/> itself (rethrow it)
    /// when the caller's own cancellation is the cause.
    /// </summary>
    /// <param name="native">A failure for which <see cref="IsTransportFailure"/> is true.</param>
    /// <param name="deadlineFired">Whether the transport's own per-call deadline elapsed.</param>
    /// <param name="caller">The token the caller passed to the call.</param>
    /// <returns>The exception to throw.</returns>
    internal static Exception Map(Exception native, bool deadlineFired, CancellationToken caller)
    {
        ArgumentNullException.ThrowIfNull(native);
        if (caller.IsCancellationRequested)
        {
            return native is OperationCanceledException cancelled && cancelled.CancellationToken == caller ? native : CallerCancelled(native, caller);
        }

        if (deadlineFired || (native is OperationCanceledException { InnerException: TimeoutException }))
        {
            return new ServiceRequestTimeoutException("The request timed out before a response was received.", native);
        }

        if (native is OperationCanceledException)
        {
            var cancelled = new OperationCanceledException(
                "The HTTP client cancelled the request internally; it was not a timeout and was not requested by the caller.");
            ExceptionTrail.AddSuppressed(cancelled, native);
            return cancelled;
        }

        return new ServiceRequestException(DescribeRequestFailure(native), native);
    }

    // The message follows what the runtime says happened (XCUT-4, phase 11): "could not be sent" only for a failure that stopped the request leaving
    // (name resolution, connect, TLS, a proxy tunnel); a connection that ended or sent something unreadable after the request arrived is "no response
    // was received", and so is any other native request failure. Neither carries a URL, a header value or a credential (XCUT-18).
    private static string DescribeRequestFailure(Exception native) =>
        native is HttpRequestException
        {
            HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError or HttpRequestError.ProxyTunnelError,
        }
            ? "The request could not be sent to the server."
            : "The request failed and no response was received.";

    // The caller cancelled, but the native exception does not carry the caller's token: it carries the linked deadline token, or it is a
    // request failure from a connection torn down by the same cancel. The call ends cancelled, carrying the caller's token, with
    // the native failure on the trail.
    private static OperationCanceledException CallerCancelled(Exception native, CancellationToken caller)
    {
        var cancelled = new OperationCanceledException("The request was cancelled by the caller.", caller);
        ExceptionTrail.AddSuppressed(cancelled, native);
        return cancelled;
    }
}
