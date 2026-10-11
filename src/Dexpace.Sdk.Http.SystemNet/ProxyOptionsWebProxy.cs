// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Net;
using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// An <see cref="IWebProxy"/> over an explicit <see cref="ProxyOptions"/> (<c>TRANSPORT-30</c>, <c>CFG-22</c>, <c>CFG-23</c>,
/// <c>CFG-27</c>, <c>CFG-28</c>; design §8.2, phase 8b P8b-5), installed on the handler the transport creates and nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// It replaces the runtime's process-wide default proxy, which reads <c>HTTP(S)_PROXY</c> and <c>NO_PROXY</c> implicitly and
/// differs from the specification in four ways (no <c>NO_PROXY=*</c> bypass, port defaulting, userinfo in the proxy URI, regex
/// bypass entries). Here the bypass list is <see cref="ProxyOptions.IsBypassed(string)"/>'s glob dialect, the proxy URI never
/// carries userinfo (so no runtime log or exception message can print a credential), and the credentials are either the caller's
/// <see cref="ProxyOptions.ChallengeCredentials"/> or, for a user name alone, <see cref="BasicOnlyCredentials"/>.
/// </para>
/// <para>
/// Stateless after construction, so one instance serves every connection concurrently. It reads no environment variable and
/// no process-wide default (<c>NoImplicitEnvironmentReadArchitectureTests</c> scans for it).
/// </para>
/// </remarks>
internal sealed class ProxyOptionsWebProxy : IWebProxy
{
    private readonly ProxyOptions _options;
    private readonly Uri _proxyUri;
    private readonly ICredentials? _credentials;

    internal ProxyOptionsWebProxy(ProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _proxyUri = BuildProxyUri(options);
        _credentials = options.ChallengeCredentials
            ?? (options.UserName is { } userName ? new BasicOnlyCredentials(options.Type, userName, options.Password) : null);
    }

    /// <summary>Not settable: nothing outside the options may inject a credential into the proxy.</summary>
    /// <exception cref="NotSupportedException">On set.</exception>
    public ICredentials? Credentials
    {
        get => _credentials;
        set => throw new NotSupportedException("The proxy's credentials come from ProxyOptions and cannot be replaced.");
    }

    public Uri GetProxy(Uri destination) => _proxyUri;

    public bool IsBypassed(Uri host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return _options.IsBypassed(host.IdnHost);
    }

    private static Uri BuildProxyUri(ProxyOptions options)
    {
        var scheme = options.Type switch
        {
            ProxyType.Socks4 => "socks4",
            ProxyType.Socks5 => "socks5",
            _ => "http",
        };
        var host = options.Host.Contains(':', StringComparison.Ordinal) ? "[" + options.Host + "]" : options.Host;
        var text = string.Create(CultureInfo.InvariantCulture, $"{scheme}://{host}:{options.Port}/");
        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            ? uri
            : throw new ArgumentException("ProxyOptions.Host and Port do not form a usable proxy address.", nameof(options));
    }
}
