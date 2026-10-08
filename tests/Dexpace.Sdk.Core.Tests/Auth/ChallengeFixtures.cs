// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>
/// Reusable WWW-Authenticate header values (the Ruby port's eight strings): RFC 2617 section 3.5's Basic and MD5
/// challenges, RFC 7616 section 3.9.1's SHA-256-sess challenge, one Digest challenge a handler must decline, and one
/// deliberately malformed string per AUTH-13 recovery clause.
/// </summary>
internal static class ChallengeFixtures
{
    internal const string Basic = "Basic realm=\"example\"";

    internal const string DigestMd5 =
        "Digest realm=\"testrealm@host.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", " +
        "opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"";

    internal const string DigestSha256Sess =
        "Digest realm=\"http-auth@example.org\", qop=\"auth\", algorithm=SHA-256-sess, " +
        "nonce=\"7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v\", " +
        "opaque=\"FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS\", charset=UTF-8, userhash=false";

    internal const string DigestUnsupportedQop = "Digest realm=\"r\", qop=\"auth-int\", nonce=\"n\"";

    internal const string MalformedUnterminatedQuote = "Digest realm=\"unterminated";

    internal const string MalformedStrayComma = "Digest realm=r,,nonce=n";

    internal const string MalformedValue = "Digest realm=@@, nonce=\"n\"";

    internal const string BareToken68 = "Bearer dGhlIHNlY3JldCB0b2tlbg==";
}
