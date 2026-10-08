// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Auth;

/// <summary>The wire spellings of <see cref="DigestAlgorithm"/>.</summary>
internal static class DigestAlgorithmNames
{
    internal static string Wire(DigestAlgorithm algorithm) => algorithm switch
    {
        DigestAlgorithm.Md5 => "MD5",
        DigestAlgorithm.Md5Sess => "MD5-sess",
        DigestAlgorithm.Sha256 => "SHA-256",
        DigestAlgorithm.Sha256Sess => "SHA-256-sess",
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Not a supported Digest algorithm."),
    };

    // An absent algorithm means MD5 (AUTH-16); the token is matched case-insensitively with an ordinal compare.
    internal static bool TryParse(string? token, out DigestAlgorithm algorithm)
    {
        if (token is null)
        {
            algorithm = DigestAlgorithm.Md5;
            return true;
        }

        foreach (var candidate in Enum.GetValues<DigestAlgorithm>())
        {
            if (string.Equals(token, Wire(candidate), StringComparison.OrdinalIgnoreCase))
            {
                algorithm = candidate;
                return true;
            }
        }

        algorithm = default;
        return false;
    }

    internal static bool IsSession(DigestAlgorithm algorithm) => algorithm is DigestAlgorithm.Md5Sess or DigestAlgorithm.Sha256Sess;

    internal static bool IsMd5(DigestAlgorithm algorithm) => algorithm is DigestAlgorithm.Md5 or DigestAlgorithm.Md5Sess;
}
