// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// A username and password for HTTP Digest authentication (RFC 7616).
/// </summary>
/// <remarks>
/// Both parts must be non-empty. No encoding check happens at construction: whether a password is representable depends
/// on the challenge's <c>charset</c>, so the Digest challenge handler decides (P6c-32). <see cref="ToString"/>
/// redacts the password.
/// </remarks>
public sealed class DigestCredential
{
    /// <summary>Initializes a <see cref="DigestCredential"/>.</summary>
    /// <param name="username">The user name. Non-empty.</param>
    /// <param name="password">The password. Non-empty.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An argument is empty.</exception>
    public DigestCredential(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        if (username.Length == 0)
        {
            throw new ArgumentException("The username must not be empty.", nameof(username));
        }

        if (password.Length == 0)
        {
            throw new ArgumentException("The password must not be empty.", nameof(password));
        }

        Username = username;
        Password = password;
    }

    /// <summary>The user name.</summary>
    public string Username { get; }

    /// <summary>The password.</summary>
    public string Password { get; }

    /// <summary>Renders the credential with the password redacted (AUTH-8).</summary>
    /// <returns>A description that never contains the password.</returns>
    public override string ToString() => $"DigestCredential {{ Username = {Username}, Password = *** }}";
}
