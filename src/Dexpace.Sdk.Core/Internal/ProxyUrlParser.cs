// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Core.Internal;

/// <summary>Why a proxy URL was rejected; the lower-cased name is the warning's reason keyword.</summary>
internal enum ProxyUrlError
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>The text is not a URL of the form <c>scheme://authority</c>.</summary>
    Unparseable = 1,

    /// <summary>The scheme is missing or not one of <c>http</c>, <c>socks4</c>, <c>socks4a</c>, <c>socks5</c>, <c>socks5h</c>.</summary>
    Scheme = 2,

    /// <summary>The host is blank or carries a character a host cannot.</summary>
    Host = 3,

    /// <summary>The port is absent, not all ASCII digits, or outside 0 to 65535.</summary>
    Port = 4,

    /// <summary>A path other than <c>/</c>, a query or a fragment follows the authority.</summary>
    Path = 5,

    /// <summary>The user name or password carries a malformed percent escape.</summary>
    Credentials = 6,
}

/// <summary>A proxy URL taken apart.</summary>
/// <param name="Type">The proxy kind.</param>
/// <param name="Host">The bare host (an IPv6 address without its brackets).</param>
/// <param name="Port">The explicit port.</param>
/// <param name="UserName">The decoded user name, or <see langword="null"/> when no credentials were given.</param>
/// <param name="Password">The decoded password, or <see langword="null"/>.</param>
internal readonly record struct ParsedProxy(ProxyType Type, string Host, int Port, string? UserName, string? Password);

/// <summary>
/// The proxy URL grammar of <c>FromEnvironment</c> (CFG-24, CFG-25; P5a-15 item 3, R6, R7): <c>scheme://[user[:password]@]host:port[/]</c>.
/// </summary>
/// <remarks>
/// It never calls <see cref="Uri"/>: <c>new Uri("http://p").Port</c> is <c>80</c>, so <c>Uri</c> cannot tell an explicit
/// port from an absent one, and the port is read from the raw authority text. It never throws; every failure is a
/// <see cref="ProxyUrlError"/>.
/// </remarks>
internal static class ProxyUrlParser
{
    private const int MaxPort = 65535;

    /// <summary>Parses <paramref name="value"/>.</summary>
    /// <param name="value">The URL text, already trimmed.</param>
    /// <param name="parsed">The parts, on success.</param>
    /// <param name="error">The reason, on failure.</param>
    /// <returns><see langword="true"/> on success.</returns>
    internal static bool TryParse(string value, out ParsedProxy parsed, out ProxyUrlError error)
    {
        parsed = default;
        if (!TryReadScheme(value, out var type, out var rest, out error))
        {
            return false;
        }

        if (!TrySplitAuthority(rest, out var authority, out error))
        {
            return false;
        }

        var at = authority.LastIndexOf('@');
        var userInfo = at >= 0 ? authority[..at] : null;
        var hostPort = at >= 0 ? authority[(at + 1)..] : authority;
        string? user = null;
        string? password = null;
        if (userInfo is not null && !TryReadCredentials(userInfo, out user, out password, out error))
        {
            return false;
        }

        if (!TryReadHostAndPort(hostPort, out var host, out var port, out error))
        {
            return false;
        }

        parsed = new ParsedProxy(type, host, port, user, password);
        return true;
    }

    private static bool TryReadScheme(string value, out ProxyType type, out string rest, out ProxyUrlError error)
    {
        type = ProxyType.Http;
        rest = string.Empty;
        error = ProxyUrlError.None;
        var separator = value.IndexOf("://", StringComparison.Ordinal);
        if (separator < 0)
        {
            // "proxy.example.com:8080" reads as a scheme followed by a colon: a scheme problem, not garbage.
            var colon = value.IndexOf(':', StringComparison.Ordinal);
            error = colon > 0 && IsSchemeToken(value.AsSpan(0, colon)) ? ProxyUrlError.Scheme : ProxyUrlError.Unparseable;
            return false;
        }

        var scheme = value.AsSpan(0, separator);
        if (!IsSchemeToken(scheme))
        {
            error = ProxyUrlError.Unparseable;
            return false;
        }

        rest = value[(separator + 3)..];
        if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            type = ProxyType.Http;
        }
        else if (scheme.Equals("socks4", StringComparison.OrdinalIgnoreCase) || scheme.Equals("socks4a", StringComparison.OrdinalIgnoreCase))
        {
            type = ProxyType.Socks4;
        }
        else if (scheme.Equals("socks5", StringComparison.OrdinalIgnoreCase) || scheme.Equals("socks5h", StringComparison.OrdinalIgnoreCase))
        {
            type = ProxyType.Socks5;
        }
        else
        {
            error = ProxyUrlError.Scheme;
            return false;
        }

