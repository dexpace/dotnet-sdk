// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// A dual-interface transport that counts its disposals in both forms and answers 200 to every call.
/// </summary>
public sealed class DisposeCountingTransport : IAsyncHttpClient, IHttpClient
{
    private int _disposals;

    /// <summary>How many times <c>Dispose</c> or <c>DisposeAsync</c> ran.</summary>
    public int DisposeCount => Volatile.Read(ref _disposals);

    /// <inheritdoc/>
    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        Task.FromResult(TestResponses.Create(Status.Ok, request));

    /// <inheritdoc/>
    public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        TestResponses.Create(Status.Ok, request);

    /// <inheritdoc/>
    public void Dispose() => Interlocked.Increment(ref _disposals);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposals);
        return ValueTask.CompletedTask;
    }
}
