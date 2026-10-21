// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// The cooperative form of CFG-21: SDK code that stops awaiting a task that can still produce a
/// <see cref="IDisposable"/> result (a <c>Response</c>) disposes that late result, exactly once and quietly
/// (design §8.3, §10 entry 8; ASYNC-5, TRANSPORT-9).
/// </summary>
/// <remarks>
/// <para>
/// <c>Task&lt;T&gt;.WaitAsync</c> abandons the task when the token fires first, so a result delivered later is disposed by
/// nobody. This helper is the sanctioned <c>WaitAsync</c> site for a task whose result is a disposable the caller would
/// otherwise orphan (the banned-API list points here; <c>TypedResponse.GetValueAsync</c> is the other <c>WaitAsync</c> site, safe
/// because the result lives in the memoized task and the response handler owns and disposes the response): it attaches one
/// awaiter per abandonment that disposes the result through <see cref="Disposal"/> if the task later completes, observes
/// a late fault so it cannot surface as an unobserved task exception, and ignores a late cancellation. "Exactly once"
/// holds twice over: one awaiter per abandonment, and <c>Response</c>'s own dispose is latched (3b).
/// </para>
/// <para>
/// It is generic and internal; the public face is <see cref="Client.ResponseTaskExtensions.WaitOrDisposeAsync"/>, which fixes
/// <c>T</c> to <c>Response</c> and is the cancel-without-interruption mode of <c>AsAsync</c>'s task (phase 11, task 2.4;
/// design §11 item 43, SEAM-30). <c>AsAsync</c> itself disposes a response produced after cancellation inside its worker (2b),
/// which covers a task that is still running when the token fires; this covers the window where the task has completed with a
/// response at the instant the caller's wait observes the token. Phase 8b's per-call timeout is not a consumer: the reference
/// transport's deadline cancels the native send through a linked token and abandons nothing (P8b-8).
/// </para>
/// </remarks>
internal static class LateResult
{
    /// <summary>Waits for <paramref name="task"/> until <paramref name="cancellationToken"/> fires.</summary>
    /// <typeparam name="T">The disposable result type.</typeparam>
    /// <param name="task">The task to wait for.</param>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>The task's result when it completes first.</returns>
    /// <exception cref="OperationCanceledException">
    /// The token fired first; a result the task produces later is disposed.
    /// </exception>
    internal static async Task<T> WaitOrDisposeAsync<T>(Task<T> task, CancellationToken cancellationToken)
        where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(task);
        try
        {
#pragma warning disable RS0030 // SEAM-30, design §8.3: the one sanctioned Task<T>.WaitAsync site; a late result is disposed below.
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore RS0030
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Test only the caller's token, never task.IsCompleted: the abandoned task may complete between WaitAsync
            // throwing and this filter running, and a filter that skipped disposal then would leak the result.
            DisposeWhenCompleted(task);
            throw;
        }
    }

    /// <summary>Attaches the disposing awaiter without waiting, for a caller that abandons a task for any reason.</summary>
    /// <typeparam name="T">The disposable result type.</typeparam>
    /// <param name="task">The abandoned task.</param>
    internal static void DisposeWhenCompleted<T>(Task<T> task)
        where T : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(task);
        _ = DisposeLateAsync(task);
    }

    private static async Task DisposeLateAsync<T>(Task<T> task)
        where T : class, IDisposable
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            Disposal.DisposeQuietly(result);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // A faulted or cancelled late task is observed here and ignored: nobody is waiting for it (R3).
        }
    }
}