        return true;
    }

    private static bool IsSchemeToken(ReadOnlySpan<char> scheme)
    {
        if (scheme.IsEmpty || !char.IsAsciiLetter(scheme[0]))
        {
            return false;
        }

        foreach (var c in scheme)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySplitAuthority(string rest, out string authority, out ProxyUrlError error)
    {
        error = ProxyUrlError.None;
        var end = rest.AsSpan().IndexOfAny('/', '?', '#');
        authority = end < 0 ? rest : rest[..end];
        if (authority.Length == 0)
        {
            error = ProxyUrlError.Unparseable;
            return false;
        }

        if (end >= 0 && rest[end..] != "/")
        {
            error = ProxyUrlError.Path;
            return false;
        }

        return true;
    }

    private static bool TryReadCredentials(string userInfo, out string? user, out string? password, out ProxyUrlError error)
    {
        user = null;
        password = null;
        error = ProxyUrlError.None;
        if (userInfo.Contains('@', StringComparison.Ordinal))
        {
            error = ProxyUrlError.Unparseable;
            return false;
        }

        var colon = userInfo.IndexOf(':', StringComparison.Ordinal);
        var rawUser = colon >= 0 ? userInfo[..colon] : userInfo;
        var rawPassword = colon >= 0 ? userInfo[(colon + 1)..] : null;
        if (!TryDecode(rawUser, out var decodedUser)
            || (rawPassword is not null && !TryDecode(rawPassword, out _)))
        {
            error = ProxyUrlError.Credentials;
            return false;
        }

        // An empty user name means "no credentials", whatever follows the colon (R7).
        if (decodedUser.Length == 0)
        {
            return true;
        }

        user = decodedUser;
        if (rawPassword is not null)
        {
            _ = TryDecode(rawPassword, out var decodedPassword);
            password = decodedPassword;
        }

        return true;
    }

    // Percent-decoding that, unlike Uri.UnescapeDataString, rejects a '%' not followed by two hex digits instead of
    // leaving it in place (R7), and rejects bytes that are not UTF-8. A '+' stays a '+'.
    private static bool TryDecode(string text, out string decoded)
    {
        decoded = text;
        if (!text.Contains('%', StringComparison.Ordinal))
        {
            return true;
        }

        var bytes = new List<byte>(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '%')
            {
                if (i + 2 >= text.Length || !char.IsAsciiHexDigit(text[i + 1]) || !char.IsAsciiHexDigit(text[i + 2]))
                {
                    return false;
                }

                bytes.Add(Convert.ToByte(text.Substring(i + 1, 2), 16));
                i += 3;
                continue;
            }

            var next = text.IndexOf('%', i);
            var end = next < 0 ? text.Length : next;
            bytes.AddRange(Encoding.UTF8.GetBytes(text.Substring(i, end - i)));
            i = end;
        }

        try
        {
            decoded = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString([.. bytes]);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryReadHostAndPort(string hostPort, out string host, out int port, out ProxyUrlError error)
    {
        host = string.Empty;
        port = 0;
        error = ProxyUrlError.None;
        string portText;
        if (hostPort.StartsWith('['))
        {
            var close = hostPort.IndexOf(']', StringComparison.Ordinal);
            if (close < 0)
            {
                error = ProxyUrlError.Unparseable;
                return false;
            }

            host = hostPort[1..close];
            var after = hostPort[(close + 1)..];
            if (after.Length != 0 && after[0] != ':')
            {
                error = ProxyUrlError.Unparseable;
                return false;
            }

            portText = after.Length == 0 ? string.Empty : after[1..];
        }
        else
        {
            var colon = hostPort.LastIndexOf(':');
            host = colon >= 0 ? hostPort[..colon] : hostPort;
            portText = colon >= 0 ? hostPort[(colon + 1)..] : string.Empty;
            if (host.Contains(':', StringComparison.Ordinal))
            {
                // An unbracketed IPv6 address is ambiguous with host:port; never guess.
                error = ProxyUrlError.Host;
                return false;
            }
        }

        if (!ProxyOptions.IsValidHost(host))
        {
            error = ProxyUrlError.Host;
            return false;
        }

        if (!TryReadPort(portText, out port))
        {
            error = ProxyUrlError.Port;
            return false;
        }

        return true;
    }

    private static bool TryReadPort(string text, out int port)
    {
        port = 0;
        if (text.Length is 0 or > 5)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }

            port = (port * 10) + (c - '0');
        }

        return port <= MaxPort;
    }
}
