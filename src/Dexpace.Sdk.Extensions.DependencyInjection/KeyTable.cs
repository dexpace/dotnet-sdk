// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Http.SystemNet;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

/// <summary>One key the reader accepts, and the member it feeds.</summary>
/// <param name="Key">The key relative to the client's section, <c>:</c>-separated and case-insensitive.</param>
/// <param name="Target">The type whose member the key feeds, or <see langword="null"/> for a handler-level key.</param>
/// <param name="Member">The property (or, for the credential classes, the constructor parameter) the key feeds.</param>
/// <param name="IsList">True when the key is a list (child keys or one comma-separated value).</param>
internal sealed record KeyEntry(string Key, Type? Target, string? Member, bool IsList = false);

/// <summary>
/// The keys the reader accepts (P9-4, P9-9): one table, shared by the unknown-key pass and the drift guard. A key that is not here
/// fails the boot naming it; a record member that is neither here nor on a code-only list fails a test (design C).
/// </summary>
internal static class KeyTable
{
    /// <summary>Every accepted key.</summary>
    internal static IReadOnlyList<KeyEntry> Entries { get; } =
    [
        new("BaseAddress", typeof(DexpaceClientOptions), nameof(DexpaceClientOptions.BaseAddress)),
        new("UserAgent", typeof(DexpaceClientOptions), nameof(DexpaceClientOptions.UserAgent)),
        new("OverallTimeout", typeof(DexpaceClientOptions), nameof(DexpaceClientOptions.OverallTimeout)),
        new("AttemptTimeout", typeof(DexpaceClientOptions), nameof(DexpaceClientOptions.AttemptTimeout)),
        new("Retry:MaxRetryAttempts", typeof(RetryOptions), nameof(RetryOptions.MaxRetryAttempts)),
        new("Retry:BaseDelay", typeof(RetryOptions), nameof(RetryOptions.BaseDelay)),
        new("Retry:MaxDelay", typeof(RetryOptions), nameof(RetryOptions.MaxDelay)),
        new("Retry:FixedDelay", typeof(RetryOptions), nameof(RetryOptions.FixedDelay)),
        new("Retry:Multiplier", typeof(RetryOptions), nameof(RetryOptions.Multiplier)),
        new("Retry:Jitter", typeof(RetryOptions), nameof(RetryOptions.Jitter)),
        new("Retry:HonorRetryAfter", typeof(RetryOptions), nameof(RetryOptions.HonorRetryAfter)),
        new("Retry:RetryableStatusCodes", typeof(RetryOptions), nameof(RetryOptions.RetryableStatusCodes), IsList: true),
        new("Retry:PacingHeaders", typeof(RetryOptions), nameof(RetryOptions.PacingHeaders), IsList: true),
        new("Retry:AttemptHeaderName", typeof(RetryOptions), nameof(RetryOptions.AttemptHeaderName)),
        new("Redirect:MaxRedirects", typeof(RedirectOptions), nameof(RedirectOptions.MaxRedirects)),
        new("Redirect:AllowHttpsToHttpDowngrade", typeof(RedirectOptions), nameof(RedirectOptions.AllowHttpsToHttpDowngrade)),
        new("Redirect:FollowSeeOther", typeof(RedirectOptions), nameof(RedirectOptions.FollowSeeOther)),
        new("Redirect:AllowedMethods", typeof(RedirectOptions), nameof(RedirectOptions.AllowedMethods), IsList: true),
        new("Logging:Level", typeof(HttpLoggingOptions), nameof(HttpLoggingOptions.Level)),
        new("Logging:BodyPreviewSize", typeof(HttpLoggingOptions), nameof(HttpLoggingOptions.BodyPreviewSize)),
        new("Logging:AllowedHeaderNames", typeof(HttpLoggingOptions), nameof(HttpLoggingOptions.AllowedHeaderNames), IsList: true),
        new("Logging:AllowedQueryParameters", typeof(HttpLoggingOptions), nameof(HttpLoggingOptions.AllowedQueryParameters), IsList: true),
        new("Logging:UrlValuedHeaderNames", typeof(HttpLoggingOptions), nameof(HttpLoggingOptions.UrlValuedHeaderNames), IsList: true),
        new("Logging:OmitDisallowedHeaders", typeof(HttpLoggingOptions), nameof(HttpLoggingOptions.OmitDisallowedHeaders)),
        new("Transport:Timeout", typeof(SystemNetHttpClientOptions), nameof(SystemNetHttpClientOptions.Timeout)),
        new("Transport:HeaderDropLogging", typeof(SystemNetHttpClientOptions), nameof(SystemNetHttpClientOptions.HeaderDropLogging)),
        new("Transport:ResendBufferLimit", typeof(SystemNetHttpClientOptions), nameof(SystemNetHttpClientOptions.ResendBufferLimit)),
        new("Transport:PooledConnectionLifetime", null, null),
        new("Transport:Proxy:Type", typeof(ProxyOptions), nameof(ProxyOptions.Type)),
        new("Transport:Proxy:Host", typeof(ProxyOptions), nameof(ProxyOptions.Host)),
        new("Transport:Proxy:Port", typeof(ProxyOptions), nameof(ProxyOptions.Port)),
        new("Transport:Proxy:NonProxyHosts", typeof(ProxyOptions), nameof(ProxyOptions.NonProxyHosts), IsList: true),
        new("Transport:Proxy:UserName", typeof(ProxyOptions), nameof(ProxyOptions.UserName)),
        new("Transport:Proxy:Password", typeof(ProxyOptions), nameof(ProxyOptions.Password)),
        new("Transport:Proxy:BypassAll", typeof(ProxyOptions), nameof(ProxyOptions.BypassAll)),
        new("Auth:ApiKey:Key", typeof(ApiKeyCredential), "key"),
        new("Auth:ApiKey:HeaderName", typeof(ApiKeyCredential), "header"),
        new("Auth:ApiKey:Scheme", typeof(ApiKeyCredential), "scheme"),
        new("Auth:Basic:Username", typeof(BasicCredential), "username"),
        new("Auth:Basic:Password", typeof(BasicCredential), "password"),
        new("Auth:Digest:Username", typeof(DigestCredential), "username"),
        new("Auth:Digest:Password", typeof(DigestCredential), "password"),
        new("Auth:TokenRefreshMargin", typeof(AuthCredentials), nameof(AuthCredentials.TokenRefreshMargin)),
    ];

