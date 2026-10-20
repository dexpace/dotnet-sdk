// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Client;

/// <summary>
/// The cancel-without-interruption mode of a response task (SEAM-30, ASYNC-3; design §5.3, §11 item 43).
/// </summary>
/// <remarks>
/// A blocking transport bridged with <see cref="HttpClientExtensions.AsAsync"/> cannot be interrupted, so a caller that wants to stop <em>waiting</em>
/// while the call runs on abandons the task. Doing that with <c>Task.WaitAsync(token)</c> can orphan an undisposed <see cref="Response"/> in the window
/// where the call completes at the very instant the token is signalled (the abandoned task delivers a response nobody closes). Awaiting the task through
/// <see cref="WaitOrDisposeAsync"/> instead closes that late response.
/// </remarks>
public static class ResponseTaskExtensions
{
    /// <summary>
    /// Waits for <paramref name="task"/> until <paramref name="cancellationToken"/> is signalled, closing the <see cref="Response"/> if the task delivers
    /// one after the caller stopped waiting.
    /// </summary>
    /// <param name="task">The response task, typically from <c>IAsyncHttpClient.ExecuteAsync</c> over an <c>AsAsync</c> bridge.</param>
    /// <param name="cancellationToken">Stops the wait. It does not cancel <paramref name="task"/>; pass the same token to the call that produced it.</param>
    /// <returns>The task's response when it completes first; the caller owns and disposes it.</returns>
    /// <remarks>
    /// <para>
    /// When the token is signalled first (or was signalled already) the wait ends with an <see cref="OperationCanceledException"/> and one awaiter is
    /// attached to <paramref name="task"/>. If the task later completes with a response, that awaiter disposes it, exactly once and quietly (a
    /// <c>Dispose</c> that throws is swallowed, as the bridge's own post-cancellation dispose is); a later fault is observed so it cannot surface as an
    /// unobserved task exception; a later cancellation is ignored. A response the task completed with before the wait observed the token is returned
    /// to the caller if the task won, and closed here if the token did.
    /// </para>
    /// <para>
    /// The exception's <see cref="OperationCanceledException.CancellationToken"/> is <paramref name="cancellationToken"/>. A failure of the task itself
    /// while the caller is still waiting is rethrown as it is.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="task"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was signalled before the task completed; a response it produces later is disposed.</exception>
    public static Task<Response> WaitOrDisposeAsync(this Task<Response> task, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(task);
        return LateResult.WaitOrDisposeAsync(task, cancellationToken);
    }
}
