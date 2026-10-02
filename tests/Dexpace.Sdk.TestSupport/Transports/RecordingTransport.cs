// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// An in-memory <see cref="IAsyncHttpClient"/> that records every request it receives and answers each one from a
/// responder (a fresh <c>200 OK</c> when none is given). An exception thrown by the responder is returned as a
/// faulted task, exactly as a real transport's failure would surface.
/// </summary>
/// <param name="respond">Builds the response for a request; <see langword="null"/> answers <c>200 OK</c>.</param>
public sealed class RecordingTransport(Func<Request, Response>? respond = null) : IAsyncHttpClient
{
    private readonly RequestLog _log = new();

    /// <summary>Every request received so far, in arrival order.</summary>
    public IReadOnlyList<Request> Requests => _log.Snapshot();

    /// <summary>The most recent request, or <see langword="null"/> before the first call.</summary>
    public Request? LastRequest => _log.Last;

    /// <summary>The number of calls received so far.</summary>
    public int CallCount => _log.Count;

    /// <summary>Whether <see cref="DisposeAsync"/> has been called.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default)
    {
        _log.Add(request);
        try
        {
            return Task.FromResult(respond is null ? TestResponses.Create(Status.Ok) : respond(request));
        }
#pragma warning disable CA1031 // Not swallowed: a responder's exception becomes the returned task's fault.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Task.FromException<Response>(ex);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
