// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Client;

/// <summary>
/// Turns a bare send function into a transport: <see cref="Create"/> for the asynchronous seam and
/// <see cref="CreateBlocking"/> for the synchronous one (SEAM-11).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why two names.</b> A C# lambda cannot implement an interface, so the function needs this shape. The factories are
/// <c>Create</c> and <c>CreateBlocking</c> rather than two overloads named <c>Create</c>, because a lambda that only
/// throws is ambiguous between a <c>Func&lt;…, Response&gt;</c> and a <c>Func&lt;…, Task&lt;Response&gt;&gt;</c> overload
/// (<c>CS0121</c>), and that is exactly the failing transport a conformance kit needs.
/// </para>
/// <para>
/// <b>Why a static class.</b> Each factory returns the interface, over a private adapter, instead of one public class
/// implementing both: a delegate is either blocking or asynchronous, and an object implementing both would make one
/// direction a bridge in disguise. The adapters perform no I/O of their own, so core still supplies no transport (SEAM-2).
/// </para>
/// <para>
/// <b>Failures (SEAM-16, ASYNC-2).</b> Every failure of the async form, including a <see langword="null"/> argument and
/// an exception the function throws before it returns a task, is delivered through the returned task. A
/// <see langword="null"/> task or <see langword="null"/> result faults with <see cref="InvalidOperationException"/>; the
/// blocking form throws the same.
/// </para>
/// <para>
/// <b>Lifecycle (SEAM-15).</b> Dispose is a no-op and the transport keeps working after it: the adapters hold nothing.
/// </para>
/// </remarks>
public static class DelegateHttpClient
{
    /// <summary>Creates an asynchronous transport that answers every call by invoking <paramref name="send"/>.</summary>
    /// <param name="send">The send function. It receives the exact request, options and token of each call.</param>
    /// <returns>An <see cref="IAsyncHttpClient"/> over <paramref name="send"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="send"/> is null.</exception>
    public static IAsyncHttpClient Create(Func<Request, RequestOptions, CancellationToken, Task<Response>> send)
    {
        ArgumentNullException.ThrowIfNull(send);
        return new AsyncAdapter(send);
    }

    /// <summary>Creates a blocking transport that answers every call by invoking <paramref name="send"/>.</summary>
    /// <param name="send">The send function. It receives the exact request, options and token of each call.</param>
    /// <returns>An <see cref="IHttpClient"/> over <paramref name="send"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="send"/> is null.</exception>
    public static IHttpClient CreateBlocking(Func<Request, RequestOptions, CancellationToken, Response> send)
    {
        ArgumentNullException.ThrowIfNull(send);
        return new BlockingAdapter(send);
    }

    private sealed class AsyncAdapter(Func<Request, RequestOptions, CancellationToken, Task<Response>> send) : IAsyncHttpClient
    {
        // Not async: a null argument is delivered through the task (ASYNC-2) and the real work is in the helper.
        public Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                return Task.FromException<Response>(new ArgumentNullException(nameof(request)));
            }

            return options is null
                ? Task.FromException<Response>(new ArgumentNullException(nameof(options)))
                : SendAsync(request, options, cancellationToken);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        // The async state machine turns a synchronous throw from the function into a faulted task.
        private async Task<Response> SendAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            var task = send(request, options, cancellationToken)
                ?? throw new InvalidOperationException("The send function returned a null task (SEAM-16).");
            return await task.ConfigureAwait(false)
                ?? throw new InvalidOperationException("The send function returned a null response (SEAM-16).");
        }
    }

    private sealed class BlockingAdapter(Func<Request, RequestOptions, CancellationToken, Response> send) : IHttpClient
    {
        public Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(options);
            return send(request, options, cancellationToken)
                ?? throw new InvalidOperationException("The send function returned a null response (SEAM-16).");
        }

        public void Dispose()
        {
        }
    }
}
