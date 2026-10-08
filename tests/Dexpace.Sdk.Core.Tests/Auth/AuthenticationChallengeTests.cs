// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>AUTH-12 and AUTH-13: the challenge model and its lenient, linear parser.</summary>
[Trait("Category", "Unit")]
public sealed class AuthenticationChallengeTests
{
    public static TheoryData<string> VectorNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var vector in VectorFile.Load<ChallengeCase>("auth/challenges.json"))
            {
                data.Add(vector.Name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(VectorNames))]
    public void Parse_matches_every_vector(string name)
    {
        var vector = VectorFile.Load<ChallengeCase>("auth/challenges.json").Single(v => v.Name == name);

        AssertMatches(vector, AuthenticationChallenge.Parse(vector.Input));
        AssertMatches(vector, AuthenticationChallenge.Parse((vector.Input ?? string.Empty).AsSpan()));
    }

    [Fact]
    public void Every_vector_has_a_source()
    {
        Assert.All(VectorFile.Load<ChallengeCase>("auth/challenges.json"), v => Assert.False(string.IsNullOrWhiteSpace(v.Source), v.Name));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void Scheme_and_parameter_names_are_lower_cased_with_ASCII_folding_only(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var challenge = AuthenticationChallenge.Parse("BEARER REALM=\"X\"").Single();
            Assert.Equal("bearer", challenge.Scheme);
            Assert.Equal("X", challenge.Parameters["realm"]);

            var dotted = AuthenticationChallenge.Parse("Bearer \u0130x=\"v\"");
            Assert.DoesNotContain(dotted, c => c.Parameters.ContainsKey("i\u0307x"));
            Assert.Equal("Z", new AuthenticationChallenge("B", new Dictionary<string, string> { ["\u0130Z"] = "Z" }).Parameters["\u0130z"]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Values_are_never_folded()
    {
        var challenge = AuthenticationChallenge.Parse("Digest realm=\"MiXeD\", algorithm=SHA-256").Single();

        Assert.Equal("MiXeD", challenge.Parameters["realm"]);
        Assert.Equal("SHA-256", challenge.Parameters["algorithm"]);
    }

    [Fact]
    public void The_constructor_folds_parameter_names_and_copies_them()
    {
        var source = new Dictionary<string, string> { ["Realm"] = "x" };
        var challenge = new AuthenticationChallenge("Bearer", source);
        source["nonce"] = "n";

        Assert.Equal("bearer", challenge.Scheme);
        Assert.Equal("x", challenge.Parameters["realm"]);
        Assert.Single(challenge.Parameters);
        Assert.Throws<ArgumentException>(() => new AuthenticationChallenge(" "));
        Assert.Throws<ArgumentNullException>(() => new AuthenticationChallenge(null!));
        Assert.Empty(new AuthenticationChallenge("basic").Parameters);
    }

    [Fact]
    public void The_constructor_keeps_the_first_of_two_names_that_fold_equal()
    {
        var challenge = new AuthenticationChallenge(
            "digest",
            new List<KeyValuePair<string, string>> { new("Realm", "first"), new("REALM", "second") }.ToDictionary(p => p.Key, p => p.Value));

        Assert.Single(challenge.Parameters);
    }

    [Fact]
    public void Token68_is_Parameters_token68()
    {
        Assert.Equal("token68", AuthenticationChallenge.Token68Key);
        var challenge = AuthenticationChallenge.Parse(ChallengeFixtures.BareToken68).Single();
        Assert.Equal("dGhlIHNlY3JldCB0b2tlbg==", challenge.Token68);
        Assert.Equal(challenge.Token68, challenge.Parameters[AuthenticationChallenge.Token68Key]);
        Assert.Null(AuthenticationChallenge.Parse(ChallengeFixtures.Basic).Single().Token68);
    }

    [Fact]
    public void Parse_never_throws_for_arbitrary_text()
    {
        const string Alphabet = "Bearer Basic realm=\"\\,=;\t é\u0001\u007f@/-.";
        var random = new Random(6);
        var buffer = new char[40];
        for (var i = 0; i < 5000; i++)
        {
            var length = random.Next(buffer.Length);
            for (var j = 0; j < length; j++)
            {
                buffer[j] = Alphabet[random.Next(Alphabet.Length)];
            }

            var challenges = AuthenticationChallenge.Parse(new string(buffer, 0, length));

            Assert.All(challenges, c =>
            {
                Assert.NotEmpty(c.Scheme);
                Assert.Equal(c.Scheme.ToLowerInvariant(), c.Scheme);
                Assert.All(c.Parameters.Keys, k => Assert.NotEmpty(k));
            });
        }
    }

    [Fact]
    public void Parse_never_throws_over_the_metacharacter_alphabet()
    {
        char[] alphabet = [' ', ',', '=', '"', '\\', '/', '!', '@', 'a', '\t'];
        for (var seed = 0; seed < 100_000; seed++)
        {
            var chars = new char[5];
            var n = seed;
            for (var i = 0; i < 5; i++)
            {
                chars[i] = alphabet[n % alphabet.Length];
                n /= alphabet.Length;
            }

            _ = AuthenticationChallenge.Parse(new string(chars));
        }
    }

    [Fact]
    public void Parse_is_linear_in_the_input()
    {
        var small = string.Concat(Enumerable.Repeat("a=b,", 20_000));
        var large = string.Concat(Enumerable.Repeat("a=b,", 80_000));
        _ = AuthenticationChallenge.Parse(small); // warm up

        var smallBytes = Allocated(() => AuthenticationChallenge.Parse(small));
        var largeBytes = Allocated(() => AuthenticationChallenge.Parse(large));

        Assert.True(largeBytes <= smallBytes * 5, $"{largeBytes} bytes for 4x the input versus {smallBytes}.");

        var quoted = "Basic realm=\"" + new string('x', 1_000_000) + "\"";
        Assert.Single(AuthenticationChallenge.Parse(quoted));
        Assert.Empty(AuthenticationChallenge.Parse(new string(',', 1_000_000)));
        Assert.Single(AuthenticationChallenge.Parse("Basic " + new string('a', 1_000_000)));
    }

    [Fact]
    public void A_quoted_comma_is_not_a_challenge_separator()
    {
        Assert.Single(AuthenticationChallenge.Parse("Digest realm=\"a, Basic realm=b\", nonce=\"n\""));
    }

    [Fact]
    public void An_escaped_quote_does_not_end_the_value()
    {
        var challenge = AuthenticationChallenge.Parse("Digest realm=\"a\\\"b, c\", nonce=\"n\"").Single();

        Assert.Equal("a\"b, c", challenge.Parameters["realm"]);
        Assert.Equal("n", challenge.Parameters["nonce"]);
    }

    [Fact]
    public void ToString_prints_names_and_never_values()
    {
        var text = AuthenticationChallenge.Parse("Digest nonce=\"n0nce-Zx9\"").Single().ToString();

        Assert.Contains("nonce", text, StringComparison.Ordinal);
        Assert.DoesNotContain("n0nce-Zx9", text, StringComparison.Ordinal);
    }

    private static long Allocated(Action action)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void AssertMatches(ChallengeCase vector, IReadOnlyList<AuthenticationChallenge> actual)
    {
        Assert.Equal(vector.Expected.Count, actual.Count);
        for (var i = 0; i < actual.Count; i++)
        {
            var expected = vector.Expected[i];
            Assert.Equal(expected.Scheme, actual[i].Scheme);
            Assert.Equal(expected.Token68, actual[i].Token68);
            var parameters = actual[i].Parameters.Where(p => p.Key != AuthenticationChallenge.Token68Key)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            Assert.Equal(expected.Parameters.OrderBy(p => p.Key, StringComparer.Ordinal), parameters.OrderBy(p => p.Key, StringComparer.Ordinal));
        }
    }

    public sealed class ChallengeCase
    {
        public string Name { get; set; } = string.Empty;

        public string? Input { get; set; }

        public string Source { get; set; } = string.Empty;

        public List<ExpectedChallenge> Expected { get; set; } = [];
    }

    public sealed class ExpectedChallenge
    {
        public string Scheme { get; set; } = string.Empty;

        public Dictionary<string, string> Parameters { get; set; } = [];

        public string? Token68 { get; set; }
    }
}
