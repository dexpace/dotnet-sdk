// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// The construction-time warnings for proxy settings the native client cannot honour as written (<c>TRANSPORT-30</c>: such a
/// combination is surfaced, never silently dropped), event <see cref="DexpaceLogEvents.TransportProxyFeatureUnsupportedId"/>.
/// </summary>
/// <remarks>
/// Each message names a feature and the <see cref="ProxyOptions"/> member that causes it, taken from the closed set of literals
/// below. Nothing from the options is formatted into it: not the host, not a user name, not a password (<c>XCUT-18</c>).
/// </remarks>
internal static class ProxyWarnings
{
    private static readonly Action<ILogger, string, string, Exception?> s_unsupported = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(DexpaceLogEvents.TransportProxyFeatureUnsupportedId, DexpaceLogEvents.TransportProxyFeatureUnsupported),
        "The transport cannot honour a proxy setting as written: {Feature} (ProxyOptions.{Member}) (TRANSPORT-30).");

    /// <summary>Logs one warning per unhonourable combination in <paramref name="options"/>; a clean configuration logs nothing.</summary>
    /// <param name="logger">Receives the warnings.</param>
    /// <param name="options">The proxy about to be installed.</param>
    internal static void Emit(ILogger logger, ProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        var socks = options.Type is ProxyType.Socks4 or ProxyType.Socks5;
        var hasHook = options.ChallengeCredentials is not null;
        if (hasHook && options.UserName is not null)
        {
            s_unsupported(logger, "the credentials hook wins and the user name and password are ignored", nameof(ProxyOptions.UserName), null);
        }

        if (hasHook && socks)
        {
            s_unsupported(logger, "a SOCKS handshake has no challenge, so the hook is asked once per connection with the proxy scheme as the authentication type", nameof(ProxyOptions.ChallengeCredentials), null);
        }

        if (!hasHook && options.Type == ProxyType.Socks4 && options.Password is not null)
        {
            s_unsupported(logger, "SOCKS4 carries a user id only, so the password is ignored", nameof(ProxyOptions.Password), null);
        }
    }
}
