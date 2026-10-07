// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// A dual-interface transport whose synchronous <c>Execute</c> answers and whose <c>ExecuteAsync</c> throws, so a test can
/// prove a synchronous send never reaches an async member (PIPE-28).
/// </summary>
public sealed class SyncFirstTransport(Func<Request, Response>? respond = null) : IAsyncHttpClient, IHttpClient
{
    private readonly RequestLog _log = new();

    /// <summary>The number of synchronous calls received.</summary>
    public int CallCount => _log.Count;

    /// <summary>The synchronous calls received.</summary>
    public IReadOnlyList<RecordedCall> Calls => _log.Snapshot();

    /// <inheritdoc/>
    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The asynchronous member of a sync-first transport was called.");

    /// <inheritdoc/>
    public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        _log.Add(request, options, cancellationToken);
        return respond is null ? TestResponses.Create(Status.Ok, request) : respond(request);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
