// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Auth;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>AUTH-17, AUTH-20 to AUTH-22: the pure Digest arithmetic and text forms.</summary>
[Trait("Category", "Unit")]
public sealed class DigestComputationTests
{
    private static byte[] B(string text) => Encoding.UTF8.GetBytes(text);

    private static string Mufasa(DigestAlgorithm algorithm, string? qop = "auth") =>
        DigestComputation.Compute(
            algorithm,
            B("Mufasa"),
            B("testrealm@host.com"),
            B("Circle Of Life"),
            "dcd98b7102dd2f0e8b11d0f600bfb0c093",
            "GET",
            "/dir/index.html",
            qop,
            "00000001",
            "0a4f113b");

    [Fact]
    public void The_RFC_2617_vectors_reproduce()
    {
        Assert.Equal("6629fae49393a05397450978507c4ef1", Mufasa(DigestAlgorithm.Md5));
        Assert.Equal("670fd8c2df070c60b045671b8b24ff02", Mufasa(DigestAlgorithm.Md5, qop: null));
    }

    [Fact]
    public void The_RFC_7616_inputs_give_the_re_derived_SHA256_values()
    {
        string Jason(DigestAlgorithm algorithm) => DigestComputation.Compute(
            algorithm,
            B("Jäsøn Doe"),
            B("http-auth@example.org"),
            B("Secret, or not?"),
            "7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v",
            "GET",
            "/doe.json",
            "auth",
            "00000001",
            "f2/wE4q74E6zIJEtWaHKaf5wv/H5QzzpXusqGemxURZJ");

        Assert.Equal("9fbf3e2223549127935ba79d47a0299af1f57eae1240ead830c0b47ad60346e1", Jason(DigestAlgorithm.Sha256));
        Assert.Equal("a0316f893cdcbd706441a5392ef9e690688b447acf4015a2b9ce520e6b551a5c", Jason(DigestAlgorithm.Sha256Sess));
    }

    [Fact]
    public void HA1_sess_folds_nonce_and_cnonce()
    {
        var plain = DigestComputation.ComputeHa1(md5: true, session: false, B("u"), B("r"), B("p"), "n", "c");
        var sess = DigestComputation.ComputeHa1(md5: true, session: true, B("u"), B("r"), B("p"), "n", "c");

        Assert.Equal(DigestComputation.HashHex(true, B("u:r:p")), plain);
        Assert.Equal(DigestComputation.HashHex(true, B($"{plain}:n:c")), sess);
    }

    [Fact]
    public void Hex_is_lower_case()
    {
        var response = Mufasa(DigestAlgorithm.Md5);

        Assert.Equal(response.ToLowerInvariant(), response);
        Assert.Equal(32, response.Length);
        Assert.Equal(64, Mufasa(DigestAlgorithm.Sha256).Length);
        Assert.Equal("00000a0b", DigestComputation.Nc(0xA0B));
    }

    [Fact]
    public void Encoding_is_UTF8_under_charset_UTF8_else_Latin1()
    {
        Assert.True(DigestComputation.TryEncode("café", utf8: true, out var utf8));
        Assert.True(DigestComputation.TryEncode("café", utf8: false, out var latin1));

        Assert.Equal([0x63, 0x61, 0x66, 0xC3, 0xA9], utf8);
        Assert.Equal([0x63, 0x61, 0x66, 0xE9], latin1);
    }

    [Fact]
    public void A_Latin1_unrepresentable_credential_is_reported_as_not_encodable_not_hashed_with_question_marks()
    {
        Assert.False(DigestComputation.TryEncode("é€", utf8: false, out _));
        Assert.True(DigestComputation.TryEncode("é€", utf8: true, out _));
        Assert.False(DigestComputation.TryEncode("\ud800", utf8: true, out _));
    }

    [Fact]
    public void Rfc8187_encoding_of_a_username()
    {
        Assert.Equal("UTF-8''J%C3%A4s%C3%B8n%20Doe", DigestComputation.Rfc8187Encode(B("Jäsøn Doe"), utf8: true));
        Assert.Equal("ISO-8859-1''Jos%E9", DigestComputation.Rfc8187Encode([0x4A, 0x6F, 0x73, 0xE9], utf8: false));
        Assert.Equal("UTF-8''a-b.c_d~e%27f%2A", DigestComputation.Rfc8187Encode(B("a-b.c_d~e'f*"), utf8: true));
    }

    [Fact]
    public void Quoted_string_escaping()
    {
        Assert.Equal("plain", DigestComputation.Quote("plain"));
        Assert.Equal("a\\\"b\\\\c", DigestComputation.Quote("a\"b\\c"));
    }

    [Theory]
    [InlineData("https://h/a%2Fb?x=%26y", "/a%2Fb?x=%26y")]
    [InlineData("https://h", "/")]
    [InlineData("https://h/", "/")]
    [InlineData("https://h?q=1", "/?q=1")]
    [InlineData("https://h/p%20q?a=b%20c", "/p%20q?a=b%20c")]
    public void Digest_uri_is_the_escaped_PathAndQuery_or_slash(string url, string expected)
    {
        Assert.Equal(expected, DigestComputation.DigestUri(new Uri(url)));
    }

    [Fact]
    public void The_cnonce_source_is_RandomNumberGenerator()
    {
        var source = File.ReadAllText(Path.Combine(SourceRoot(), "Auth", "DigestChallengeHandler.cs"));

        Assert.Contains("RandomNumberGenerator.GetHexString(32, lowercase: true)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Random.Shared", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Random", source, StringComparison.Ordinal);
    }

    private static string SourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Dexpace.Sdk.Core");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The src/Dexpace.Sdk.Core tree was not found above the test output.");
    }
}
