// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// The Digest algorithms the SDK implements (RFC 7616, AUTH-15).
/// </summary>
/// <remarks>
/// The wire spellings are <c>MD5</c>, <c>MD5-sess</c>, <c>SHA-256</c> and <c>SHA-256-sess</c>; the challenge's spelling is
/// matched case-insensitively and the response always uses the full RFC spelling (AUTH-22). The values are explicit and
/// stable.
/// </remarks>
public enum DigestAlgorithm
{
    /// <summary><c>MD5</c>, the default when a challenge names no algorithm. Unavailable on a host whose crypto provider refuses MD5.</summary>
    Md5 = 0,

    /// <summary><c>MD5-sess</c>.</summary>
    Md5Sess = 1,

    /// <summary><c>SHA-256</c>.</summary>
    Sha256 = 2,

    /// <summary><c>SHA-256-sess</c>.</summary>
    Sha256Sess = 3,
}
