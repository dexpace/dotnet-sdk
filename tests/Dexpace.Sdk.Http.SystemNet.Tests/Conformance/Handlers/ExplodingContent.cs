// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Net.Http;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Conformance;

/// <summary>Content whose length computation throws, so adapting a response that carries it fails after the response is live; it disposes the original content, which owns the connection.</summary>
internal sealed class ExplodingContent(HttpContent original) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;

    protected override bool TryComputeLength(out long length) => throw new InvalidOperationException("adaptation failed");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            original.Dispose();
        }

        base.Dispose(disposing);
    }
}
