// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// Whether this host's crypto provider will compute MD5 (AUTH-15, P6c-30; design §10 entry 16).
/// </summary>
/// <remarks>
/// A FIPS-enabled OpenSSL refuses MD5. <c>net10.0</c> exposes no <c>MD5.IsSupported</c> (verified), so the answer is found
/// by trying a hash once per process. When MD5 is unavailable the Digest handler drops the MD5 algorithms from its
/// effective preference, so an MD5-only challenge is declined and a server offering both falls through to SHA-256.
/// </remarks>
internal static class Md5Availability
{
    // MD5 is only tried, never relied on for security: the Digest protocol defines the response over it (RFC 7616).
#pragma warning disable CA5351
    private static readonly Lazy<bool> s_available = new(static () => Probe(static () => MD5.HashData(ReadOnlySpan<byte>.Empty)));
#pragma warning restore CA5351

    internal static bool IsAvailable => s_available.Value;

    // Any other exception is a defect and propagates.
    internal static bool Probe(Func<byte[]> hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        try
        {
            _ = hash();
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }
}
