// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;
using Dexpace.Sdk.Core.Auth;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>AUTH-15, P6c-30: the MD5 probe and the algorithm enum.</summary>
[Trait("Category", "Unit")]
public sealed class Md5AvailabilityTests
{
    [Fact]
    public void Probe_succeeds_when_the_hash_succeeds()
    {
        Assert.True(Md5Availability.Probe(() => [1]));
    }

    [Fact]
    public void Probe_is_false_on_CryptographicException_and_PlatformNotSupportedException()
    {
        Assert.False(Md5Availability.Probe(() => throw new CryptographicException("FIPS")));
        Assert.False(Md5Availability.Probe(() => throw new PlatformNotSupportedException()));
    }

    [Fact]
    public void Probe_does_not_swallow_other_exceptions()
    {
        Assert.Throws<InvalidOperationException>(() => Md5Availability.Probe(() => throw new InvalidOperationException("defect")));
    }

    [Fact]
    public void IsAvailable_is_computed_once_and_true_on_this_host()
    {
        Assert.Equal(Md5Availability.IsAvailable, Md5Availability.IsAvailable);
        Assert.True(Md5Availability.IsAvailable);
    }
}

/// <summary>AUTH-15: the algorithm enum and its wire names.</summary>
[Trait("Category", "Unit")]
public sealed class DigestAlgorithmTests
{
    [Fact]
    public void Values_are_explicit()
    {
        Assert.Equal(0, (int)DigestAlgorithm.Md5);
        Assert.Equal(1, (int)DigestAlgorithm.Md5Sess);
        Assert.Equal(2, (int)DigestAlgorithm.Sha256);
        Assert.Equal(3, (int)DigestAlgorithm.Sha256Sess);
        Assert.Equal(4, Enum.GetValues<DigestAlgorithm>().Length);
    }

    [Fact]
    public void Wire_names_are_MD5_MD5_sess_SHA_256_SHA_256_sess()
    {
        Assert.Equal("MD5", DigestAlgorithmNames.Wire(DigestAlgorithm.Md5));
        Assert.Equal("MD5-sess", DigestAlgorithmNames.Wire(DigestAlgorithm.Md5Sess));
        Assert.Equal("SHA-256", DigestAlgorithmNames.Wire(DigestAlgorithm.Sha256));
        Assert.Equal("SHA-256-sess", DigestAlgorithmNames.Wire(DigestAlgorithm.Sha256Sess));
    }

    [Fact]
    public void Parsing_is_case_insensitive_and_an_absent_token_means_MD5()
    {
        Assert.True(DigestAlgorithmNames.TryParse("sha-256-SESS", out var sess));
        Assert.Equal(DigestAlgorithm.Sha256Sess, sess);
        Assert.True(DigestAlgorithmNames.TryParse(null, out var none));
        Assert.Equal(DigestAlgorithm.Md5, none);
        Assert.False(DigestAlgorithmNames.TryParse("SHA-512-256", out _));
    }
}
