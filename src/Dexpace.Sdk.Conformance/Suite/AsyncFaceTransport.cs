// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>The asynchronous face: sends through <see cref="IAsyncHttpClient.ExecuteAsync"/>.</summary>
internal sealed class AsyncFaceTransport(IAsyncHttpClient client) : IFaceTransport
{
    /// <summary>The transport itself, for the assertions that must call its interface directly (<c>TRANSPORT-21</c>).</summary>
    internal IAsyncHttpClient Client { get; } = client;

    public TransportFace Face => TransportFace.Async;

    public async Task<Response> SendAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        var task = Client.ExecuteAsync(request, options, cancellationToken)
            ?? throw new ConformanceException("ExecuteAsync returned a null Task instead of delivering the failure through it (SEAM-16, TRANSPORT-23).");
        return await task.ConfigureAwait(false)
            ?? throw new ConformanceException("The transport completed its task with a null Response (TRANSPORT-23, ASYNC-1, SEAM-16).");
    }

    public ValueTask DisposeAsync() => Client.DisposeAsync();
}
