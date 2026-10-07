// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Net.Http;
using System.Net.Sockets;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// The single source of the retry facts the model and the policies share (HTTP-9; design §6.1). Phase 6a grows this
/// class and derives its allow-lists from the same set.
/// </summary>
/// <remarks>
/// <see cref="IsRetryableStatus"/> and <see cref="IsRetryableCause"/> are CFG-35's single classifier (P5a-21). Phase 6a
/// wires XCUT-5 (<c>HttpResponseException.IsRetryable</c> baked from the status half) and XCUT-6 (the
/// <c>IRetryableError</c> capability, a widening of the cause half). No as-built behaviour changes in 5a: this is not
/// <see cref="RetryPolicy"/>'s configured status set, which is XCUT-7's data.
/// </remarks>
internal static class RetryFacts
{
    /// <summary>
    /// The methods whose repetition has the same effect as issuing them once (RFC 9110 §9.2.2): GET, HEAD, OPTIONS,
    /// PUT and DELETE. TRACE is deliberately absent: it is safe but not idempotent for retry purposes here, so
    /// <see cref="RetryPolicy"/> no longer retries it. Read through <c>Method.IsIdempotent</c>.
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
    /// Whether an exception, or any cause of it, is in the I/O family: an <see cref="System.IO.IOException"/>, a
    /// <see cref="SocketException"/>, a <see cref="TimeoutException"/>, or an <see cref="HttpRequestException"/> with no
    /// status code (design §11 item 23). The walk is bounded and cycle-safe (<see cref="ExceptionFacts.EnumerateCauses"/>).
    /// A caller-cancelled <see cref="TaskCanceledException"/> is not retryable; an <c>HttpClient</c> timeout is, because
    /// its inner exception is a <see cref="TimeoutException"/>.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns><see langword="true"/> for a retryable cause.</returns>
    internal static bool IsRetryableCause(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        foreach (var cause in ExceptionFacts.EnumerateCauses(exception))
        {
            if (cause is System.IO.IOException or SocketException or TimeoutException or HttpRequestException { StatusCode: null })
            {
                return true;
            }
        }

        return false;
    }
}
