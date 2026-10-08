// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Auth;

/// <summary>AUTH-14 and AUTH-25: the Basic challenge handler.</summary>
[Trait("Category", "Unit")]
public sealed class BasicChallengeHandlerTests
{
    private static readonly Request s_request = Request.Get("https://api.example.com/v1/items");

    private static IReadOnlyList<AuthenticationChallenge> Challenges(string header) => AuthenticationChallenge.Parse(header);

    [Theory]
    [InlineData("Basic realm=\"a\"")]
    [InlineData("Basic realm=\"other\"")]
    [InlineData("BASIC")]
    public void Answers_a_basic_challenge_with_any_realm_by_setting_Authorization(string header)
    {
        var handler = new BasicChallengeHandler(new BasicCredential("u", "p"));

        var answer = handler.Authorize(Challenges(header), s_request, proxy: false);

        Assert.NotNull(answer);
        Assert.StartsWith("Basic ", answer.Headers.Get("Authorization"), StringComparison.Ordinal);
    }

    [Fact]
    public void Value_is_Basic_plus_base64_of_UTF8_user_colon_password()
    {
        var aladdin = new BasicChallengeHandler(new BasicCredential("Aladdin", "open sesame"))
            .Authorize(Challenges(ChallengeFixtures.Basic), s_request, proxy: false);
        Assert.Equal("Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==", aladdin!.Headers.Get("Authorization"));

        var unicode = new BasicChallengeHandler(new BasicCredential("u", "päss€"))
            .Authorize(Challenges(ChallengeFixtures.Basic), s_request, proxy: false);
        var encoded = unicode!.Headers.Get("Authorization")!["Basic ".Length..];
        Assert.Equal("u:päss€", Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));

        var blank = new BasicChallengeHandler(new BasicCredential("u", " "))
            .Authorize(Challenges(ChallengeFixtures.Basic), s_request, proxy: false);
        Assert.NotNull(blank);
    }

    [Theory]
    [InlineData("Bearer realm=\"a\"")]
    [InlineData("Digest realm=\"a\", nonce=\"n\"")]
    [InlineData("")]
    public void Returns_null_when_no_basic_challenge_is_present(string header)
    {
        var handler = new BasicChallengeHandler(new BasicCredential("u", "p"));

        Assert.Null(handler.Authorize(Challenges(header), s_request, proxy: false));
    }

    [Fact]
    public void Sets_Proxy_Authorization_when_proxy_is_true()
    {
        var handler = new BasicChallengeHandler(new BasicCredential("u", "p"));

        var answer = handler.Authorize(Challenges(ChallengeFixtures.Basic), s_request, proxy: true)!;

        Assert.StartsWith("Basic ", answer.Headers.Get("Proxy-Authorization"), StringComparison.Ordinal);
        Assert.Null(answer.Headers.Get("Authorization"));
    }

    [Fact]
    public void Replaces_an_existing_header_rather_than_appending()
    {
        var request = s_request.WithHeaders(s_request.Headers.Set("Authorization", "Basic old"));
        var handler = new BasicChallengeHandler(new BasicCredential("u", "p"));

        var answer = handler.Authorize(Challenges(ChallengeFixtures.Basic), request, proxy: false)!;

        Assert.Single(answer.Headers.GetAll("Authorization"));
        Assert.NotEqual("Basic old", answer.Headers.Get("Authorization"));
    }

    [Fact]
    public void Returns_a_new_request_and_leaves_the_input_untouched()
    {
        var handler = new BasicChallengeHandler(new BasicCredential("u", "p"));

        var answer = handler.Authorize(Challenges(ChallengeFixtures.Basic), s_request, proxy: false)!;

        Assert.NotSame(s_request, answer);
        Assert.Null(s_request.Headers.Get("Authorization"));
    }

    [Fact]
    public void The_value_equals_the_one_BasicAuthPolicy_stamps_preemptively()
    {
        var credential = new BasicCredential("alice", "s3cr3t");
        var handler = new BasicChallengeHandler(credential);

        var answer = handler.Authorize(Challenges(ChallengeFixtures.Basic), s_request, proxy: false)!;

        Assert.Equal("Basic " + credential.ToBase64(), answer.Headers.Get("Authorization"));
        Assert.Equal(credential.HeaderValue, answer.Headers.Get("Authorization"));
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        var handler = new BasicChallengeHandler(new BasicCredential("u", "p"));

        Assert.Throws<ArgumentNullException>(() => new BasicChallengeHandler(null!));
        Assert.Throws<ArgumentNullException>(() => handler.Authorize(null!, s_request, proxy: false));
        Assert.Throws<ArgumentNullException>(() => handler.Authorize([], null!, proxy: false));
    }
}

/// <summary>AUTH-23 and AUTH-24: the composite handler.</summary>
[Trait("Category", "Unit")]
public sealed class CompositeChallengeHandlerTests
{
    private static readonly Request s_request = Request.Get("https://api.example.com/v1/items");

    private static IReadOnlyList<AuthenticationChallenge> Challenges => AuthenticationChallenge.Parse(ChallengeFixtures.Basic);

    [Fact]
    public void The_first_non_null_answer_wins()
    {
        var composite = new CompositeChallengeHandler(new Fixed(null), new Fixed("first"), new Fixed("second"));

        Assert.Equal("first", composite.Authorize(Challenges, s_request, proxy: false)!.Headers.Get("X-Answer"));
    }

