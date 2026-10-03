// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Client;

/// <summary>
/// The option-less calls on the transport SPIs, and the bridges between the synchronous <see cref="IHttpClient"/> and
/// asynchronous <see cref="IAsyncHttpClient"/> transport SPIs.
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// Sends <paramref name="request"/> with <see cref="RequestOptions.Empty"/> (SEAM-11).
    /// </summary>
    /// <param name="client">The synchronous transport.</param>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to abort the exchange.</param>
    /// <returns>The response (caller owns disposal).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="request"/> is null.</exception>
    public static Response Execute(this IHttpClient client, Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        return client.Execute(request, RequestOptions.Empty, cancellationToken);
    }

    /// <summary>
    /// Sends <paramref name="request"/> with <see cref="RequestOptions.Empty"/> (SEAM-11).
    /// </summary>
    /// <param name="client">The asynchronous transport.</param>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to abort the exchange.</param>
    /// <returns>A task that completes with the response (caller owns disposal).</returns>
    /// <remarks>
    /// A null <paramref name="request"/> is delivered through the returned task, not thrown (ASYNC-2); a null
    /// <paramref name="client"/> has nothing to deliver it through and throws.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is null.</exception>
    public static Task<Response> ExecuteAsync(this IAsyncHttpClient client, Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return request is null
            ? Task.FromException<Response>(new ArgumentNullException(nameof(request)))
            : client.ExecuteAsync(request, RequestOptions.Empty, cancellationToken);
    }

    /// <summary>
    /// Wraps a synchronous transport as an <see cref="IAsyncHttpClient"/> by running each blocking
    /// <see cref="IHttpClient.Execute"/> call on <paramref name="scheduler"/>.
    /// </summary>
    /// <param name="client">The synchronous transport to wrap.</param>
    /// <param name="scheduler">
    /// The scheduler that runs the blocking calls. There is no overload without one (SEAM-18): blocking calls starve a
    /// shared pool, so the caller chooses where they run.
    /// </param>
    /// <returns>An async facade over <paramref name="client"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Scheduler.</b> Each call is one task started with <see cref="TaskCreationOptions.LongRunning"/> and
    /// <see cref="TaskCreationOptions.DenyChildAttach"/> on <paramref name="scheduler"/>; a custom scheduler receives
    /// <c>LongRunning</c> as a hint it may ignore. <see cref="TaskScheduler.Default"/> is accepted, and then each call
    /// runs on a dedicated thread, not a pool thread, so it costs one thread per call.
    /// </para>
    /// <para>
    /// <b>Context and cancellation (SEAM-24).</b> The caller's <see cref="ExecutionContext"/> flows to the worker, so
    /// <c>AsyncLocal&lt;T&gt;</c> values, <c>Activity.Current</c> and logging scopes are visible there. Cancellation maps
    /// both ways through the one token: it reaches the blocking call, and a cancelled call surfaces
    /// <see cref="OperationCanceledException"/> to the awaiting caller. A blocking call cannot be interrupted, so the
    /// token is cooperative (design §10 <c>cooperative-cancellation</c>).
    /// </para>
    /// <para>
    /// <b>No orphaned response (SEAM-30).</b> A response produced after the call's token is signalled is disposed and the
    /// call completes cancelled, so design §5.3's "cancel without interruption" mode (the caller stops awaiting through
    /// <c>WaitAsync</c> while the worker runs on) cannot orphan one. That dispose is quiet: a
    /// <c>Dispose</c> that throws there does not replace the cancellation; the failure is reported as an <c>exception</c>
    /// event on the current activity (and a warning when a logger exists) and the task completes cancelled.
    /// <para><b>Breaking (behaviour):</b> a throwing dispose there used to fault the task with that exception.</para>
    /// </para>
    /// <para>
    /// <b>Lifecycle (SEAM-14, SEAM-25).</b> The bridge owns neither the scheduler nor the wrapped client, so disposing it
    /// disposes neither. Argument errors reach the caller through the returned task (ASYNC-2).
    /// </para>
    /// <para>
    /// <b>Breaking:</b> was <c>AsAsync(this IHttpClient)</c>, which offloaded to the thread pool; it takes the caller's
    /// scheduler now, and no longer disposes the wrapped client.
    /// </para>
    /// <para>
    /// <b>Breaking (behaviour):</b> a response produced after the call's token is signalled is disposed and the call
    /// completes cancelled.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> or <paramref name="scheduler"/> is null.</exception>
    public static IAsyncHttpClient AsAsync(this IHttpClient client, TaskScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(scheduler);
        return new SyncToAsyncAdapter(client, scheduler);
    }

    /// <summary>
    /// Wraps an asynchronous transport as an <see cref="IHttpClient"/> that blocks on the returned
    /// task. The blocking wait unwraps transport exceptions so callers see the original failure.
    /// </summary>
    /// <param name="client">The asynchronous transport to wrap.</param>
    /// <returns>A blocking facade over <paramref name="client"/>.</returns>
    /// <remarks>
    /// <para>
    /// A last resort (design §3.3, §5.3), for call sites that cannot go async. The call's options and token are passed to
    /// <see cref="IAsyncHttpClient.ExecuteAsync"/>, so cancelling the token cancels the in-flight task (SEAM-13). A
    /// <see langword="null"/> task or <see langword="null"/> response from the wrapped client is an
    /// <see cref="InvalidOperationException"/> (SEAM-16).
    /// </para>
    /// <para>
    /// <b>Deadlock hazard.</b> The call blocks the calling thread. Under a <see cref="SynchronizationContext"/> that
    /// runs one callback at a time, a wrapped transport whose awaits do not use <c>ConfigureAwait(false)</c> can
    /// deadlock. The bridge does not hop to the thread pool to avoid this: that would reintroduce the shared-pool
    /// dependency SEAM-18 rules out for <see cref="AsAsync(IHttpClient, TaskScheduler)"/>.
    /// </para>
    /// <para>
    /// <b>Breaking (behaviour):</b> no longer disposes the wrapped client (SEAM-14); a caller who wrapped a transport they
    /// own disposes it themselves.
    /// </para>
    /// </remarks>
    public static IHttpClient AsBlocking(this IAsyncHttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new AsyncToSyncAdapter(client);
    }

    private sealed class SyncToAsyncAdapter(IHttpClient inner, TaskScheduler scheduler) : IAsyncHttpClient
    {
        // Not async: a null argument is delivered through the task (ASYNC-2), and the work is handed to the scheduler.
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return Task.FromException<Response>(new ArgumentNullException(nameof(request)));
            }

            if (options is null)
            {
                return Task.FromException<Response>(new ArgumentNullException(nameof(options)));
            }

            return Task.Factory.StartNew(
                () => Run(inner, request, options, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                scheduler);
        }

        // SEAM-25, XCUT-22: the bridge owns neither the scheduler nor the wrapped client, so it releases nothing.
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static Response Run(IHttpClient inner, Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            var response = inner.Execute(request, options, cancellationToken)
                ?? throw new InvalidOperationException("The wrapped IHttpClient returned null (SEAM-16).");

            // SEAM-30, design §5.3: the caller may have stopped awaiting, so a response produced after the token was
            // signalled is closed here and never surfaces. Quiet, so a failing dispose cannot replace the cancellation.
            if (cancellationToken.IsCancellationRequested)
            {
                Disposal.DisposeQuietly(response);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return response;
        }
    }

    // design §3.3, §5.3 — last resort; the SynchronizationContext deadlock hazard is documented on AsBlocking.
#pragma warning disable RS0030
    private sealed class AsyncToSyncAdapter(IAsyncHttpClient inner) : IHttpClient
    {
        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(options);

            var task = inner.ExecuteAsync(request, options, cancellationToken)
                ?? throw new InvalidOperationException("The wrapped IAsyncHttpClient returned null (SEAM-16).");
            return task.GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("The wrapped IAsyncHttpClient returned null (SEAM-16).");
        }

        // SEAM-14, XCUT-22: the bridge creates nothing, so it releases nothing; the caller disposes what it wrapped.
        public void Dispose()
        {
        }
    }
#pragma warning restore RS0030
}
