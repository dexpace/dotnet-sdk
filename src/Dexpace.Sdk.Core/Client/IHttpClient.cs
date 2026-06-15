// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Client;

/// <summary>
/// The synchronous transport SPI.
/// </summary>
/// <remarks>
/// The response body is not pre-buffered — callers are responsible for disposing the returned
/// <see cref="Response"/>. Most consumers should prefer <see cref="IAsyncHttpClient"/>; this
/// blocking variant exists for callers and call sites that cannot go async. See
/// <see cref="HttpClientExtensions"/> for sync/async bridges.
/// </remarks>
public interface IHttpClient : IDisposable
{
    /// <summary>
    /// Sends <paramref name="request"/> over the underlying transport and returns the matching
    /// <see cref="Response"/>. Implementations MUST NOT return <see langword="null"/>.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <returns>The response (caller owns disposal).</returns>
    Response Execute(Request request);
}
