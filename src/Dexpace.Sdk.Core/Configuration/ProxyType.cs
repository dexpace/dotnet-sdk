// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>The kind of proxy a <see cref="ProxyOptions"/> describes (CFG-22).</summary>
public enum ProxyType
{
    /// <summary>An HTTP proxy.</summary>
    Http = 0,

    /// <summary>A SOCKS4 proxy. A resolved <c>socks4a</c> URL maps here.</summary>
    Socks4 = 1,

    /// <summary>A SOCKS5 proxy. A resolved <c>socks5h</c> URL maps here.</summary>
    Socks5 = 2,
}
