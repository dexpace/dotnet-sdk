// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Net.Sockets;

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>A loopback port that was bound and released, so connecting to it is refused.</summary>
internal static class FreePort
{
    /// <summary>Binds an ephemeral loopback port, releases it and returns its number.</summary>
    internal static int Release()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
