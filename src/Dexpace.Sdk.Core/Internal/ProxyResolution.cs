// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// Resolves a <see cref="ProxyOptions"/> from environment-style variables (CFG-24 to CFG-28; P5a-15).
/// </summary>
/// <remarks>
/// The variable order is <c>HTTPS_PROXY</c>, <c>https_proxy</c>, <c>HTTP_PROXY</c>, <c>http_proxy</c>; the first present
/// non-empty value (after trimming ASCII whitespace, R8) is the candidate. When <c>GATEWAY_INTERFACE</c> is present
/// (a CGI host), upper-case <c>HTTP_PROXY</c> is skipped with a warning (and <c>http_proxy</c> too when a case-insensitive lookup returns the same value), because a CGI server copies a request's
/// <c>Proxy:</c> header into it (httpoxy). A malformed chosen value yields <see langword="null"/> and one warning; it
/// never falls through to the next variable.
/// </remarks>
internal static class ProxyResolution
{
    private static readonly string[] s_proxyVariables = ["HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy"];

#pragma warning disable RS0030 // CFG-28, design §8.2, P5a-17: the one sanctioned environment read; reached only by the explicit FromEnvironment().
    internal static Func<string, string?> DefaultLookup { get; } = Environment.GetEnvironmentVariable;
#pragma warning restore RS0030

    /// <summary>Resolves from the process environment, the one sanctioned read.</summary>
    /// <param name="logger">Receives the warning.</param>
    /// <returns>The proxy, or <see langword="null"/>.</returns>
    internal static ProxyOptions? ResolveFromProcess(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return Resolve(DefaultLookup, logger);
    }

    /// <summary>Resolves from <paramref name="environment"/>.</summary>
    /// <param name="environment">The variable lookup.</param>
    /// <param name="logger">Receives the warning.</param>
    /// <returns>The proxy, or <see langword="null"/> when none is configured, the value is invalid, or every target bypasses.</returns>
    internal static ProxyOptions? Resolve(Func<string, string?> environment, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        if (!TryChooseVariable(environment, logger, out var variable, out var value))
        {
            return null;
        }

        if (!ProxyUrlParser.TryParse(value, out var parsed, out var error))
        {
            ProxyResolutionLog.Ignored(logger, variable, error.ToString().ToLowerInvariant());
            return null;
        }

        var bypass = ReadBypassList(environment);
        if (bypass.BypassAll)
        {
            return null;
        }

        return new ProxyOptions
        {
            Type = parsed.Type,
            Host = parsed.Host,
            Port = parsed.Port,
            UserName = parsed.UserName,
            Password = parsed.Password,
            NonProxyHosts = bypass.Tokens,
        };
    }

    private static bool TryChooseVariable(Func<string, string?> environment, ILogger logger, out string variable, out string value)
    {
        foreach (var name in s_proxyVariables)
        {
            var candidate = Present(environment(name));
            if (candidate is null)
            {
                continue;
            }

            // Upper-case HTTP_PROXY is skipped under CGI. A lookup that ignores case (Windows, IConfiguration) would hand the
            // same attacker-controlled value back for http_proxy, so the lower-case spelling is honoured only when it
            // differs from a present HTTP_PROXY (the Ruby find_proxy rule).
            if (name is "HTTP_PROXY" or "http_proxy"
                && Present(environment("GATEWAY_INTERFACE")) is not null
                && (name == "HTTP_PROXY" || Present(environment("HTTP_PROXY")) == candidate))
            {
                ProxyResolutionLog.Ignored(logger, name, "cgi");
                continue;
            }

            variable = name;
            value = candidate;
            return true;
        }

        variable = string.Empty;
        value = string.Empty;
        return false;
    }

    private static NoProxyResult ReadBypassList(Func<string, string?> environment)
    {
        var value = Present(environment("NO_PROXY")) ?? Present(environment("no_proxy"));
        return value is null ? new NoProxyResult([], BypassAll: false) : NoProxyList.Parse(value);
    }

    // An empty or whitespace-only value is absent (R8); nothing inside the value is altered.
    private static string? Present(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
