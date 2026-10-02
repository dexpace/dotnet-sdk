// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

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
    /// Wraps a synchronous transport as an <see cref="IAsyncHttpClient"/> by offloading each
    /// blocking <see cref="IHttpClient.Execute"/> call to the thread pool.
    /// </summary>
    /// <param name="client">The synchronous transport to wrap.</param>
    /// <returns>An async facade over <paramref name="client"/>.</returns>
    public static IAsyncHttpClient AsAsync(this IHttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new SyncToAsyncAdapter(client);
    }

    /// <summary>
    /// Wraps an asynchronous transport as an <see cref="IHttpClient"/> that blocks on the returned
    /// task. The blocking wait unwraps transport exceptions so callers see the original failure.
    /// </summary>
    /// <param name="client">The asynchronous transport to wrap.</param>
    /// <returns>A blocking facade over <paramref name="client"/>.</returns>
    public static IHttpClient AsBlocking(this IAsyncHttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new AsyncToSyncAdapter(client);
    }

    private sealed class SyncToAsyncAdapter(IHttpClient inner) : IAsyncHttpClient
    {
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            Task.Run(() => inner.Execute(request, options, cancellationToken), cancellationToken);

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    // AsBlocking's documented sync bridge (design §3.3): blocking on the async transport is its whole purpose.
#pragma warning disable RS0030
    private sealed class AsyncToSyncAdapter(IAsyncHttpClient inner) : IHttpClient
    {
        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
            inner.ExecuteAsync(request, options, cancellationToken).GetAwaiter().GetResult();

        public void Dispose() =>
            inner.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
#pragma warning restore RS0030
}
