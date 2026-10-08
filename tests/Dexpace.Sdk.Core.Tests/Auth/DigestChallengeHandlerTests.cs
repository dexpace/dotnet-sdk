// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Reflection;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>AUTH-15 to AUTH-22 and AUTH-24: the Digest challenge handler, over the shared vectors and by rule.</summary>
[Trait("Category", "Unit")]
public sealed class DigestChallengeHandlerTests
{
    private const string Mufasa = "Digest realm=\"testrealm@host.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"";

    public static TheoryData<string> VectorNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var vector in VectorFile.Load<DigestCase>("auth/digest.json"))
            {
                data.Add(vector.Name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(VectorNames))]
    public void Every_vector_row_produces_its_expected_Authorization_or_null(string name)
    {
        var vector = VectorFile.Load<DigestCase>("auth/digest.json").Single(v => v.Name == name);
        var handler = HandlerFor(vector);
        var request = new Request(Method.Of(vector.Method), new Uri(vector.Url));
        var challenges = vector.Challenges.SelectMany(AuthenticationChallenge.Parse).ToList();

        var first = handler.Authorize(challenges, request, proxy: false);
        Assert.Equal(vector.Expected?.Authorization, first?.Headers.Get("Authorization"));
        if (vector.Expected is not null || vector.Second is not null)
        {
            Assert.Null(first?.Headers.Get("Proxy-Authorization"));
        }

        if (vector.Second is not null)
        {
            var second = handler.Authorize(challenges, request, proxy: false);
            Assert.Equal(vector.Second.Authorization, second?.Headers.Get("Authorization"));
        }
    }

    [Fact]
    public void Every_vector_has_a_source()
    {
        Assert.All(VectorFile.Load<DigestCase>("auth/digest.json"), v => Assert.False(string.IsNullOrWhiteSpace(v.Source), v.Name));
    }

    // ---- Selection (AUTH-16) -------------------------------------------------

    private static DigestChallengeHandler Handler(
        IEnumerable<DigestAlgorithm>? preference = null,
        bool md5 = true,
        string cnonce = "cn",
        string user = "u",
        string password = "p") =>
        new(new DigestCredential(user, password), preference ?? DigestChallengeHandler.DefaultPreference, md5, () => cnonce);

    private static Request Get(string url = "https://host/x") => Request.Get(url);

    private static string? Answer(DigestChallengeHandler handler, params string[] headers) =>
        handler.Authorize([.. headers.SelectMany(AuthenticationChallenge.Parse)], Get(), proxy: false)?.Headers.Get("Authorization");

    private static Dictionary<string, string> Fields(string header) =>
        AuthenticationChallenge.Parse(header).Single().Parameters.ToDictionary(p => p.Key, p => p.Value);

    [Fact]
    public void Satisfiable_requires_digest_realm_nonce()
    {
        var handler = Handler();

        Assert.NotNull(Answer(handler, "Digest realm=\"r\", nonce=\"n\""));
        Assert.Null(Answer(handler, "Digest realm=\"r\""));
        Assert.Null(Answer(handler, "Digest nonce=\"n\""));
        Assert.Null(Answer(handler, "Basic realm=\"r\""));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(", qop=\"auth\"", true)]
    [InlineData(", qop=\"auth-int,auth\"", true)]
    [InlineData(", qop=\"AUTH\"", true)]
    [InlineData(", qop=\"auth-int\"", false)]
    public void Qop_absent_or_a_list_containing_auth_is_accepted(string qop, bool accepted)
    {
        var answer = Answer(Handler(), "Digest realm=\"r\", nonce=\"n\"" + qop);

        Assert.Equal(accepted, answer is not null);
    }

    [Fact]
    public void Algorithm_absent_means_MD5()
    {
        var answer = Answer(Handler(), "Digest realm=\"r\", nonce=\"n\"")!;

        Assert.Equal("MD5", Fields(answer)["algorithm"]);
    }

    [Fact]
    public void Chosen_by_configured_preference_not_wire_order()
    {
        var md5First = new[] { "Digest realm=\"r\", nonce=\"a\", algorithm=MD5", "Digest realm=\"r\", nonce=\"b\", algorithm=SHA-256" };
        var sha = Fields(Answer(Handler(), md5First)!);
        var md5 = Fields(Answer(Handler([DigestAlgorithm.Md5, DigestAlgorithm.Sha256]), md5First)!);

        Assert.Equal("SHA-256", sha["algorithm"]);
        Assert.Equal("b", sha["nonce"]);
        Assert.Equal("MD5", md5["algorithm"]);
        Assert.Equal("a", md5["nonce"]);
    }

    [Fact]
    public void Default_preference_is_SHA256_SHA256sess_MD5_MD5sess()
    {
        Assert.Equal(
            [DigestAlgorithm.Sha256, DigestAlgorithm.Sha256Sess, DigestAlgorithm.Md5, DigestAlgorithm.Md5Sess],
            DigestChallengeHandler.DefaultPreference);
    }

    [Fact]
    public void A_caller_preference_is_any_non_empty_duplicate_free_subset()
    {
        var credential = new DigestCredential("u", "p");

        Assert.Throws<ArgumentException>(() => new DigestChallengeHandler(credential, Array.Empty<DigestAlgorithm>()));
        Assert.Throws<ArgumentException>(() => new DigestChallengeHandler(credential, [DigestAlgorithm.Md5, DigestAlgorithm.Md5]));
        Assert.Throws<ArgumentException>(() => new DigestChallengeHandler(credential, [(DigestAlgorithm)42]));
        Assert.Throws<ArgumentNullException>(() => new DigestChallengeHandler(null!));
        Assert.NotNull(new DigestChallengeHandler(credential, [DigestAlgorithm.Sha256Sess]));
    }

    [Fact]
    public void A_challenge_among_several_is_picked_by_scheme()
    {
        var answer = Answer(Handler(), "Bearer realm=\"b\"", "Digest realm=\"r\", nonce=\"n\"");

        Assert.NotNull(answer);
        Assert.StartsWith("Digest ", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Sess_without_qop_is_declined()
    {
        Assert.Null(Answer(Handler(), "Digest realm=\"r\", nonce=\"n\", algorithm=SHA-256-sess"));
        Assert.Null(Answer(Handler(), "Digest realm=\"r\", nonce=\"n\", algorithm=MD5-sess"));
        Assert.NotNull(Answer(Handler(), "Digest realm=\"r\", nonce=\"n\", qop=\"auth\", algorithm=MD5-sess"));
    }

    // ---- Wire form (AUTH-22) ---------------------------------------------------

    [Fact]
    public void Username_realm_nonce_uri_response_cnonce_opaque_are_quoted_qop_nc_algorithm_are_bare()
    {
        var header = Answer(Handler(cnonce: "cn"), Mufasa)!;

        foreach (var name in new[] { "username", "realm", "nonce", "uri", "response", "cnonce", "opaque" })
        {
            Assert.Contains(name + "=\"", header, StringComparison.Ordinal);
        }

        Assert.Contains(", qop=auth,", header, StringComparison.Ordinal);
        Assert.Contains(", nc=00000001,", header, StringComparison.Ordinal);
        Assert.Contains(", algorithm=MD5,", header, StringComparison.Ordinal);
        Assert.DoesNotContain("userhash", header, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SHA-256", "SHA-256")]
    [InlineData("sha-256-sess", "SHA-256-sess")]
    [InlineData("md5", "MD5")]
    [InlineData("MD5-SESS", "MD5-sess")]
    public void Algorithm_is_always_emitted_in_full_spelling(string challenge, string expected)
    {
        var header = Answer(Handler(), $"Digest realm=\"r\", nonce=\"n\", qop=\"auth\", algorithm={challenge}")!;

        Assert.Equal(expected, Fields(header)["algorithm"]);
    }

    [Fact]
    public void Qop_nc_cnonce_only_when_qop_is_negotiated()
    {
        var legacy = Fields(Answer(Handler(), "Digest realm=\"r\", nonce=\"n\"")!);
        var negotiated = Fields(Answer(Handler(), "Digest realm=\"r\", nonce=\"n\", qop=\"auth\"")!);

        Assert.DoesNotContain("qop", legacy.Keys);
        Assert.DoesNotContain("nc", legacy.Keys);
        Assert.DoesNotContain("cnonce", legacy.Keys);
        Assert.Equal("auth", negotiated["qop"]);
        Assert.Equal("00000001", negotiated["nc"]);
        Assert.Equal("cn", negotiated["cnonce"]);
    }

    [Fact]
    public void Opaque_only_when_sent()
    {
        Assert.DoesNotContain("opaque", Fields(Answer(Handler(), "Digest realm=\"r\", nonce=\"n\"")!).Keys);
        Assert.Equal("o", Fields(Answer(Handler(), "Digest realm=\"r\", nonce=\"n\", opaque=\"o\"")!)["opaque"]);
    }

    [Fact]
    public void A_username_with_non_ascii_goes_out_as_username_star()
    {
        var utf8 = Answer(Handler(user: "Jäsøn Doe"), "Digest realm=\"r\", nonce=\"n\", charset=UTF-8")!;
        var latin1 = Answer(Handler(user: "José"), "Digest realm=\"r\", nonce=\"n\"")!;

        Assert.Contains("username*=UTF-8''J%C3%A4s%C3%B8n%20Doe,", utf8, StringComparison.Ordinal);
        Assert.DoesNotContain("username=\"", utf8, StringComparison.Ordinal);
        Assert.Contains("username*=ISO-8859-1''Jos%E9,", latin1, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unechoable_realm_nonce_or_opaque_is_declined()
    {
        Assert.Null(Answer(Handler(), "Digest realm=\"café\", nonce=\"n\""));
        Assert.Null(Answer(Handler(), "Digest realm=\"r\", nonce=\"né\""));
        Assert.Null(Answer(Handler(), "Digest realm=\"r\", nonce=\"n\", opaque=\"oé\""));
    }

    [Fact]
    public void The_header_value_passes_the_outbound_grammar()
    {
        var request = Get();
        var header = Answer(Handler(user: "a\"b", password: "p"), Mufasa)!;

        var set = request.Headers.Set("Authorization", header);

        Assert.Equal(header, set.Get("Authorization"));
    }

    [Fact]
    public void Proxy_true_sets_Proxy_Authorization_only()
    {
        var challenges = AuthenticationChallenge.Parse(Mufasa);

        var answer = Handler().Authorize(challenges, Get(), proxy: true)!;

        Assert.StartsWith("Digest ", answer.Headers.Get("Proxy-Authorization"), StringComparison.Ordinal);
        Assert.Null(answer.Headers.Get("Authorization"));
    }

    // ---- FIPS (AUTH-15, P6c-30) --------------------------------------------------

    [Fact]
    public void With_MD5_unavailable_an_MD5_only_challenge_is_declined()
    {
        Assert.Null(Answer(Handler(md5: false), "Digest realm=\"r\", nonce=\"n\""));
        Assert.Null(Answer(Handler(md5: false), "Digest realm=\"r\", nonce=\"n\", qop=\"auth\", algorithm=MD5-sess"));
    }

    [Fact]
    public void With_MD5_unavailable_a_challenge_offering_both_falls_through_to_SHA256()
    {
        var answer = Answer(
            Handler([DigestAlgorithm.Md5, DigestAlgorithm.Sha256], md5: false),
            "Digest realm=\"r\", nonce=\"a\", algorithm=MD5",
            "Digest realm=\"r\", nonce=\"b\", algorithm=SHA-256")!;

        Assert.Equal("SHA-256", Fields(answer)["algorithm"]);
    }

    [Fact]
    public void Construction_never_fails_on_a_host_without_MD5_even_with_an_MD5_only_preference()
    {
        var handler = Handler([DigestAlgorithm.Md5, DigestAlgorithm.Md5Sess], md5: false);

        Assert.Null(Answer(handler, Mufasa));
        Assert.False(handler.Md5Available);
    }

    // ---- Not built on purpose, counters ------------------------------------------

    [Fact]
    public void No_rspauth_verification()
    {
        var members = typeof(DigestChallengeHandler).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Where(n => n.Contains("Info", StringComparison.OrdinalIgnoreCase) || n.Contains("rspauth", StringComparison.OrdinalIgnoreCase) || n.Contains("Verify", StringComparison.OrdinalIgnoreCase));

        Assert.Empty(members);
    }

    [Fact]
    public void A_declined_attempt_does_not_advance_nc()
    {
        // The password has no ISO-8859-1 code: the Latin-1 challenge is declined and must not consume a count.
        var handler = Handler(password: "日本");
        Assert.Null(Answer(handler, "Digest realm=\"r\", nonce=\"same\", qop=\"auth\""));

        var utf8 = Answer(handler, "Digest realm=\"r\", nonce=\"same\", qop=\"auth\", charset=UTF-8")!;

        Assert.Equal("00000001", Fields(utf8)["nc"]);
    }

    [Fact]
    public void The_cnonce_default_is_32_lower_case_hex_characters_and_fresh_each_time()
    {
        var handler = new DigestChallengeHandler(new DigestCredential("u", "p"));
        var challenges = AuthenticationChallenge.Parse("Digest realm=\"r\", nonce=\"n\", qop=\"auth\", algorithm=SHA-256");

        var cnonces = Enumerable.Range(0, 5)
            .Select(_ => Fields(handler.Authorize(challenges, Get(), proxy: false)!.Headers.Get("Authorization")!)["cnonce"])
            .ToList();

        Assert.All(cnonces, c => Assert.Matches("^[0-9a-f]{32}$", c));
        Assert.Equal(5, cnonces.Distinct().Count());
    }

    private static DigestChallengeHandler HandlerFor(DigestCase vector)
    {
        var preference = vector.Preference?.Select(Enum.Parse<DigestAlgorithm>).ToList() ?? [.. DigestChallengeHandler.DefaultPreference];
        return new DigestChallengeHandler(
            new DigestCredential(vector.Credential.User, vector.Credential.Password),
            preference,
            vector.Md5Available ?? true,
            () => vector.Cnonce);
    }

    public sealed class DigestCase
    {
        public string Name { get; set; } = string.Empty;

        public List<string> Challenges { get; set; } = [];

        public DigestCredentialCase Credential { get; set; } = new();

        public string Method { get; set; } = "GET";

        public string Url { get; set; } = string.Empty;

        public string Cnonce { get; set; } = string.Empty;

        public List<string>? Preference { get; set; }

        public bool? Md5Available { get; set; }

        public ExpectedHeader? Expected { get; set; }

        public ExpectedHeader? Second { get; set; }

        public string Source { get; set; } = string.Empty;
    }

    public sealed class DigestCredentialCase
    {
        public string User { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }

    public sealed class ExpectedHeader
    {
        public string Authorization { get; set; } = string.Empty;
    }
}
