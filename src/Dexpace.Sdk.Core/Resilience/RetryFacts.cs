// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Net.Http;
using System.Net.Sockets;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Resilience;

/// <summary>
/// The single source of the retry facts the model and the policies share (HTTP-9; design §6.1): the idempotent-method
/// set, the status classifier, the failure classifier and the re-send gate.
/// </summary>
/// <remarks>
/// <see cref="IsRetryableStatus"/>, <see cref="IsRetryableCause"/> and <see cref="IsRetryableFailure"/> are the single
/// classifier (CFG-35, XCUT-5, XCUT-6; P5a-21, P6a-8). The class lives in <c>Resilience</c>, which references no
/// pipeline type (P6a-4), so both retry stacks share it.
/// </remarks>
internal static class RetryFacts
{
    /// <summary>
    /// The methods whose repetition has the same effect as issuing them once (RFC 9110 §9.2.2): GET, HEAD, OPTIONS,
    /// PUT and DELETE. TRACE is deliberately absent: it is safe but not idempotent for retry purposes here. Read through <c>Method.IsIdempotent</c>.
    /// </summary>
    internal static FrozenSet<Method> IdempotentMethods { get; } =
        new[] { Method.Get, Method.Head, Method.Options, Method.Put, Method.Delete }.ToFrozenSet();

    /// <summary>
    /// Whether a status code is retryable by the single classifier: 408, 429, and 500 to 599 except 501 and 505 (XCUT-5).
    /// </summary>
    /// <param name="code">The HTTP status code.</param>
    /// <returns><see langword="true"/> for a retryable code.</returns>
    internal static bool IsRetryableStatus(int code) =>
        code is 408 or 429 or (>= 500 and <= 599 and not 501 and not 505);

    /// <summary>
    /// The default configured set of retryable statuses, <c>{408, 429, 500, 502, 503, 504}</c> (XCUT-7), a subset of the
    /// classifier <see cref="IsRetryableStatus"/> (RETRY-1).
    /// </summary>
    internal static FrozenSet<int> DefaultRetryableStatusCodes { get; } = new[] { 408, 429, 500, 502, 503, 504 }.ToFrozenSet();

    /// <summary>
    /// Whether a request may be sent again: with no body, its method is idempotent; with a body, the body is replayable
    /// whatever the method (RETRY-5, RETRY-7, XCUT-10, BODY-5). The one predicate both retry stacks call.
    /// </summary>
    /// <param name="request">The request a retry would re-send.</param>
    /// <returns><see langword="true"/> when the request is safe to re-send.</returns>
    internal static bool IsResendable(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Body is null ? IdempotentMethods.Contains(request.Method) : request.Body.IsReplayable;
    }

    /// <summary>
    /// The one classifier for a failure (RETRY-2, RETRY-23, RETRY-37, RECOV-17): whether the engine may retry it.
    /// </summary>
    /// <remarks>
    /// The call's token is tested first, so a cancelled call is never retryable. An <see cref="HttpResponseException"/>
    /// anywhere in the cause chain decides alone, by <paramref name="retryableStatuses"/> (the baked flag and any
    /// wrapper's capability are not consulted). Otherwise a <see langword="true"/> <see cref="IRetryableError"/>
    /// capability or a member of the I/O family anywhere in the chain makes the failure retryable; a
    /// <see langword="false"/> capability does not veto (P6a-8).
    /// </remarks>
    /// <param name="failure">The failure.</param>
    /// <param name="retryableStatuses">The configured retryable status codes.</param>
    /// <param name="callToken">The call's cancellation token.</param>
    /// <returns><see langword="true"/> for a retryable failure.</returns>
    internal static bool IsRetryableFailure(Exception failure, IReadOnlySet<int> retryableStatuses, CancellationToken callToken)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(retryableStatuses);
        if (callToken.IsCancellationRequested)
        {
            return false;
        }

        foreach (var cause in ExceptionFacts.EnumerateCauses(failure))
        {
            if (cause is HttpResponseException http)
            {
                return retryableStatuses.Contains(http.Status.Code);
            }
        }

        return IsRetryableCause(failure);
    }

    /// <summary>
    /// Whether an exception, or any cause of it, is transient: it advertises <see cref="IRetryableError.IsRetryable"/>, or
    /// it is in the I/O family: an <see cref="System.IO.IOException"/>, a <see cref="SocketException"/>, a
    /// <see cref="TimeoutException"/>, or an <see cref="HttpRequestException"/> with no status code (design §11 item 23).
    /// The walk is bounded and cycle-safe (<see cref="ExceptionFacts.EnumerateCauses"/>). A caller-cancelled
    /// <see cref="TaskCanceledException"/> is not retryable; an <c>HttpClient</c> timeout is, because its inner exception
    /// is a <see cref="TimeoutException"/>.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> for a retryable cause.</returns>
    internal static bool IsRetryableCause(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        foreach (var cause in ExceptionFacts.EnumerateCauses(exception))
        {
            if (cause is IRetryableError { IsRetryable: true }
                || cause is System.IO.IOException or SocketException or TimeoutException or HttpRequestException { StatusCode: null })
            {
                return true;
            }
        }

        return false;
    }
}
