// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;

namespace Dexpace.Sdk.Conformance.Wire;

/// <summary>The deterministic multi-megabyte body of <see cref="LoopbackResponse.Large"/> and its SHA-256, so a test can compare digests without holding two copies.</summary>
internal static class LargeBody
{
    /// <summary>The <paramref name="bytes"/> pseudo-random octets of <c>new Random(seed)</c>.</summary>
    /// <param name="bytes">The length; positive.</param>
    /// <param name="seed">The generator seed.</param>
    internal static byte[] Generate(int bytes, int seed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
#pragma warning disable CA5394 // A reproducible, non-secret test payload: the seed is the point.
        var payload = new byte[bytes];
        new Random(seed).NextBytes(payload);
#pragma warning restore CA5394
        return payload;
    }

    /// <summary>The SHA-256 of <see cref="Generate"/>'s output for the same arguments.</summary>
    /// <param name="bytes">The length; positive.</param>
    /// <param name="seed">The generator seed.</param>
    internal static byte[] Sha256(int bytes, int seed) => SHA256.HashData(Generate(bytes, seed));
}
