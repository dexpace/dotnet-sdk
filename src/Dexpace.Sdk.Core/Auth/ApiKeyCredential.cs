// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// An API key sent in a request header, optionally behind a scheme prefix (<c>SharedAccessKey &lt;key&gt;</c>, AUTH-26).
/// </summary>
/// <remarks>
/// <para>
/// The stamped value (the key, or <c>"&lt;scheme&gt; &lt;key&gt;"</c>) is computed once and checked against the outbound
/// header grammar at construction (XCUT-18). There is no separate named-key credential in this port: a prefixed key is
/// <c>new ApiKeyCredential(key, scheme: "SharedAccessKey")</c> (P6c-19). <see cref="ToString"/> redacts the key.
/// </para>
/// <para>
/// <b>Breaking:</b> a whitespace-only key, a blank scheme or a scheme containing whitespace, and a key or scheme the
/// outbound header grammar refuses, are now rejected at construction (was: only an empty key, with the rest failing per
/// request or never).
/// </para>
/// </remarks>
public sealed class ApiKeyCredential
{
    /// <summary>Initializes an <see cref="ApiKeyCredential"/>.</summary>
    /// <param name="key">The API key. Must not be null, empty or whitespace.</param>
    /// <param name="header">The header to send the key in. Defaults to <c>Authorization</c>.</param>
    /// <param name="scheme">An optional prefix; non-blank and free of whitespace when set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> is blank, <paramref name="scheme"/> is blank or contains whitespace, or the resulting header
    /// value is not valid outbound header text. The message never contains the key.
    /// </exception>
    public ApiKeyCredential(string key, HttpHeaderName? header = null, string? scheme = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (scheme is not null && (string.IsNullOrWhiteSpace(scheme) || scheme.Any(char.IsWhiteSpace)))
        {
            throw new ArgumentException("The scheme must be non-blank and contain no whitespace.", nameof(scheme));
        }

        Key = key;
        HeaderName = header ?? HttpHeaderName.WellKnown.Authorization;
        Scheme = scheme;
        HeaderValue = scheme is null ? key : scheme + " " + key;
        HeaderSyntax.ValidateOutboundValue(HeaderValue, HeaderName.Original, nameof(key));
    }

    /// <summary>The API key.</summary>
    public string Key { get; }

    /// <summary>The header the key is sent in.</summary>
    public HttpHeaderName HeaderName { get; }

    /// <summary>The optional scheme prefix, or <see langword="null"/> when the key is sent bare.</summary>
    public string? Scheme { get; }

    /// <summary>The value stamped on the wire, computed once.</summary>
    internal string HeaderValue { get; }

    /// <summary>Renders the credential with the key redacted (AUTH-8).</summary>
    /// <returns>A description that never contains the key.</returns>
    public override string ToString() => $"ApiKeyCredential {{ Key = ***, HeaderName = {HeaderName.Original}, Scheme = {Scheme} }}";
}
