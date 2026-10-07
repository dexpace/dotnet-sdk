// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dexpace.Sdk.Core.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// An HTTP or SOCKS proxy for the transport to use, with its bypass list and credentials (CFG-22, CFG-23, CFG-24;
/// design §8.2).
/// </summary>
/// <remarks>
/// <para>
/// A sealed record whose collection member is copied at <see langword="init"/> (CFG-8). It is <b>not</b> a property of
/// <see cref="DexpaceClientOptions"/>: it configures the transport, which the client options do not reach. Installing it
/// is the transport's job (phase 8b); this type is the model and <see cref="FromEnvironment(Func{string, string?}, ILogger)"/>
/// is the resolver, and nothing in core calls the resolver (CFG-28).
/// </para>
/// <para>
/// <see cref="ToString"/> masks credentials (TRANSPORT-30: credentials MUST NOT be logged): a present user name or password
/// prints <c>***</c>, an absent one <c>(none)</c>, so a log line tells "configured" from "not configured" without the
/// value. Equality compares <see cref="NonProxyHosts"/> by content and <see cref="ChallengeCredentials"/> by reference.
/// </para>
/// <para>
/// <see cref="ChallengeCredentials"/> is typed <see cref="ICredentials"/>: .NET's native proxy authentication asks an
/// <see cref="ICredentials"/> for the proxy and the scheme when a 407 arrives, which is the challenge-driven hook
/// CFG-22 names (P5a-13). <see cref="UserName"/> and <see cref="Password"/> are the Basic fallback.
/// </para>
/// </remarks>
public sealed record ProxyOptions
{
    private static readonly Regex[] s_noPatterns = [];

    private static readonly IReadOnlyList<string> s_noHosts = Array.AsReadOnly<string>([]);

    private IReadOnlyList<string> _nonProxyHosts = s_noHosts;
    private Regex[] _patterns = s_noPatterns;

    /// <summary>The proxy kind. Defaults to <see cref="ProxyType.Http"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="ProxyType"/>.</exception>
    public ProxyType Type
    {
        get;
        init
        {
            field = value switch
            {
                ProxyType.Http or ProxyType.Socks4 or ProxyType.Socks5 => value,
                _ => throw new ArgumentOutOfRangeException(nameof(value), "The proxy type is not a defined ProxyType."),
            };
        }
    }

