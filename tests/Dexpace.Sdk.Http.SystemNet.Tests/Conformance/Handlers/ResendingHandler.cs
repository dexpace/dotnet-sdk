// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net.Http;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Conformance;

/// <summary>Sends every request twice through the inner handler, the native re-send of design section 11 item 18. Shared by linked source with the DI suite (phase 9, task 4.1).</summary>
internal sealed class ResendingHandler : DelegatingHandler
{
    public ResendingHandler()
    {
    }

    public ResendingHandler(HttpMessageHandler inner)
        : base(inner)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var first = await base.SendAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
