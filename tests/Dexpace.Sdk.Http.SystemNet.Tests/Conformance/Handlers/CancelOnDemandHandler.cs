// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net.Http;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Conformance;

/// <summary>
/// Cancels every call in flight through it on demand, from inside the handler chain, with a source that is neither the caller's token nor a deadline.
/// Shared by linked source with the DI suite (phase 9, task 4.1): the parameterless constructor leaves <c>InnerHandler</c> for <c>IHttpClientFactory</c> to assign.
/// </summary>
internal sealed class CancelOnDemandHandler : DelegatingHandler
{
    private readonly CancellationTokenSource _cancel = new();

    public CancelOnDemandHandler()
    {
    }

    public CancelOnDemandHandler(HttpMessageHandler inner)
        : base(inner)
    {
    }

    internal void CancelInFlight() => _cancel.Cancel();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancel.Token);
        return await base.SendAsync(request, linked.Token);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancel.Dispose();
        }

        base.Dispose(disposing);
    }
}
