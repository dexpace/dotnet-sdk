// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>
/// The configuration keys the SDK publishes (CFG-14). They are published and never read: the section of a client is always the caller's
/// argument to <c>AddDexpaceClient</c>, so no key is baked in (OBS-35), and nothing in the package consults the environment on its own
/// (CFG-28).
/// </summary>
public static class DexpaceConfigurationKeys
{
    /// <summary>The conventional name of the configuration section that holds an SDK client's settings. A suggestion for the caller's own section, never read by the package (OBS-35).</summary>
    public const string SectionName = "Dexpace";

    /// <summary>The key, relative to a client's section, of the retry budget (<c>RetryOptions.MaxRetryAttempts</c>).</summary>
    public const string MaxRetryAttempts = "Retry:MaxRetryAttempts";

    /// <summary>The key, relative to a client's section, of the HTTP log level (<c>HttpLoggingOptions.Level</c>), read by name: <c>None</c>, <c>Headers</c> or <c>Body</c> (OBS-35).</summary>
    public const string LogLevel = "Logging:Level";

    /// <summary>The name of the environment variable that carries the proxy for plain HTTP requests, as <c>ProxyOptions.FromEnvironment</c> reads it.</summary>
    public const string HttpProxy = "HTTP_PROXY";

    /// <summary>The name of the environment variable that carries the proxy for HTTPS requests, as <c>ProxyOptions.FromEnvironment</c> reads it.</summary>
    public const string HttpsProxy = "HTTPS_PROXY";

    /// <summary>The name of the environment variable that lists the hosts that bypass the proxy, as <c>ProxyOptions.FromEnvironment</c> reads it.</summary>
    public const string NoProxy = "NO_PROXY";
}
