// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The blocking face: sends through <see cref="IHttpClient.Execute"/> on a dedicated thread, so many blocked calls cannot
/// starve one another and the caller can still cancel. The token is handed to <c>Execute</c> and not to the task that runs it,
/// so a token that is already signalled still reaches the transport: cancellation of a blocking call is cooperative, and
/// observing the token is the transport's job (<c>TRANSPORT-3</c>'s own face).
/// </summary>
internal sealed class BlockingFaceTransport(IHttpClient client) : IFaceTransport
{
    /// <summary>The transport itself.</summary>
    internal IHttpClient Client { get; } = client;

    public TransportFace Face => TransportFace.Blocking;

    public async Task<Response> SendAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        var call = Task.Factory.StartNew(
            () => Client.Execute(request, options, cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
        return await call.ConfigureAwait(false)
            ?? throw new ConformanceException("Execute returned a null Response (TRANSPORT-23, SEAM-16).");
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }
}
