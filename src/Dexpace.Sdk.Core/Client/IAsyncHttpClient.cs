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
/// denominator every other .NET async pattern (channels, Rx, Dataflow) adapts to. Transport
/// packages (such as <c>Dexpace.Sdk.Http.SystemNet</c>) adapt one HTTP library to this interface;
/// <c>core</c> ships no transport of its own.
/// </para>
/// <para>
/// <b>Thread-safety.</b> Implementations must be safe for concurrent calls from multiple threads;
/// per-call state must be confined to the returned task.
/// </para>
/// <para>
/// <b>Cancellation.</b> A signalled cancellation token is a best-effort request to abort the
/// in-flight exchange. If the response has already been delivered, cancelling does NOT dispose
/// the <see cref="Response"/> body; callers still own <c>Dispose</c>.
/// </para>
/// <para>
/// <b>Lifecycle.</b> Implementations are <see cref="IAsyncDisposable"/>. Dispose is idempotent and
/// ownership-aware: only SDK-owned resources are released; a caller-supplied
/// <c>System.Net.Http.HttpClient</c> is left untouched.
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
    /// <param name="cancellationToken">A token to abort the exchange.</param>
    /// <returns>A task that completes with the response.</returns>
    Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default);
}
