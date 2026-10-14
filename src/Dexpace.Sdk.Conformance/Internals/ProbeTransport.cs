// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// An in-memory transport on both faces for the invariant suite: a pipeline under test sends into it, and its
/// <see cref="Behavior"/> decides each send (numbered from 1) with the call's own token. It counts the sends, records the
/// requests and counts its own disposals, so an assertion can prove what reached the transport and that the SDK never closed
/// what it did not create (<c>XCUT-22</c>). It speaks no socket and reads no clock.
/// </summary>
internal sealed class ProbeTransport(ProbeTransport.Behavior behavior) : IAsyncHttpClient, IHttpClient
{
    private readonly object _lock = new();
    private readonly List<Request> _requests = [];
    private int _sends;
    private int _disposals;

    /// <summary>What the transport does with one send, on each face.</summary>
    /// <param name="Async">The asynchronous face.</param>
    /// <param name="Blocking">The blocking face.</param>
    internal sealed record Behavior(
        Func<Request, int, CancellationToken, Task<Response>> Async,
        Func<Request, int, CancellationToken, Response> Blocking)
    {
        /// <summary>Both faces answer by calling <paramref name="respond"/>, which may throw.</summary>
        /// <param name="respond">Builds the response of one send from the request and the 1-based send number.</param>
        internal static Behavior Of(Func<Request, int, Response> respond) =>
            new(
                (request, send, _) => Task.FromResult(respond(request, send)),
                (request, send, _) => respond(request, send));

        /// <summary>Answers every send with a fresh body-less response of status <paramref name="code"/>.</summary>
        /// <param name="code">The status code.</param>
        internal static Behavior Status(int code) => Of((request, _) => Empty(request, code));

        /// <summary>
        /// Blocks until the call's token fires and then throws <see cref="OperationCanceledException"/> carrying it: a transport
        /// that honours cancellation and has nothing to answer with. <paramref name="signalSent"/> is released once the send is in flight.
        /// </summary>
        /// <param name="signalSent">Completed when a send has begun, so the caller can cancel knowing a send is parked.</param>
        internal static Behavior HangUntilCancelled(TaskCompletionSource? signalSent = null) =>
            new(
                async (_, _, token) =>
                {
                    signalSent?.TrySetResult();
                    var cancelled = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using var registration = token.Register(static state => ((TaskCompletionSource<Response>)state!).TrySetCanceled(), cancelled);
                    return await cancelled.Task.ConfigureAwait(false);
                },
                (_, _, token) =>
                {
                    signalSent?.TrySetResult();
                    token.WaitHandle.WaitOne();
                    token.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("The hang ended without cancellation.");
                });
    }

    /// <summary>The number of sends received on either face.</summary>
    internal int Sends => Volatile.Read(ref _sends);

    /// <summary>How many times the transport was disposed.</summary>
    internal int Disposals => Volatile.Read(ref _disposals);

    /// <summary>The requests received, in arrival order.</summary>
    internal IReadOnlyList<Request> Requests
    {
        get
        {
            lock (_lock)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>A fresh body-less response of status <paramref name="code"/> to <paramref name="request"/>.</summary>
    /// <param name="request">The request answered.</param>
    /// <param name="code">The status code.</param>
    /// <param name="headers">Response headers, or none.</param>
    internal static Response Empty(Request request, int code, Headers? headers = null) =>
        new(request, Status.FromCode(code), Protocol.Http11, headers, null, null);

    /// <inheritdoc />
    public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        var send = Record(request);
        try
        {
            return behavior.Async(request, send, cancellationToken);
        }
#pragma warning disable CA1031 // A synchronous throw from the behaviour is the transport's failure, delivered through the task like any other.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return Task.FromException<Response>(ex);
        }
    }

    /// <inheritdoc />
    public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        behavior.Blocking(request, Record(request), cancellationToken);

    /// <inheritdoc />
    public void Dispose() => Interlocked.Increment(ref _disposals);

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposals);
        return ValueTask.CompletedTask;
    }

    private int Record(Request request)
    {
        lock (_lock)
        {
            _requests.Add(request);
        }

        return Interlocked.Increment(ref _sends);
    }
}
