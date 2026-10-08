// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// A username and password for HTTP Basic authentication (RFC 7617).
/// </summary>
/// <remarks>
/// <para>
/// Both parts must be non-empty; whitespace is allowed (AUTH-14). A <c>:</c> in the username is rejected because RFC 7617
/// §2 forbids it (P6c-17). <see cref="ToString"/> redacts the password.
/// </para>
/// <para>
/// <b>Breaking:</b> an empty username or password and a colon in the username are now rejected at construction (was:
/// only <see langword="null"/>).
/// </para>
/// </remarks>
public sealed class BasicCredential
{
    /// <summary>Initializes a <see cref="BasicCredential"/>.</summary>
    /// <param name="username">The user-id. Non-empty, without a colon.</param>
    /// <param name="password">The password. Non-empty.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An argument is empty, or the username contains a colon.</exception>
    public BasicCredential(string username, string password)
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

        if (username.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException("The username must not contain a colon (RFC 7617 section 2).", nameof(username));
        }

        Username = username;
        Password = password;
        HeaderValue = "Basic " + ToBase64();
    }

    /// <summary>The user-id.</summary>
    public string Username { get; }

    /// <summary>The password.</summary>
    public string Password { get; }

    /// <summary>The complete <c>Authorization</c> value, computed once and shared by the policy and the challenge handler.</summary>
    internal string HeaderValue { get; }

    /// <summary>Encodes <c>username:password</c> as UTF-8 Base64.</summary>
    /// <returns>The Base64 text.</returns>
    public string ToBase64()
    {
        var bytes = Encoding.UTF8.GetBytes($"{Username}:{Password}");
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Renders the credential with the password redacted (AUTH-8).</summary>
    /// <returns>A description that never contains the password.</returns>
    public override string ToString() => $"BasicCredential {{ Username = {Username}, Password = *** }}";
}
