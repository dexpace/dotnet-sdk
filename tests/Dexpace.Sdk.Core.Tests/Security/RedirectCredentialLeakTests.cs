// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 6's convergence point (P6b-25, REDIR-7 to REDIR-11, REDIR-24, XCUT-17, XCUT-16, AUTH-29): the card's exit
/// criterion, that an <c>Authorization</c>, a <c>Cookie</c> and a <c>Proxy-Authorization</c> header each survives neither a
/// cross-origin hop nor a retry across a hop. The pipeline is the real stack, <see cref="RedirectPolicy"/> over
/// <see cref="RetryPolicy"/> over a per-hop probe over a real <see cref="AuthorizationPolicy"/> over a scripted transport,
/// and every assertion runs on <em>every</em> request the transport saw after the seed, never only the last. The caller
/// sets all three headers on the seed; the auth policy re-stamps its own credential per hop and withholds it off the seed
/// origin. The test asserts on headers only, so it holds whichever retry and auth shape lands. Permanent (roadmap
/// constraint 5); the wire half is <c>RedirectCredentialLeakWireTests</c> in the SystemNet suite.
/// </summary>
[Trait("Category", "Security")]
public sealed class RedirectCredentialLeakTests
{
    private const string CallerSecret = "caller-secret";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>What a request must look like at the transport.</summary>
    public enum Shape
    {
        /// <summary>The auth policy's stamp, and the caller's Cookie and Proxy-Authorization kept.</summary>
        StampedAndKept = 0,

        /// <summary>None of the three headers.</summary>
        Bare = 1,

        /// <summary>The auth policy's stamp, with the caller's Cookie and Proxy-Authorization still gone.</summary>
        StampedOnly = 2,
    }

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();
        foreach (var kind in new[] { "Basic", "Bearer" })
        {
            foreach (var name in s_scenarios.Select(s => s.Name))
            {
                data.Add(name, kind);
            }
        }

