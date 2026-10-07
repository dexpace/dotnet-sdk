// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// The one warning <c>ProxyOptions.FromEnvironment</c> logs when it ignores a value (CFG-24). It names the variable and
/// the rule broken, never the value, which may hold a password.
/// </summary>
/// <remarks>
/// The <see cref="EventId"/> is provisional (20, <c>ProxyConfigurationIgnored</c>, R10): phase 5b's event scheme renumbers
/// it. The call is wrapped so that a throwing <see cref="ILogger"/> cannot make the resolver throw (OBS-20's rule ahead of
/// 5b's emission guard, which replaces this wrapper; P5a-28).
/// </remarks>
internal static class ProxyResolutionLog
{
    private static readonly Action<ILogger, string, string, Exception?> s_ignored = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(20, "ProxyConfigurationIgnored"),
        "Proxy configuration in {Variable} was ignored: {Reason}.");

    /// <summary>Logs that <paramref name="variable"/> was ignored for <paramref name="reason"/>.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="variable">The environment variable name.</param>
    /// <param name="reason">The rule broken, as a keyword.</param>
    internal static void Ignored(ILogger logger, string variable, string reason)
    {
        try
        {
            s_ignored(logger, variable, reason, null);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // A failing logger must not turn a rejected proxy value into an exception (CFG-24).
        }
    }
}
