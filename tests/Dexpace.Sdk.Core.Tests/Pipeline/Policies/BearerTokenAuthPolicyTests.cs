// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Streams;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Time.Testing;
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
    public void Process_sync_stamps_through_the_real_sync_path()
    {
        // Migrated from the documented bridge (P4c-13, closed): the sync path calls GetToken, never GetTokenAsync.
        var policy = new BearerTokenAuthPolicy(new SyncOnlyCredential("sync-bearer"), "scope");
        var context = TestContexts.For();

        var sent = TestContexts.Sent(policy, context.SeedRequest, context);

        Assert.Equal("Bearer sync-bearer", sent.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task The_sync_and_async_paths_share_one_cache()
    {
        var credential = new FakeTokenCredential("shared");
        var policy = new BearerTokenAuthPolicy(credential, "scope");
        var context = TestContexts.For();

        var viaAsync = await TestContexts.SentAsync(policy, context.SeedRequest, context);
        var viaSync = TestContexts.Sent(policy, context.SeedRequest, context);

        Assert.Equal(viaAsync.Headers.Get("Authorization"), viaSync.Headers.Get("Authorization"));
        Assert.Equal(1, credential.CallCount);
    }

    private sealed class SyncOnlyCredential(string token) : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            throw new InvalidOperationException("The sync path must not call GetTokenAsync.");

        public override AccessToken GetToken(TokenRequestContext context, CancellationToken ct = default) =>
            new(token, s_farFuture);
    }

    // -------------------------------------------------------------------------
    // The 401 hook (AUTH-30, AUTH-31, AUTH-36)
    // -------------------------------------------------------------------------

    /// <summary>A credential that hands out <c>tok-1</c>, <c>tok-2</c>, … (or a fixed token when asked to).</summary>
    private sealed class SequenceCredential(bool repeatFirst = false) : TokenCredential
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
        {
            var n = Interlocked.Increment(ref _calls);
            return new ValueTask<AccessToken>(new AccessToken("tok-" + (repeatFirst ? 1 : n), s_farFuture));
        }
    }

    private static HttpPipeline PipelineOver(BearerTokenAuthPolicy policy, ScriptedTransport transport) =>
        new PipelineBuilder().Add(policy).Build(transport, MakeOptions());

    private static async Task<Response> SendAsync(HttpPipeline pipeline, Request request, bool async) =>
        async
            ? await pipeline.SendAsync(request, RequestOptions.Empty, TestContext.Current.CancellationToken)
            : pipeline.Send(request, RequestOptions.Empty, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_401_with_a_Bearer_challenge_evicts_fetches_fresh_and_retries_once(bool async)
    {
        var credential = new SequenceCredential();
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Bearer realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(PipelineOver(new BearerTokenAuthPolicy(credential, "s"), transport), MakeRequest(), async);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal("Bearer tok-1", transport.Requests[0].Headers.Get("Authorization"));
        Assert.Equal("Bearer tok-2", transport.Requests[1].Headers.Get("Authorization"));
        Assert.Equal(2, credential.Calls);
    }

    [Fact]
    public async Task A_401_with_a_non_bearer_challenge_is_returned()
    {
        var credential = new SequenceCredential();
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""));

        using var response = await SendAsync(PipelineOver(new BearerTokenAuthPolicy(credential, "s"), transport), MakeRequest(), async: true);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
        Assert.Equal(1, credential.Calls);
    }

    [Fact]
    public async Task A_401_without_WWW_Authenticate_is_returned_without_a_retry()
    {
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Unauthorized));

        using var response = await SendAsync(
            PipelineOver(new BearerTokenAuthPolicy(new SequenceCredential(), "s"), transport), MakeRequest(), async: true);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_retry_is_method_agnostic(bool async)
    {
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Bearer realm=\"r\""), TestResponses.Create(Status.Ok));
        var post = Request.Post("https://api.example.com/v1/items", RequestBody.FromString("payload"));

        using var response = await SendAsync(PipelineOver(new BearerTokenAuthPolicy(new SequenceCredential(), "s"), transport), post, async);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal(Method.Post, transport.Requests[1].Method);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_stream_body_is_not_retried_and_the_401_is_returned_undisposed(bool async)
    {
        var body = new TrackingResponseBody([], "401");
        using var transport = new ScriptedTransport(
            TestResponses.Unauthorized(null, body, "Bearer realm=\"r\""),
            TestResponses.Create(Status.Ok));
        var post = Request.Post("https://api.example.com/v1/items", RequestBody.FromStream(new ChunkedReadStream([1, 2, 3], 2)));

        using var response = await SendAsync(PipelineOver(new BearerTokenAuthPolicy(new SequenceCredential(), "s"), transport), post, async);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
        Assert.Equal(0, body.DisposeCount);
    }

    [Fact]
    public async Task A_provider_that_hands_back_the_rejected_token_yields_no_retry()
    {
        var credential = new SequenceCredential(repeatFirst: true);
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Bearer realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(PipelineOver(new BearerTokenAuthPolicy(credential, "s"), transport), MakeRequest(), async: true);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task A_second_401_after_the_retry_is_returned_without_another_retry()
    {
        var credential = new SequenceCredential();
        using var transport = new ScriptedTransport(
            TestResponses.Unauthorized("Bearer realm=\"r\""),
            TestResponses.Unauthorized("Bearer realm=\"r\""));

        using var response = await SendAsync(PipelineOver(new BearerTokenAuthPolicy(credential, "s"), transport), MakeRequest(), async: true);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal(2, credential.Calls);
    }

    [Fact]
    public async Task Per_call_OAuth2_scopes_get_their_own_token_on_retry()
    {
        var recorder = new ScopeRecordingCredential();
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Bearer realm=\"r\""), TestResponses.Create(Status.Ok));
        var pipeline = PipelineOver(new BearerTokenAuthPolicy(recorder, "policy-scope"), transport);
        var options = new RequestOptions { Auth = new AuthDescriptor(new AuthRequirement(AuthScheme.OAuth2) { Scopes = ["call-scope"] }) };

        using var response = await pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken);

        Assert.Equal(["call-scope", "call-scope"], recorder.Scopes);
    }

    [Fact]
    public async Task Retry_uses_the_logger_of_the_call_for_background_failures()
    {
        // A pipeline built with a shared cache whose background refresh fails reports through the call's logger (event 160).
        var time = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var calls = 0;
        var cache = new AccessTokenCache(
            new DelegateCredential(_ =>
            {
                var n = ++calls;
                return n == 1 ? new AccessToken("tok-1", time.GetUtcNow().AddSeconds(60)) : throw new InvalidOperationException("down");
            }),
            time);
        var logger = new RecordingLogger();
        var context = TestContexts.For(logger: logger);
        var policy = new BearerTokenAuthPolicy(cache, "s");
        using var first = new ScriptedTransport(TestResponses.Create(Status.Ok), TestResponses.Create(Status.Ok));

        using var r1 = await policy.ProcessAsync(context.SeedRequest, context, TestContexts.RunnerOver(first));
        time.Advance(TimeSpan.FromSeconds(40));
        using var r2 = await policy.ProcessAsync(context.SeedRequest, context, TestContexts.RunnerOver(first));
        await cache.PendingRefresh(new TokenRequestContext(["s"]))!;

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(160, entry.EventId.Id);
    }

    private sealed class DelegateCredential(Func<TokenRequestContext, AccessToken> factory) : TokenCredential
    {
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            new(factory(context));
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

    [Fact]
    public async Task Per_call_OAuth2_scopes_fetch_a_token_for_those_scopes()
    {
        var recorder = new ScopeRecordingCredential();
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BearerTokenAuthPolicy(recorder, "policy-scope"))
            .Build(transport);
        var options = new RequestOptions
        {
            Auth = new AuthDescriptor(new AuthRequirement(AuthScheme.OAuth2) { Scopes = ["call-scope"] }),
        };

        using var perCall = await pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken);
        using var byDefault = await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(["call-scope", "policy-scope"], recorder.Scopes);
    }

    private sealed class ScopeRecordingCredential : TokenCredential
    {
        private readonly List<string> _scopes = [];

        public IReadOnlyList<string> Scopes
        {
            get
            {
                lock (_scopes)
                {
                    return [.. _scopes];
                }
            }
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
        {
            lock (_scopes)
            {
                _scopes.AddRange(context.Scopes);
            }

            return new ValueTask<AccessToken>(new AccessToken("tok", s_farFuture));
        }
    }
}