    /// <summary>The proxy host: a name, an IPv4 address or a bare IPv6 address (no brackets).</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The value is blank or holds whitespace, a control character, or one of <c>/ @ [ ]</c>.</exception>
    public required string Host
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = IsValidHost(value)
                ? value
                : throw new ArgumentException("The proxy host is blank or contains a character that is not allowed in a host.", nameof(value));
        }
    }

    /// <summary>The proxy port, 0 to 65535 (CFG-25).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside 0 to 65535.</exception>
    public required int Port
    {
        get;
        init
        {
            field = value is >= 0 and <= 65535
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), "The proxy port must be in 0 to 65535.");
        }
    }

    /// <summary>
    /// The ordered bypass globs (CFG-23): <c>*</c> any run, <c>?</c> one character, everything else literal, full-string and
    /// case-insensitive. Copied and compiled once when set; a leading dot is a literal, unlike curl (P5a-14).
    /// </summary>
    /// <exception cref="ArgumentNullException">The value or an entry is <see langword="null"/>.</exception>
    public IReadOnlyList<string> NonProxyHosts
    {
        get => _nonProxyHosts;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            var copy = new string[value.Count];
            var compiled = new Regex[copy.Length];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = value[i] ?? throw new ArgumentNullException(nameof(value), "A bypass pattern is null.");
                compiled[i] = ProxyGlob.Compile(copy[i]);
            }

            _nonProxyHosts = Array.AsReadOnly(copy);
            _patterns = compiled;
        }
    }

    /// <summary>The proxy user name for Basic authentication, or <see langword="null"/>.</summary>
    public string? UserName { get; init; }

    /// <summary>The proxy password, or <see langword="null"/>. Never rendered by <see cref="ToString"/>.</summary>
    public string? Password { get; init; }

    /// <summary>The challenge-driven credentials slot (CFG-22; P5a-13), or <see langword="null"/>.</summary>
    public ICredentials? ChallengeCredentials { get; init; }

    /// <summary>Whether every target bypasses the proxy (CFG-27's explicit flag).</summary>
    public bool BypassAll { get; init; }

    internal Regex[] CompiledPatterns => _patterns;

    /// <summary>Whether a request to <paramref name="host"/> should bypass the proxy (CFG-23).</summary>
    /// <remarks>
    /// True when <see cref="BypassAll"/> is set or any glob matches the whole host, ignoring case. Write
    /// <c>*.internal.example.com</c>, not <c>.internal.example.com</c>: a leading dot is a literal here, unlike curl and
    /// <c>HttpClient.DefaultProxy</c> (P5a-14).
    /// </remarks>
    /// <param name="host">The target host name.</param>
    /// <returns><see langword="true"/> to connect directly.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public bool IsBypassed(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return BypassAll || ProxyGlob.Matches(_patterns, host);
    }

    /// <summary>Resolves a proxy from the process environment, logging nothing (CFG-24).</summary>
    /// <inheritdoc cref="FromEnvironment(Func{string, string?}, ILogger)" path="/remarks"/>
    /// <returns>The proxy, or <see langword="null"/>.</returns>
    public static ProxyOptions? FromEnvironment() => ProxyResolution.ResolveFromProcess(NullLogger.Instance);

    /// <summary>Resolves a proxy from the process environment (CFG-24).</summary>
    /// <inheritdoc cref="FromEnvironment(Func{string, string?}, ILogger)" path="/remarks"/>
    /// <param name="logger">Receives one warning when a value is ignored.</param>
    /// <returns>The proxy, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
    public static ProxyOptions? FromEnvironment(ILogger logger) => ProxyResolution.ResolveFromProcess(logger);

    /// <summary>Resolves a proxy through <paramref name="environment"/>, for tests and for configuration-backed lookups (CFG-24 to CFG-27).</summary>
    /// <remarks>
    /// <para>
    /// <b>Variables.</b> <c>HTTPS_PROXY</c>, then <c>https_proxy</c>, <c>HTTP_PROXY</c>, <c>http_proxy</c>; the first present
    /// non-empty value is chosen (an empty or whitespace-only value is absent). One proxy serves every target, so
    /// <c>HTTPS_PROXY</c> is preferred for <c>http://</c> targets too. When <c>GATEWAY_INTERFACE</c> is present,
    /// upper-case <c>HTTP_PROXY</c> is skipped with a warning (httpoxy). A malformed chosen value does <b>not</b> fall
    /// through to the next variable.
    /// </para>
    /// <para>
    /// <b>URL.</b> <c>scheme://[user[:password]@]host:port[/]</c>. The scheme is required: <c>http</c>, <c>socks4</c>,
    /// <c>socks4a</c> (mapped to <see cref="ProxyType.Socks4"/>), <c>socks5</c> or <c>socks5h</c> (mapped to
    /// <see cref="ProxyType.Socks5"/>); <c>https</c> and anything else is invalid rather than silently downgraded. The
    /// port must be explicit, all ASCII digits, in 0 to 65535, read from the raw authority (never <c>Uri.Port</c>).
    /// User name and password are percent-decoded; a <c>+</c> stays a <c>+</c>, and a malformed escape is invalid.
    /// </para>
    /// <para>
    /// <b>Bypass.</b> <c>NO_PROXY</c>, then <c>no_proxy</c>: split on commas not preceded by a backslash, empty fragments
    /// dropped, <c>\,</c> unescaped, trimmed, in that order. Exactly one bare <c>*</c> routes every target directly and
    /// returns <see langword="null"/>; <c>*</c> in a longer list is a glob. The pipe-separated system-property form has no
    /// .NET source; an explicit <see cref="ProxyOptions"/> value stands in for it.
    /// </para>
    /// <para>
    /// <b>Never throws</b> for its input: every failure is <see langword="null"/> and one warning naming the variable and
    /// the rule, never the value. A throwing <paramref name="environment"/> or a fatal exception propagates.
    /// This differs from <c>HttpClient.DefaultProxy</c>, which does not bypass on <c>NO_PROXY=*</c>, defaults an absent
    /// port and returns credentials in the proxy URI.
    /// </para>
    /// </remarks>
    /// <param name="environment">Looks a variable up by name; returns <see langword="null"/> for an absent one.</param>
    /// <param name="logger">Receives one warning when a value is ignored.</param>
    /// <returns>The proxy, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> or <paramref name="logger"/> is null.</exception>
    public static ProxyOptions? FromEnvironment(Func<string, string?> environment, ILogger logger) =>
        ProxyResolution.Resolve(environment, logger);

    /// <summary>Equality over every member; <see cref="NonProxyHosts"/> by ordinal content, <see cref="ChallengeCredentials"/> by reference.</summary>
    /// <param name="other">The value to compare with.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    public bool Equals(ProxyOptions? other)
    {
        if (other is null)
        {
            return false;
        }

        return ReferenceEquals(this, other)
            || (Type == other.Type
                && string.Equals(Host, other.Host, StringComparison.Ordinal)
                && Port == other.Port
                && _nonProxyHosts.SequenceEqual(other._nonProxyHosts, StringComparer.Ordinal)
                && string.Equals(UserName, other.UserName, StringComparison.Ordinal)
                && string.Equals(Password, other.Password, StringComparison.Ordinal)
                && ReferenceEquals(ChallengeCredentials, other.ChallengeCredentials)
                && BypassAll == other.BypassAll);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Host, StringComparer.Ordinal);
        hash.Add(Port);
        foreach (var pattern in _nonProxyHosts)
        {
            hash.Add(pattern, StringComparer.Ordinal);
        }

        hash.Add(UserName, StringComparer.Ordinal);
        hash.Add(Password, StringComparer.Ordinal);
        hash.Add(ChallengeCredentials is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ChallengeCredentials));
        hash.Add(BypassAll);
        return hash.ToHashCode();
    }

    /// <summary>Renders every member with credentials masked (CFG-22; TRANSPORT-30).</summary>
    /// <returns>For example <c>ProxyOptions { Type = Http, Host = proxy.internal, Port = 3128, … UserName = ***, Password = *** … }</c>.</returns>
    public override string ToString()
    {
        var text = new StringBuilder("ProxyOptions { Type = ").Append(Type);
        text.Append(", Host = ").Append(Host.Contains(':', StringComparison.Ordinal) ? "[" + Host + "]" : Host);
        text.Append(", Port = ").Append(Port);
        text.Append(", NonProxyHosts = [").AppendJoin(", ", _nonProxyHosts).Append(']');
        text.Append(", UserName = ").Append(UserName is null ? "(none)" : "***");
        text.Append(", Password = ").Append(Password is null ? "(none)" : "***");
        text.Append(", ChallengeCredentials = ").Append(ChallengeCredentials is null ? "(none)" : "set");
        text.Append(", BypassAll = ").Append(BypassAll ? "True" : "False").Append(" }");
        return text.ToString();
    }

    internal static bool IsValidHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        foreach (var c in host)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c) || c is '/' or '@' or '[' or ']')
            {
                return false;
            }
        }

        return true;
    }
}
