// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// An in-memory <see cref="IAsyncHttpClient"/> that answers call <c>n</c> with script entry <c>n</c> and records
/// every request. An entry is a <see cref="Response"/> (returned as is), an <see cref="Exception"/> (returned as a
/// faulted task), a <see cref="Func{TResult}"/> of <see cref="Response"/>, or a
/// <see cref="Func{T, TResult}"/> from <see cref="Request"/> to <see cref="Response"/> (invoked per call). A call
/// past the end of the script throws <see cref="InvalidOperationException"/>, so an unexpected extra attempt fails
/// the test instead of hanging it.
/// </summary>
public sealed class ScriptedTransport : IAsyncHttpClient, IHttpClient
{
    private readonly object[] _script;
    private readonly RequestLog _log = new();

    /// <summary>Creates a transport that plays <paramref name="script"/> in order, one entry per call.</summary>
    /// <param name="script">The entries, as described on the type.</param>
    public ScriptedTransport(params IEnumerable<object> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        _script = [.. script];
        foreach (var entry in _script)
        {
            if (entry is not (Response or Exception or Func<Response> or Func<Request, Response>))
            {
                throw new ArgumentException($"Unsupported script entry type: {entry?.GetType().FullName ?? "null"}.", nameof(script));
            }
        }
    }

    /// <summary>Every request received so far, in arrival order.</summary>
    public IReadOnlyList<Request> Requests => [.. _log.Snapshot().Select(call => call.Request)];

    /// <summary>Every call received so far, in arrival order, with the exact options and token instances.</summary>
    public IReadOnlyList<RecordedCall> Calls => _log.Snapshot();

    /// <summary>The most recent request, or <see langword="null"/> before the first call.</summary>
    public Request? LastRequest => _log.Last?.Request;

    /// <summary>The most recent call, or <see langword="null"/> before the first call.</summary>
    public RecordedCall? LastCall => _log.Last;

    /// <summary>The number of calls received so far, including one that ran past the end of the script.</summary>
    public int CallCount => _log.Count;

    /// <summary>Whether <see cref="DisposeAsync"/> has been called.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        var index = _log.Add(request, options, cancellationToken);
        if (index >= _script.Length)
        {
            throw new InvalidOperationException(
                $"ScriptedTransport exhausted: {_script.Length} entr(ies) scripted, call #{index + 1} received.");
        }

        return _script[index] switch
        {
            Response response => Task.FromResult(response),
            Exception exception => Task.FromException<Response>(exception),
            Func<Response> factory => Task.FromResult(factory()),
            Func<Request, Response> responder => Task.FromResult(responder(request)),
            var entry => throw new InvalidOperationException($"Unsupported script entry type: {entry.GetType()}."),
        };
    }

    /// <summary>The synchronous twin: shares the script and the request log with <see cref="ExecuteAsync"/>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="options">The per-call options.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The next scripted response; a scripted exception is thrown.</returns>
    public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        var index = _log.Add(request, options, cancellationToken);
        if (index >= _script.Length)
        {
            throw new InvalidOperationException(
                $"ScriptedTransport exhausted: {_script.Length} entr(ies) scripted, call #{index + 1} received.");
        }

        return _script[index] switch
        {
            Response response => response,
            Exception exception => throw exception,
            Func<Response> factory => factory(),
            Func<Request, Response> responder => responder(request),
            var entry => throw new InvalidOperationException($"Unsupported script entry type: {entry.GetType()}."),
        };
    }

    /// <summary>Marks the transport disposed.</summary>
    public void Dispose() => IsDisposed = true;

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}
