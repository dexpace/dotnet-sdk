// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Extensions.DependencyInjection;

internal static partial class ConfigurationReader
{
    private const string AuthRequired = "required when any key of this credential is present";

    // P9-18: Auth:* feeds the redacting credentials. No rule here quotes a configured value, so every credential failure carries
    // fixed text and never an accessor's message (a key or password could appear in one). AuthCredentials.Token is code-only.
    private static AuthCredentials? ReadAuth(ConfigSection section)
    {
        var apiKey = ReadApiKey(section, out var apiKeyPresent);
        var basic = ReadUsernamePassword(section, "Auth:Basic:Username", "Auth:Basic:Password", static (u, p) => new BasicCredential(u, p), colonForbidden: true, out var basicPresent);
        var digest = ReadUsernamePassword(section, "Auth:Digest:Username", "Auth:Digest:Password", static (u, p) => new DigestCredential(u, p), colonForbidden: false, out var digestPresent);
        var marginPresent = section.Has("Auth:TokenRefreshMargin");
        if (!(apiKeyPresent || basicPresent || digestPresent || marginPresent))
        {
            return null;
        }

        var binder = new RecordBinder<AuthCredentials>(
            section,
            new AuthCredentials { ApiKey = apiKey, Basic = basic as BasicCredential, Digest = digest as DigestCredential });
        binder.Duration("Auth:TokenRefreshMargin", static (r, v) => r with { TokenRefreshMargin = v });
        return binder.Value;
    }

    private static ApiKeyCredential? ReadApiKey(ConfigSection section, out bool present)
    {
        var key = section.Text("Auth:ApiKey:Key");
        var header = section.Text("Auth:ApiKey:HeaderName");
        var scheme = section.Text("Auth:ApiKey:Scheme");
        present = key is not null || header is not null || scheme is not null;
        if (!present)
        {
            return null;
        }

        var ok = true;
        HttpHeaderName? name = null;
        if (key is null)
        {
            section.Fail("Auth:ApiKey:Key", AuthRequired);
            ok = false;
        }

        if (header is not null && !TryHeaderName(header, out name))
        {
            section.Fail("Auth:ApiKey:HeaderName", "not a valid HTTP header name");
            ok = false;
        }

        if (scheme is not null && scheme.Any(char.IsWhiteSpace))
        {
            section.Fail("Auth:ApiKey:Scheme", "must not contain whitespace");
            ok = false;
        }

        return ok ? Build(section, "Auth:ApiKey:Key", () => new ApiKeyCredential(key!, name, scheme)) : null;
    }

    private static object? ReadUsernamePassword(
        ConfigSection section,
        string usernameKey,
        string passwordKey,
        Func<string, string, object> create,
        bool colonForbidden,
        out bool present)
    {
        var username = section.Text(usernameKey);
        var password = section.Text(passwordKey);
        present = username is not null || password is not null;
        if (!present)
        {
            return null;
        }

        if (username is null || password is null)
        {
            section.Fail(username is null ? usernameKey : passwordKey, AuthRequired);
            return null;
        }

        if (colonForbidden && username.Contains(':', StringComparison.Ordinal))
        {
            section.Fail(usernameKey, "must not contain a colon");
            return null;
        }

        return Build(section, passwordKey, () => create(username, password));
    }

    private static T? Build<T>(ConfigSection section, string key, Func<T> create)
        where T : class
    {
        try
        {
            return create();
        }
        catch (ArgumentException)
        {
            section.Fail(key, "is not a valid credential value");
            return null;
        }
    }

    private static bool TryHeaderName(string text, out HttpHeaderName? name)
    {
        try
        {
            name = HttpHeaderName.Of(text);
            return true;
        }
        catch (ArgumentException)
        {
            name = null;
            return false;
        }
    }
}
