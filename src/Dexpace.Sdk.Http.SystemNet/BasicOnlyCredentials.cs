// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Configuration;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// The credentials a <see cref="ProxyOptions"/> pair becomes when it carries no <see cref="ProxyOptions.ChallengeCredentials"/>
/// (<c>TRANSPORT-30</c>: proxy authentication falls back to Basic from the user name and password). It answers the one
/// authentication type the pair can honestly satisfy and returns <see langword="null"/> for every other, so a proxy that
/// challenges with Digest, NTLM or Negotiate gets no credential it did not ask for.
/// </summary>
/// <remarks>
/// <para>
/// For an HTTP proxy the runtime asks under <c>Basic</c> after a <c>407</c>. For a SOCKS proxy there is no challenge: the runtime
/// asks once per connection, passing the proxy URI's scheme as the authentication type (<c>socks5</c>, <c>socks4</c>; measured on
/// .NET 10.0.401, phase 8b task 0.1), and uses the answer for the handshake. SOCKS4 carries a user id only, so its answer has an
/// empty password.
/// </para>
/// <para>
/// Every answer is a new <see cref="NetworkCredential"/>: it is mutable, and the runtime must not be able to change the pair the
/// next answer is built from. This type is never a server credential: the transport's handler leaves its own
/// <c>Credentials</c> unset, so the pair reaches a proxy and never an origin (<c>TRANSPORT-30</c>'s MUST).
/// </para>
/// </remarks>
internal sealed class BasicOnlyCredentials : ICredentials
{
    private readonly string _authenticationType;
    private readonly string _userName;
    private readonly string _password;

    internal BasicOnlyCredentials(ProxyType type, string userName, string? password)
    {
        ArgumentNullException.ThrowIfNull(userName);
        (_authenticationType, _password) = type switch
        {
            ProxyType.Socks5 => ("socks5", password ?? string.Empty),
            ProxyType.Socks4 => ("socks4", string.Empty),
            _ => ("Basic", password ?? string.Empty),
        };
        _userName = userName;
    }

    public NetworkCredential? GetCredential(Uri uri, string authType) =>
        string.Equals(authType, _authenticationType, StringComparison.OrdinalIgnoreCase)
            ? new NetworkCredential(_userName, _password)
            : null;
}