        return data;
    }

    private static readonly Scenario[] s_scenarios =
    [
        new("same-origin hop", false, () => [Redirect(302, "https://seed.example/b"), Ok()], [Shape.StampedAndKept, Shape.StampedAndKept]),
        new("cross-origin hop", false, () => [Redirect(302, "https://other.example/x"), Ok()], [Shape.StampedAndKept, Shape.Bare]),
        new(
            "cross-origin hop, then a retry",
            false,
            () => [Redirect(302, "https://other.example/x"), Status503(), Ok()],
            [Shape.StampedAndKept, Shape.Bare, Shape.Bare]),
        new(
            "same-origin hop, then a retry",
            false,
            () => [Redirect(307, "https://seed.example/b"), Status503(), Ok()],
            [Shape.StampedAndKept, Shape.StampedAndKept, Shape.StampedAndKept]),
        new(
            "a retry before the hop",
            false,
            () => [Status503(), Redirect(302, "https://other.example/x"), Ok()],
            [Shape.StampedAndKept, Shape.StampedAndKept, Shape.Bare]),
        new(
            "foreign then back to the seed origin, with a retry",
            false,
            () => [Redirect(302, "https://other.example/x"), Redirect(302, "https://seed.example/back"), Status503(), Ok()],
            [Shape.StampedAndKept, Shape.Bare, Shape.StampedOnly, Shape.StampedOnly]),
        new(
            "a permitted downgrade, then a retry",
            true,
            () => [Redirect(302, "http://other.example/x"), Status503(), Ok()],
            [Shape.StampedAndKept, Shape.Bare, Shape.Bare]),
        new("a port change only", false, () => [Redirect(302, "https://seed.example:8443/"), Ok()], [Shape.StampedAndKept, Shape.Bare]),
    ];

    private static Response Redirect(int status, string location) => TestResponses.Redirect(status, location);

    private static Response Status503() => TestResponses.Create(Status.ServiceUnavailable);

    private static Response Ok() => TestResponses.Create(Status.Ok);

    private static Headers CallerCredentials() => new Headers.Builder()
        .Add("Authorization", CallerSecret)
        .Add("Cookie", "session=abc")
        .Add("Proxy-Authorization", "Basic cHJveHk6cHc=")
        .Build();

    private static (AuthorizationPolicy Policy, string Prefix) AuthFor(string kind) => kind == "Basic"
        ? (new BasicAuthPolicy(new BasicCredential("user", "pass")), "Basic ")
        : (new BearerTokenAuthPolicy(new FixedTokenCredential("tok"), "scope"), "Bearer ");

    private static HttpPipeline Pipeline(ScriptedTransport transport, AuthorizationPolicy auth, ProbePolicy probe) =>
        new PipelineBuilder()
            .Add(new RedirectPolicy())
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Add(probe)
            .Add(auth)
            .Build(transport);

    private static void Assert3(Request request, Shape shape, string prefix, string label)
    {
        var authorization = request.Headers.GetAll("Authorization");
        var cookie = request.Headers.GetAll("Cookie");
        var proxy = request.Headers.GetAll("Proxy-Authorization");

        // The caller's own Authorization value must never reach the wire, on any request.
        Assert.DoesNotContain(CallerSecret, authorization);
        switch (shape)
        {
            case Shape.Bare:
                Assert.True(authorization.Count == 0, $"{label}: Authorization leaked");
                Assert.True(cookie.Count == 0, $"{label}: Cookie leaked");
                Assert.True(proxy.Count == 0, $"{label}: Proxy-Authorization leaked");
                break;
            case Shape.StampedAndKept:
                Assert.True(authorization.Count == 1 && authorization[0].StartsWith(prefix, StringComparison.Ordinal), $"{label}: stamp missing");
                Assert.Equal(["session=abc"], cookie);
                Assert.Equal(["Basic cHJveHk6cHc="], proxy);
                break;
            default:
                Assert.True(authorization.Count == 1 && authorization[0].StartsWith(prefix, StringComparison.Ordinal), $"{label}: stamp missing");
                Assert.True(cookie.Count == 0, $"{label}: a stripped Cookie was restored");
                Assert.True(proxy.Count == 0, $"{label}: a stripped Proxy-Authorization was restored");
                break;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task No_credential_survives_a_cross_origin_hop_or_a_retry_across_one(string scenarioName, string authKind)
    {
        var scenario = s_scenarios.Single(s => s.Name == scenarioName);
        var (auth, prefix) = AuthFor(authKind);
        using var transport = new ScriptedTransport(scenario.Script());
        var probe = new ProbePolicy("hop", PipelineStage.PerHop, []);
        var options = new DexpaceClientOptions { Redirect = new RedirectOptions { AllowHttpsToHttpDowngrade = scenario.AllowDowngrade } };
        var seed = Request.Create(Method.Get, "https://seed.example/start", CallerCredentials());

        using var response = await Pipeline(transport, auth, probe).SendAsync(seed, options, Ct);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(scenario.Expected.Length, transport.CallCount);
        var requests = transport.Requests;
        for (var i = 0; i < requests.Count; i++)
        {
            Assert3(requests[i], scenario.Expected[i], prefix, $"{scenarioName} / {authKind} / request {i}");
        }
    }

    [Fact]
    public async Task A_return_to_the_seed_origin_does_not_restore_a_stripped_cookie()
    {
        var (auth, prefix) = AuthFor("Basic");
        using var transport = new ScriptedTransport(
            Redirect(302, "https://other.example/x"),
            Redirect(302, "https://seed.example/back"),
            Ok());
        var seed = Request.Create(Method.Get, "https://seed.example/start", CallerCredentials());

        using var response = await Pipeline(transport, auth, new ProbePolicy("hop", PipelineStage.PerHop, [])).SendAsync(seed, new DexpaceClientOptions(), Ct);

        var back = transport.Requests[2];
        Assert.Equal(new Uri("https://seed.example/back"), back.Url);
        Assert3(back, Shape.StampedOnly, prefix, "back at the seed origin");
    }

    [Fact]
    public async Task A_redirect_exception_is_never_retried()
    {
        var (auth, _) = AuthFor("Basic");
        using var transport = new ScriptedTransport(Redirect(302, "http://other.example/x"), Ok());
        var seed = Request.Create(Method.Get, "https://seed.example/start", CallerCredentials());
        var pipeline = Pipeline(transport, auth, new ProbePolicy("hop", PipelineStage.PerHop, []));

        await Assert.ThrowsAsync<RedirectSchemeDowngradeException>(async () => await pipeline.SendAsync(seed, new DexpaceClientOptions(), Ct));

        Assert.Equal(1, transport.CallCount);
    }

    private sealed record Scenario(string Name, bool AllowDowngrade, Func<object[]> Script, Shape[] Expected);

    private sealed class FixedTokenCredential(string token) : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            new(new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1)));
    }
}
