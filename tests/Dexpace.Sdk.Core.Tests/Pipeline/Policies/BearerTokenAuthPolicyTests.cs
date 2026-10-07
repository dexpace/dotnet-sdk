// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

[Trait("Category", "Unit")]
public sealed class BearerTokenAuthPolicyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly DateTimeOffset s_farFuture =
        new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Request MakeRequest(string url = "https://api.example.com/v1/items")
        => Request.Get(url);

    private static DexpaceClientOptions MakeOptions() => new();

    /// <summary>
    /// A <see cref="TokenCredential"/> that returns a canned token and tracks how many times
    /// <see cref="GetTokenAsync"/> was called.
    /// </summary>
    private sealed class FakeTokenCredential(string token) : TokenCredential
    {
        private int _callCount;

        /// <summary>Number of times <see cref="GetTokenAsync"/> was called.</summary>
        public int CallCount => _callCount;

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext context,
            CancellationToken ct = default)
        {
            Interlocked.Increment(ref _callCount);
            return new ValueTask<AccessToken>(new AccessToken(token, s_farFuture));
        }
    }

    // -------------------------------------------------------------------------
    // Stage
    // -------------------------------------------------------------------------

    [Fact]
    public void Stage_IsAuth()
    {
        var policy = new BearerTokenAuthPolicy(
            new FakeTokenCredential("tok"),
            "https://api.example.com/.default");

        Assert.Equal(PipelineStage.Auth, policy.Stage);
    }

    // -------------------------------------------------------------------------
    // Bearer token stamping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_StampsBearerTokenInAuthorizationHeader()
    {
        var credential = new FakeTokenCredential("abc123");
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BearerTokenAuthPolicy(credential, "scope1", "scope2"))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        var value = transport.LastRequest!.Headers.Get("Authorization");
        Assert.Equal("Bearer abc123", value);
    }

    [Fact]
    public async Task ProcessAsync_ReplacesExistingAuthorizationHeader()
    {
        var credential = new FakeTokenCredential("fresh-token");
        var request = MakeRequest().WithHeaders(Headers.Empty.Set("Authorization", "Bearer old-token"));

        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BearerTokenAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(request, MakeOptions(), TestContext.Current.CancellationToken);

        var values = transport.LastRequest!.Headers.GetAll("Authorization");
        Assert.Single(values);
        Assert.Equal("Bearer fresh-token", values[0]);
    }

    // -------------------------------------------------------------------------
    // Cache reuse — credential called only once across two requests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_CacheReused_CredentialCalledOnceAcrossTwoRequests()
    {
        // Two pipeline sends share the same BearerTokenAuthPolicy instance, so they share
        // the same AccessTokenCache. The token expires far in the future, so the second
        // send must reuse the cached token without calling the credential again.
        var credential = new FakeTokenCredential("shared-token");
        var transport = new RecordingTransport();
        var policy = new BearerTokenAuthPolicy(credential, "read", "write");
        var pipeline = new PipelineBuilder()
            .Add(policy)
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);
        var firstValue = transport.LastRequest!.Headers.Get("Authorization");

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);
        var secondValue = transport.LastRequest!.Headers.Get("Authorization");

        Assert.Equal("Bearer shared-token", firstValue);
        Assert.Equal("Bearer shared-token", secondValue);
        Assert.Equal(1, credential.CallCount);
    }

    // -------------------------------------------------------------------------
    // Scopes are forwarded to the credential
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_ForwardsConfiguredScopesToCredential()
    {
        string[]? capturedScopes = null;
        var credential = new CapturingScopesCredential(
            "scope-token",
            scopes => capturedScopes = scopes);

        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BearerTokenAuthPolicy(credential, "openid", "profile"))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.NotNull(capturedScopes);
        Assert.Equal(["openid", "profile"], capturedScopes!);
    }

    // -------------------------------------------------------------------------
    // Cross-origin withholding
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_WithholdsCredential()
    {
        var policy = new BearerTokenAuthPolicy(new FakeTokenCredential("secret-bearer"), "scope");
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var first = await TestContexts.SentAsync(policy, context.SeedRequest, context);
        Assert.Equal("Bearer secret-bearer", first.Headers.Get("Authorization"));

        // A hop to a different origin: the credential must be withheld.
        var foreign = await TestContexts.SentAsync(
            policy, MakeRequest("https://other-service.example.org/callback").WithHeaders(Headers.Empty), context);
        Assert.Null(foreign.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_SameOriginRerun_StampsBearerAgain()
    {
        var policy = new BearerTokenAuthPolicy(new FakeTokenCredential("retry-bearer"), "scope");
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var first = await TestContexts.SentAsync(policy, context.SeedRequest, context);
        Assert.Equal("Bearer retry-bearer", first.Headers.Get("Authorization"));

        // Same origin retry: must stamp again.
        var second = await TestContexts.SentAsync(policy, first.WithHeaders(Headers.Empty), context);
        Assert.Equal("Bearer retry-bearer", second.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_StripsStaleAuthorizationHeader()
    {
        // A stale Authorization header must be absent on a cross-origin hop, even without RedirectPolicy, and the token
        // cache / credential must NOT be called again.
        var credential = new FakeTokenCredential("secret-bearer");
        var policy = new BearerTokenAuthPolicy(credential, "scope");
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var first = await TestContexts.SentAsync(policy, context.SeedRequest, context);
        Assert.Equal("Bearer secret-bearer", first.Headers.Get("Authorization"));
        var callCountAfterFirstRun = credential.CallCount;

        var foreign = await TestContexts.SentAsync(
            policy,
            MakeRequest("https://other-service.example.org/callback")
                .WithHeaders(Headers.Empty.Set("Authorization", "Bearer secret-bearer")),
            context);

        Assert.Null(foreign.Headers.Get("Authorization"));
        Assert.Equal(callCountAfterFirstRun, credential.CallCount);
    }

    [Fact]
    public void Process_sync_stamps_through_the_documented_bridge()
    {
        var policy = new BearerTokenAuthPolicy(new FakeTokenCredential("sync-bearer"), "scope");
        var context = TestContexts.For();

        var sent = TestContexts.Sent(policy, context.SeedRequest, context);

        Assert.Equal("Bearer sync-bearer", sent.Headers.Get("Authorization"));
    }

    // -------------------------------------------------------------------------
    // Helper: captures scopes passed to GetTokenAsync
    // -------------------------------------------------------------------------

    private sealed class CapturingScopesCredential(
        string token,
        Action<string[]> onGetToken) : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext context,
            CancellationToken ct = default)
        {
            onGetToken([.. context.Scopes]);
            return new ValueTask<AccessToken>(new AccessToken(token, s_farFuture));
        }
    }
}