    /// <summary>
    /// Members that are code-only: configuration can never feed them (a delegate, a logger, a clock, a credentials hook, a container service),
    /// so the drift guard does not ask for a key.
    /// </summary>
    internal static IReadOnlyList<(Type Type, string Member)> CodeOnly { get; } =
    [
        (typeof(RedirectOptions), nameof(RedirectOptions.Predicate)),
        (typeof(SystemNetHttpClientOptions), nameof(SystemNetHttpClientOptions.Logger)),
        (typeof(SystemNetHttpClientOptions), nameof(SystemNetHttpClientOptions.TimeProvider)),
        (typeof(ProxyOptions), nameof(ProxyOptions.ChallengeCredentials)),
        (typeof(AuthCredentials), nameof(AuthCredentials.Token)),
    ];

    /// <summary>Members that hold a nested record or credential whose own members the keys cover (their group of keys is the member).</summary>
    internal static IReadOnlyList<(Type Type, string Member)> Groups { get; } =
    [
        (typeof(DexpaceClientOptions), nameof(DexpaceClientOptions.Retry)),
        (typeof(DexpaceClientOptions), nameof(DexpaceClientOptions.Redirect)),
        (typeof(DexpaceClientOptions), nameof(DexpaceClientOptions.Logging)),
        (typeof(SystemNetHttpClientOptions), nameof(SystemNetHttpClientOptions.Proxy)),
        (typeof(AuthCredentials), nameof(AuthCredentials.ApiKey)),
        (typeof(AuthCredentials), nameof(AuthCredentials.Basic)),
        (typeof(AuthCredentials), nameof(AuthCredentials.Digest)),
    ];

    /// <summary>The keys whose configuration is read by the primary handler, not by a record (the handler-level category).</summary>
    internal static IReadOnlyList<string> HandlerLevel { get; } = ["Transport:PooledConnectionLifetime"];

    /// <summary>The prefixes of the keys, for the unknown-key pass: a child that leads to a key is not unknown.</summary>
    internal static bool IsKey(string fullKey, out bool isList)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Key, fullKey, StringComparison.OrdinalIgnoreCase))
            {
                isList = entry.IsList;
                return true;
            }
        }

        isList = false;
        return false;
    }

    /// <summary>True when some key lies below <paramref name="prefix"/> (a group such as <c>Retry</c> or <c>Transport:Proxy</c>).</summary>
    internal static bool IsGroup(string prefix) =>
        Entries.Any(entry => entry.Key.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase));
}
