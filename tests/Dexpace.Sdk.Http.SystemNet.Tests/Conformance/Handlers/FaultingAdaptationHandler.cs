// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net.Http;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Conformance;

/// <summary>
/// Takes the live response from the inner handler and swaps its content for one that fails the length computation the
/// adapter does, so adapting it throws after the response is live. The failing content disposes the original content, which
/// owns the connection, so a transport that disposes the response on a failed adaptation releases it (TRANSPORT-22).
/// Shared by linked source with the DI suite (phase 9, task 4.1).
/// </summary>
internal sealed class FaultingAdaptationHandler : DelegatingHandler
{
    public FaultingAdaptationHandler()
    {
    }

    public FaultingAdaptationHandler(HttpMessageHandler inner)
        : base(inner)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var live = await base.SendAsync(request, cancellationToken);
        live.Content = new ExplodingContent(live.Content);
        return live;
    }
}
