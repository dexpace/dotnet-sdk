// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Client;

/// <summary>
/// The synchronous transport SPI.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contract (SEAM-11).</b> One request produces one response. The response body is not pre-buffered — callers are
/// responsible for disposing the returned <see cref="Response"/>. The <see cref="RequestOptions"/> are optional and
/// ignorable: a transport that ignores them behaves identically to one that honours <see cref="RequestOptions.Empty"/>.
/// Most consumers should prefer <see cref="IAsyncHttpClient"/>; this blocking variant exists for callers and call sites
/// that cannot go async. A bare blocking send function is a transport through
/// <c>DelegateHttpClient.CreateBlocking</c>, and <see cref="HttpClientExtensions"/> holds the sync/async bridges.
/// </para>
/// <para>
/// <b>Thread-safety (SEAM-12).</b> Implementations must be safe for concurrent calls from multiple threads; per-call
/// state must be confined to the call.
/// </para>
/// <para>
/// <b>Cancellation (SEAM-13).</b> The call takes a <see cref="CancellationToken"/>. Honouring it inside blocking I/O is
/// the implementation's; the reference transport only does so once phase 8b lands a real synchronous send.
/// </para>
/// <para>
/// <b>Lifecycle (SEAM-14, SEAM-15).</b> Dispose is idempotent and ownership-aware. What a call does after dispose is
/// implementation-defined, and each SDK-shipped implementation documents its own mode.
/// </para>
/// <para>
/// <b>Failures (SEAM-16).</b> The call never returns <see langword="null"/>, and a <see langword="null"/> argument is
/// rejected with <see cref="ArgumentNullException"/>.
/// </para>
/// <para>
/// <b>Breaking:</b> was <c>Execute(Request)</c>. The member takes the call's <see cref="RequestOptions"/> and a token,
/// with no parameter defaults; callers use <see cref="HttpClientExtensions.Execute(IHttpClient, Request, CancellationToken)"/>
/// for the option-less call.
/// </para>
/// </remarks>
public interface IHttpClient : IDisposable
{
    /// <summary>
    /// Sends <paramref name="request"/> over the underlying transport and returns the matching
    /// <see cref="Response"/>. Implementations MUST NOT return <see langword="null"/>.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="options">
    /// The per-call options. A transport that does not read them behaves as it would for
    /// <see cref="RequestOptions.Empty"/>.
    /// </param>
    /// <param name="cancellationToken">A token to abort the exchange.</param>
    /// <returns>The response (caller owns disposal).</returns>
    Response Execute(Request request, RequestOptions options, CancellationToken cancellationToken);
}
