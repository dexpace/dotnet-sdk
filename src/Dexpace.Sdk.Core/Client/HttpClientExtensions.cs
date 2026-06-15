// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Client;

/// <summary>
/// Bridges between the synchronous <see cref="IHttpClient"/> and asynchronous
/// <see cref="IAsyncHttpClient"/> transport SPIs.
/// </summary>
public static class HttpClientExtensions
{
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
        public Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default) =>
            Task.Run(() => inner.Execute(request), cancellationToken);

        public ValueTask DisposeAsync()
        {
            inner.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AsyncToSyncAdapter(IAsyncHttpClient inner) : IHttpClient
    {
        public Response Execute(Request request) =>
            inner.ExecuteAsync(request).GetAwaiter().GetResult();

        public void Dispose() =>
            inner.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
