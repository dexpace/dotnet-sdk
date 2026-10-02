// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// The synchronous-seam counterpart of <see cref="RecordingTransport"/>: an in-memory <see cref="IHttpClient"/>
/// that records every request and answers each one from a responder (a fresh <c>200 OK</c> when none is given).
/// </summary>
/// <param name="respond">Builds the response for a request; <see langword="null"/> answers <c>200 OK</c>.</param>
public sealed class RecordingSyncTransport(Func<Request, Response>? respond = null) : IHttpClient
{
    private readonly RequestLog _log = new();

    /// <summary>Every request received so far, in arrival order.</summary>
    public IReadOnlyList<Request> Requests => [.. _log.Snapshot().Select(call => call.Request)];

    /// <summary>Every call received so far, in arrival order, with the exact options and token instances.</summary>
    public IReadOnlyList<RecordedCall> Calls => _log.Snapshot();

    /// <summary>The most recent request, or <see langword="null"/> before the first call.</summary>
    public Request? LastRequest => _log.Last?.Request;

    /// <summary>The most recent call, or <see langword="null"/> before the first call.</summary>
    public RecordedCall? LastCall => _log.Last;

    /// <summary>The number of calls received so far.</summary>
    public int CallCount => _log.Count;

    /// <summary>Whether <see cref="Dispose"/> has been called.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        _log.Add(request, options, cancellationToken);
        return respond is null ? TestResponses.Create(Status.Ok) : respond(request);
    }

    /// <inheritdoc />
    public void Dispose() => IsDisposed = true;
}
