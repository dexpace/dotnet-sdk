// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// The authentication schemes a call can require (AUTH-1).
/// </summary>
/// <remarks>
/// The values are explicit and stable. <see cref="NoAuth"/> is never written to the wire, and <c>0</c> is deliberately
/// not the permissive value: <c>default(AuthScheme)</c> is <see cref="OAuth2"/>, so an uninitialised scheme can never
/// silently mean "anonymous" (P6c-3).
/// </remarks>
public enum AuthScheme
{
    /// <summary>OAuth 2.0 / bearer token authentication.</summary>
    OAuth2 = 0,

    /// <summary>An API key sent in a header.</summary>
    ApiKey = 1,

    /// <summary>HTTP Basic authentication (RFC 7617).</summary>
    Basic = 2,

    /// <summary>HTTP Digest authentication (RFC 7616).</summary>
    Digest = 3,

    /// <summary>No authentication: the call is sent anonymously.</summary>
    NoAuth = 4,
}
