// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Client;

/// <summary>
/// The asynchronous transport SPI — the async-first counterpart of <see cref="IHttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Task{TResult}"/> is the SDK's canonical async contract: it is the lowest common
/// denominator every other .NET async pattern (channels, Rx, Dataflow) adapts to (SEAM-17). Transport
/// packages (such as <c>Dexpace.Sdk.Http.SystemNet</c>) adapt one HTTP library to this interface;
/// <c>core</c> ships no transport of its own.
/// </para>
/// <para>
/// <b>Contract (SEAM-11).</b> One request produces one response. The response body is never pre-buffered: the caller
/// reads and disposes it. The <see cref="RequestOptions"/> are optional and ignorable, and a transport that ignores
/// them behaves identically to one that honours <see cref="RequestOptions.Empty"/>. A bare send function is a transport
/// through <c>DelegateHttpClient</c>.
/// </para>
/// <para>
/// <b>Thread-safety (SEAM-12).</b> Implementations must be safe for concurrent calls from multiple threads;
/// per-call state must be confined to the call.
/// </para>
/// <para>
/// <b>Cancellation (SEAM-13).</b> A signalled cancellation token is a request to abort the in-flight exchange. If the
/// response has already been delivered, cancelling does NOT dispose the <see cref="Response"/> body; callers still own
/// <c>Dispose</c>.
/// </para>
/// <para>
/// <b>Lifecycle (SEAM-14, SEAM-15).</b> Implementations are <see cref="IAsyncDisposable"/>. Dispose is idempotent and
/// ownership-aware: only SDK-owned resources are released, and a caller-supplied
/// <c>System.Net.Http.HttpClient</c> is left untouched. What a call does after dispose is implementation-defined, and
/// each SDK-shipped implementation documents its own mode.
/// </para>
/// <para>
/// <b>Failures (SEAM-16, ASYNC-2).</b> The returned task never completes with <see langword="null"/>. A
/// <see langword="null"/> argument is rejected with <see cref="ArgumentNullException"/>, delivered through the
/// returned task rather than thrown synchronously.
/// </para>
/// <para>
/// <b>Breaking:</b> was <c>ExecuteAsync(Request, CancellationToken = default)</c>. The member takes the call's
/// <see cref="RequestOptions"/> and declares no parameter defaults, because a default on an implementation's member
/// would let <c>transport.ExecuteAsync(request, default)</c> bind it with <see langword="null"/> options. Callers that
/// import <c>Dexpace.Sdk.Core.Client</c> keep calling <c>ExecuteAsync(request, token)</c> through
/// <see cref="HttpClientExtensions.ExecuteAsync(IAsyncHttpClient, Request, CancellationToken)"/>.
/// </para>
/// </remarks>
public interface IAsyncHttpClient : IAsyncDisposable
{
    /// <summary>
    /// Sends <paramref name="request"/> over the underlying transport. The returned task completes
    /// with the matching <see cref="Response"/> (caller owns disposal) or faults with the transport
    /// failure. Implementations MUST NOT complete with <see langword="null"/> on success.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="options">
    /// The per-call options. A transport that does not read them behaves as it would for
    /// <see cref="RequestOptions.Empty"/>.
    /// </param>
    /// <param name="cancellationToken">A token to abort the exchange.</param>
    /// <returns>A task that completes with the response.</returns>
    Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken);
}