    [Fact]
    public void Order_is_the_constructor_order()
    {
        var a = new CompositeChallengeHandler(new Fixed("strong"), new Fixed("weak"));
        var b = new CompositeChallengeHandler(new Fixed("weak"), new Fixed("strong"));

        Assert.Equal("strong", a.Authorize(Challenges, s_request, proxy: false)!.Headers.Get("X-Answer"));
        Assert.Equal("weak", b.Authorize(Challenges, s_request, proxy: false)!.Headers.Get("X-Answer"));
    }

    [Fact]
    public void Returns_null_when_every_handler_declines()
    {
        Assert.Null(new CompositeChallengeHandler(new Fixed(null), new Fixed(null)).Authorize(Challenges, s_request, proxy: false));
    }

    [Fact]
    public void Copies_its_handlers()
    {
        var handlers = new IChallengeHandler[] { new Fixed("a") };
        var composite = new CompositeChallengeHandler(handlers);

        handlers[0] = new Fixed("b");

        Assert.Equal("a", composite.Authorize(Challenges, s_request, proxy: false)!.Headers.Get("X-Answer"));
    }

    [Fact]
    public void An_empty_list_or_a_null_element_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CompositeChallengeHandler(Array.Empty<IChallengeHandler>()));
        Assert.Throws<ArgumentException>(() => new CompositeChallengeHandler(new IChallengeHandler?[] { null }!));
        Assert.Throws<ArgumentNullException>(() => new CompositeChallengeHandler(null!));
    }

    [Fact]
    public void Passes_the_proxy_flag_through()
    {
        var recorder = new Recording();

        _ = new CompositeChallengeHandler(recorder).Authorize(Challenges, s_request, proxy: true);

        Assert.True(recorder.LastProxy);
    }

    [Fact]
    public void A_handler_after_the_winner_is_not_called()
    {
        var after = new Recording();

        _ = new CompositeChallengeHandler(new Fixed("win"), after).Authorize(Challenges, s_request, proxy: false);

        Assert.Equal(0, after.Calls);
    }

    [Fact]
    public async Task Is_stateless_and_safe_under_parallel_use()
    {
        var composite = new CompositeChallengeHandler(
            new Fixed(null),
            new BasicChallengeHandler(new BasicCredential("u", "p")));

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(
            _ => Task.Run(() => composite.Authorize(Challenges, s_request, proxy: false), TestContext.Current.CancellationToken)));

        Assert.All(results, r => Assert.StartsWith("Basic ", r!.Headers.Get("Authorization"), StringComparison.Ordinal));
    }

    private sealed class Fixed(string? answer) : IChallengeHandler
    {
        public Request? Authorize(IReadOnlyList<AuthenticationChallenge> challenges, Request request, bool proxy) =>
            answer is null ? null : request.WithHeaders(request.Headers.Set("X-Answer", answer));
    }

    private sealed class Recording : IChallengeHandler
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public bool LastProxy { get; private set; }

        public Request? Authorize(IReadOnlyList<AuthenticationChallenge> challenges, Request request, bool proxy)
        {
            Interlocked.Increment(ref _calls);
            LastProxy = proxy;
            return null;
        }
    }
}

/// <summary>AUTH-25: every shipped handler changes only the header the proxy flag selects.</summary>
[Trait("Category", "Unit")]
public sealed class ChallengeHandlerContractTests
{
    private static readonly Request s_request = Request.Get("https://api.example.com/v1/items")
        .WithHeaders(Headers.Empty.Set("X-Keep", "1"));

    public static TheoryData<string, string, IChallengeHandler> Handlers => new()
    {
        { "basic", ChallengeFixtures.Basic, new BasicChallengeHandler(new BasicCredential("u", "p")) },
        {
            "composite",
            ChallengeFixtures.Basic,
            new CompositeChallengeHandler(new BasicChallengeHandler(new BasicCredential("u", "p")))
        },
        { "digest", ChallengeFixtures.DigestMd5, new DigestChallengeHandler(new DigestCredential("u", "p")) },
        { "digest-sha256-sess", ChallengeFixtures.DigestSha256Sess, new DigestChallengeHandler(new DigestCredential("u", "p")) },
    };

    [Theory]
    [MemberData(nameof(Handlers))]
    public void The_return_is_null_or_a_request_whose_only_changed_header_is_the_one_the_flag_selects(
        string name,
        string header,
        IChallengeHandler handler)
    {
        Assert.NotEmpty(name);
        var challenges = AuthenticationChallenge.Parse(header);

        foreach (var (proxy, expected, other) in new[] { (false, "Authorization", "Proxy-Authorization"), (true, "Proxy-Authorization", "Authorization") })
        {
            var answer = handler.Authorize(challenges, s_request, proxy);

            Assert.NotNull(answer);
            Assert.NotNull(answer.Headers.Get(expected));
            Assert.Null(answer.Headers.Get(other));
            Assert.Equal("1", answer.Headers.Get("X-Keep"));
            Assert.Equal(s_request.Url, answer.Url);
            Assert.Equal(s_request.Method, answer.Method);
        }
    }
}
