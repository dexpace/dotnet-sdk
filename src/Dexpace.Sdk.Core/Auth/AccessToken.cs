// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// An access token returned by a <see cref="TokenCredential"/>, together with its optional expiry and an optional
/// proactive-refresh hint.
/// </summary>
/// <remarks>
/// <para>
/// Equality is by value over the token, the expiry and the refresh hint (AUTH-8, P6c-21). <see cref="ToString"/>
/// redacts the token.
/// </para>
/// <para>
/// <b>Breaking:</b> <see cref="ExpiresOn"/> was a non-nullable <see cref="DateTimeOffset"/> (a token may now never
/// expire); the token must now be non-blank (was: non-null); the struct had no equality and <see cref="ToString"/>
/// printed the type name.
/// </para>
/// </remarks>
public readonly struct AccessToken : IEquatable<AccessToken>
{
    /// <summary>Initializes a token that never expires.</summary>
    /// <param name="token">The raw token string.</param>
    /// <exception cref="ArgumentException"><paramref name="token"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public AccessToken(string token)
        : this(token, null, null)
    {
    }

    /// <summary>Initializes a token with an optional expiry and no proactive-refresh hint.</summary>
    /// <param name="token">The raw token string.</param>
    /// <param name="expiresOn">The time at which this token becomes invalid, or <see langword="null"/> if it never does.</param>
    /// <exception cref="ArgumentException"><paramref name="token"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public AccessToken(string token, DateTimeOffset? expiresOn)
        : this(token, expiresOn, null)
    {
    }

    /// <summary>Initializes a token with an optional expiry and an optional proactive-refresh hint.</summary>
    /// <param name="token">The raw token string.</param>
    /// <param name="expiresOn">The time at which this token becomes invalid, or <see langword="null"/> if it never does.</param>
    /// <param name="refreshOn">
    /// An optional hint: when the clock reaches this value the token cache should proactively refresh, even though
    /// <paramref name="expiresOn"/> has not been reached.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="token"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
    public AccessToken(string token, DateTimeOffset? expiresOn, DateTimeOffset? refreshOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Token = token;
        ExpiresOn = expiresOn;
        RefreshOn = refreshOn;
    }

    /// <summary>The raw token value. <see langword="null"/> only for <c>default(AccessToken)</c>.</summary>
    public string Token { get; }

    /// <summary>The time at which this token becomes invalid, or <see langword="null"/> if it never expires (AUTH-10).</summary>
    public DateTimeOffset? ExpiresOn { get; }

    /// <summary>
    /// An optional proactive-refresh hint. When non-<see langword="null"/>, the token cache begins refreshing once the
    /// clock passes this value, even though <see cref="ExpiresOn"/> has not yet been reached.
    /// </summary>
    public DateTimeOffset? RefreshOn { get; }

    /// <summary>Compares two tokens by value.</summary>
    /// <param name="left">The first token.</param>
    /// <param name="right">The second token.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    public static bool operator ==(AccessToken left, AccessToken right) => left.Equals(right);

    /// <summary>Compares two tokens by value.</summary>
    /// <param name="left">The first token.</param>
    /// <param name="right">The second token.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(AccessToken left, AccessToken right) => !left.Equals(right);

    /// <summary>Whether the token is expired at <paramref name="now"/> once <paramref name="margin"/> is allowed for (AUTH-10).</summary>
    /// <param name="now">The current time.</param>
    /// <param name="margin">How early to treat the token as expired.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="now"/> plus <paramref name="margin"/> is strictly after
    /// <see cref="ExpiresOn"/>; a token without an expiry is never expired.
    /// </returns>
    public bool IsExpired(DateTimeOffset now, TimeSpan margin) => ExpiresOn is { } expiresOn && now + margin > expiresOn;

    /// <inheritdoc/>
    public bool Equals(AccessToken other) =>
        string.Equals(Token, other.Token, StringComparison.Ordinal) && ExpiresOn == other.ExpiresOn && RefreshOn == other.RefreshOn;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AccessToken other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Token, ExpiresOn, RefreshOn);

    /// <summary>Renders the token with its value redacted (AUTH-8).</summary>
    /// <returns>A description that never contains the token.</returns>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"AccessToken {{ Token = ***, ExpiresOn = {ExpiresOn?.ToString("O", CultureInfo.InvariantCulture)}, RefreshOn = {RefreshOn?.ToString("O", CultureInfo.InvariantCulture)} }}");
}
