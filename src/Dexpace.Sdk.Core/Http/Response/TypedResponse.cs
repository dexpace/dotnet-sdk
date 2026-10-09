// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// A received response whose body has not been parsed yet: the metadata is readable at once, and the typed value is parsed
/// lazily, once, on the first <see cref="GetValueAsync"/> (HTTP-44, HTTP-45; P7a-14, P7a-15).
/// </summary>
/// <typeparam name="T">The parsed value type.</typeparam>
/// <remarks>
/// <para>
/// <b>One parse.</b> The first <see cref="GetValueAsync"/> runs the <see cref="IResponseHandler{T}"/> on the
/// <see cref="CancellationToken"/> given at construction; every later call, concurrent or sequential, gets the same result. A
/// success (a <see langword="null"/> included) is memoized, and so is a failure: the same exception object is thrown on every
/// access, and the handler is not run again. A cancelled construction token is a failure like any other.
/// </para>
/// <para>
/// <b>No caller blocks another.</b> Exactly-once is a compare-and-swap on a <see cref="TaskCompletionSource{TResult}"/>, not
/// a lock and not <c>Lazy&lt;Task&lt;T&gt;&gt;</c> (whose monitor is held across the factory's synchronous prefix, which for a
/// buffered body is the whole parse): a concurrent first caller gets an incomplete task at once and awaits it.
/// </para>
/// <para>
/// <b>A caller's token cancels only that caller's wait.</b> <c>GetValueAsync(token)</c> stops waiting when <c>token</c> fires,
/// but the parse keeps running under the construction token for everyone else, so one impatient reader cannot poison the memo.
/// </para>
/// <para>
/// <b>Ownership.</b> The handler owns and disposes the response on its own paths, so a caller who never calls
/// <see cref="GetValueAsync"/> must dispose this wrapper. Disposal forwards to the response's latched dispose (HTTP-43). The
/// wrapped response is deliberately not exposed: a body accessor would let a caller consume the single-use body behind the memo.
/// </para>
/// </remarks>
public sealed class TypedResponse<T> : IAsyncDisposable, IDisposable
{
    private readonly Response _response;
    private readonly IResponseHandler<T> _handler;
    private readonly CancellationToken _cancellationToken;
    private Task<T>? _value;

    /// <summary>Wraps <paramref name="response"/>, to be parsed lazily by <paramref name="handler"/>.</summary>
    /// <param name="response">The received response. Not parsed here.</param>
    /// <param name="handler">The handler that turns the response into a value, and disposes it.</param>
    /// <param name="cancellationToken">The token the (single) parse runs under; the call's token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="handler"/> is <see langword="null"/>.</exception>
    public TypedResponse(Response response, IResponseHandler<T> handler, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(handler);
        _response = response;
        _handler = handler;
        _cancellationToken = cancellationToken;
        Request = response.Request;
        Status = response.Status;
        Headers = response.Headers;
        Protocol = response.Protocol;
        ReasonPhrase = response.ReasonPhrase;
    }

    /// <summary>The request that produced the response. Readable without touching the body.</summary>
    public Request.Request Request { get; }

    /// <summary>The status. Readable without touching the body.</summary>
    public Status Status { get; }

    /// <summary>The response headers. Readable without touching the body.</summary>
    public Common.Headers Headers { get; }

    /// <summary>The negotiated protocol. Readable without touching the body.</summary>
    public Common.Protocol Protocol { get; }

    /// <summary>The reason phrase, or <see langword="null"/>. Readable without touching the body.</summary>
    public string? ReasonPhrase { get; }

    /// <summary>Gets the parsed value, parsing on the first call and returning the memoized outcome on every later one.</summary>
    /// <param name="cancellationToken">
    /// Cancels this caller's wait only; the parse runs under the construction token and is not cancelled by it.
    /// </param>
    /// <returns>The value; or the memoized failure, thrown as the same exception object each time.</returns>
    public ValueTask<T> GetValueAsync(CancellationToken cancellationToken = default)
    {
        var task = Volatile.Read(ref _value) ?? Start();
        if (!cancellationToken.CanBeCanceled)
        {
            return new ValueTask<T>(task);
        }

        // SEAM-30: the banned WaitAsync is safe here. The result lives in the memoized task itself, so a caller that stops
        // waiting orphans nothing; the handler owns and disposes the response.
#pragma warning disable RS0030
        return new ValueTask<T>(task.WaitAsync(cancellationToken));
#pragma warning restore RS0030
    }

    /// <summary>Disposes the wrapped response, at most once (HTTP-43).</summary>
    public void Dispose() => _response.Dispose();

    /// <summary>Disposes the wrapped response asynchronously, at most once (HTTP-43).</summary>
    /// <returns>A task that completes when the response has been released.</returns>
    public ValueTask DisposeAsync() => _response.DisposeAsync();

    private Task<T> Start()
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Interlocked.CompareExchange(ref _value, completion.Task, null) is { } winner)
        {
            return winner;
        }

        // Not a background launch: RunAsync runs inline on the winning caller's thread until its first await, so
        // BannedSymbols' background-launch rule does not apply. It never throws to here: every outcome lands in the source.
        _ = RunAsync(completion);
        return completion.Task;
    }

    private async Task RunAsync(TaskCompletionSource<T> completion)
    {
        try
        {
            var result = await _handler.HandleAsync(_response, _cancellationToken).ConfigureAwait(false);

            // SEAM-30: the result is memoized in the task itself; nothing is orphaned if no caller ever awaits it.
#pragma warning disable RS0030
            completion.TrySetResult(result);
#pragma warning restore RS0030
        }
        catch (Exception ex)
        {
            // Every exception, cancellation included, so awaiting rethrows the same object on every access. A fatal one is
            // recorded first and then not swallowed.
            completion.TrySetException(ex);
            if (ExceptionFacts.IsFatal(ex))
            {
                throw;
            }
        }
    }
}
