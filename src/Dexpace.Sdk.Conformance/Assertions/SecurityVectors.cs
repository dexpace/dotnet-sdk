// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The byte table of chapter 19 (XCUT-18) and the redaction vectors of XCUT-19, ported from nodejs-sdk@54aeed4
// tests/conformance/xcut/security-by-default.conformance.test.ts. The secrets are long, distinctive tokens, so a hit in a rendered
// line is never an accident.
using System.Globalization;

namespace Dexpace.Sdk.Conformance;

/// <summary>The vectors <c>xcut-18</c> and <c>xcut-19</c> judge: control and non-ASCII characters by role, and URLs and headers carrying secrets.</summary>
internal static class SecurityVectors
{
    /// <summary>The userinfo of the redaction URL: a user name and a password.</summary>
    internal const string UserInfo = "svc-user-9f3a:p4ss-9f3a";

    /// <summary>A secret query-parameter value.</summary>
    internal const string QuerySecret = "QUERYSECRET-71c2";

    /// <summary>A secret in a key=value fragment token.</summary>
    internal const string FragmentSecret = "FRAGSECRET-5d0e";

    /// <summary>A secret in an <c>Authorization</c> header.</summary>
    internal const string AuthorizationSecret = "AUTHSECRET-b83a";

    /// <summary>A secret in a <c>Cookie</c> header.</summary>
    internal const string CookieSecret = "COOKIESECRET-2e47";

    /// <summary>A secret in a <c>Set-Cookie</c> header.</summary>
    internal const string SetCookieSecret = "SETCOOKIESECRET-c19b";

    /// <summary>A value of a header outside the allow-list.</summary>
    internal const string CustomHeaderSecret = "CUSTOMSECRET-0aa7";

    /// <summary>A secret in a response body.</summary>
    internal const string BodySecret = "BODYSECRET-3c5f";

    /// <summary>The URL <c>xcut-19</c> redacts: userinfo, a secret query value, an allow-listed one and a fragment token.</summary>
    internal static string SecretUrl { get; } = $"https://{UserInfo}@example.test/path?token={QuerySecret}&api-version=2026-01&sig={QuerySecret}#access_token={FragmentSecret}&state=ok";

    /// <summary>A <c>Location</c>-style header value carrying userinfo and a secret query value.</summary>
    internal static string SecretLocation { get; } = $"https://{UserInfo}@example.test/callback?code={QuerySecret}";

    /// <summary>The substrings that must not survive redaction of <see cref="SecretUrl"/> or <see cref="SecretLocation"/>.</summary>
    internal static IReadOnlyList<string> UrlSecrets { get; } = ["svc-user-9f3a", "p4ss-9f3a", QuerySecret, FragmentSecret];

    /// <summary>The characters a header name must reject: every C0 control (HTAB included), DEL, and non-ASCII.</summary>
    internal static IReadOnlyList<char> NameRejects { get; } = [.. ControlsAndDel(includeTab: true), '\u0080', 'é', '中'];

    /// <summary>The characters an outbound header value must reject: the same set except HTAB.</summary>
    internal static IReadOnlyList<char> OutboundValueRejects { get; } = [.. ControlsAndDel(includeTab: false), '\u0080', 'é', '中'];

    /// <summary>The characters an inbound header value must reject: control bytes other than HTAB (obs-text is permitted).</summary>
    internal static IReadOnlyList<char> InboundValueRejects { get; } = [.. ControlsAndDel(includeTab: false)];

    /// <summary>Names the character for a failure message: <c>U+0009</c>.</summary>
    /// <param name="c">The character.</param>
    internal static string Describe(char c) => string.Create(CultureInfo.InvariantCulture, $"U+{(int)c:X4}");

    private static IEnumerable<char> ControlsAndDel(bool includeTab)
    {
        for (var c = 0; c <= 0x1F; c++)
        {
            if (includeTab || c != 0x09)
            {
                yield return (char)c;
            }
        }

        yield return '\u007f';
    }
}
