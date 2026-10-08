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
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>AUTH-4 and AUTH-5 end to end (P6c-7, P6c-36): the descriptor-driven, multi-credential policy.</summary>
[Trait("Category", "Unit")]
public sealed class MultiSchemeAuthPolicyTests
{
    private const string Url = "https://api.example.com/v1/items";

    private static readonly DexpaceClientOptions s_options = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AuthDescriptor Of(params AuthScheme[] schemes) => new(schemes.Select(static s => new AuthRequirement(s)));

    private static AuthCredentials AllCredentials(SequenceTokens? tokens = null) => new()
    {
        Token = tokens ?? new SequenceTokens(),
        ApiKey = new ApiKeyCredential("key-1", HttpHeaderName.Of("X-Api-Key")),
        Basic = new BasicCredential("alice", "s3cr3t"),
        Digest = new DigestCredential("alice", "s3cr3t"),
    };

    private static HttpPipeline Pipeline(MultiSchemeAuthPolicy policy, ScriptedTransport transport) =>
        new PipelineBuilder().Add(policy).Build(transport, s_options);

    private static async Task<Response> SendAsync(HttpPipeline pipeline, RequestOptions? options = null, bool async = true) =>
        async
            ? await pipeline.SendAsync(Request.Get(Url), options ?? RequestOptions.Empty, Ct)
            : pipeline.Send(Request.Get(Url), options ?? RequestOptions.Empty, Ct);

    [Fact]
    public async Task Available_schemes_are_the_configured_credentials()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.Digest, AuthScheme.Basic), new AuthCredentials { Basic = new BasicCredential("u", "p") });
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(policy, transport));

        Assert.StartsWith("Basic ", transport.LastRequest!.Headers.Get("Authorization"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_client_descriptor_order_decides()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.OAuth2, AuthScheme.Basic), AllCredentials());
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(policy, transport));

        Assert.Equal("Bearer tok-1", transport.LastRequest!.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task A_per_call_descriptor_overrides_the_client_one()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.OAuth2), AllCredentials());
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var options = new RequestOptions { Auth = Of(AuthScheme.Basic) };

        using var response = await SendAsync(Pipeline(policy, transport), options);

        Assert.StartsWith("Basic ", transport.LastRequest!.Headers.Get("Authorization"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_operation_descriptor_is_used_when_no_per_call_one_is_set()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.OAuth2), AllCredentials());
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var options = new RequestOptions { OperationAuth = Of(AuthScheme.ApiKey) };

        using var response = await SendAsync(Pipeline(policy, transport), options);

        Assert.Equal("key-1", transport.LastRequest!.Headers.Get("X-Api-Key"));
        Assert.Null(transport.LastRequest.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task A_per_call_NoAuth_sends_anonymously()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.OAuth2), AllCredentials());
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var options = new RequestOptions { Auth = new AuthDescriptor(AuthRequirement.NoAuth) };

        using var response = await SendAsync(Pipeline(policy, transport), options);

        Assert.Null(transport.LastRequest!.Headers.Get("Authorization"));
        Assert.Null(transport.LastRequest.Headers.Get("X-Api-Key"));
    }

    [Fact]
    public async Task A_descriptor_naming_only_unconfigured_schemes_throws_AuthResolutionException_before_sending()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.Basic), new AuthCredentials { Basic = new BasicCredential("u", "p") });
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var options = new RequestOptions { Auth = Of(AuthScheme.Digest) };

        await Assert.ThrowsAsync<AuthResolutionException>(() => SendAsync(Pipeline(policy, transport), options));

        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task ApiKey_is_stamped_in_its_own_header_and_withheld_cross_origin()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.ApiKey), AllCredentials());
        var context = TestContexts.For(Request.Get("https://a.example.com/"));
        var foreign = Request.Get("https://b.example.com/")
            .WithHeaders(Headers.Empty.Set("X-Api-Key", "stale").Set("Authorization", "stale"));

        var sent = await TestContexts.SentAsync(policy, foreign, context);

        Assert.Null(sent.Headers.Get("X-Api-Key"));
        Assert.Null(sent.Headers.Get("Authorization"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Basic_is_preemptive(bool async)
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.Basic), AllCredentials());
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(policy, transport), async: async);

        Assert.Equal("Basic YWxpY2U6czNjcjN0", transport.LastRequest!.Headers.Get("Authorization"));
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Digest_is_reactive_and_answers_one_401(bool async)
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.Digest), AllCredentials());
        using var transport = new ScriptedTransport(
            TestResponses.Unauthorized("Digest realm=\"r\", nonce=\"n\", qop=\"auth\""),
            TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(policy, transport), async: async);

        Assert.Equal(200, response.Status.Code);
        Assert.Null(transport.Requests[0].Headers.Get("Authorization"));
        Assert.StartsWith("Digest ", transport.Requests[1].Headers.Get("Authorization"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OAuth2_is_stamped_and_retried_once_on_a_Bearer_401(bool async)
    {
        var tokens = new SequenceTokens();
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.OAuth2), AllCredentials(tokens));
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Bearer realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(policy, transport), async: async);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal("Bearer tok-1", transport.Requests[0].Headers.Get("Authorization"));
        Assert.Equal("Bearer tok-2", transport.Requests[1].Headers.Get("Authorization"));
        Assert.Equal(2, tokens.Calls);
    }

    [Fact]
    public async Task Per_call_OAuth2_scopes_reach_the_token_provider()
    {
        var tokens = new SequenceTokens();
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.OAuth2), AllCredentials(tokens));
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var options = new RequestOptions { Auth = new AuthDescriptor(new AuthRequirement(AuthScheme.OAuth2) { Scopes = ["a", "b"] }) };

        using var response = await SendAsync(Pipeline(policy, transport), options);

        Assert.Equal(["a", "b"], tokens.LastScopes);
    }

    [Fact]
    public async Task Two_calls_with_different_tiers_use_different_schemes_through_one_policy_instance()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.OAuth2, AuthScheme.Basic), AllCredentials());
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok), TestResponses.Create(Status.Ok));
        var pipeline = Pipeline(policy, transport);

        using var first = await SendAsync(pipeline);
        using var second = await SendAsync(pipeline, new RequestOptions { Auth = Of(AuthScheme.ApiKey) });

        Assert.StartsWith("Bearer ", transport.Requests[0].Headers.Get("Authorization"), StringComparison.Ordinal);
        Assert.Equal("key-1", transport.Requests[1].Headers.Get("X-Api-Key"));
        Assert.Null(transport.Requests[1].Headers.Get("Authorization"));
    }

    [Fact]
    public async Task The_policy_is_safe_under_concurrent_calls()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.Basic), AllCredentials());
        using var transport = new ScriptedTransport(Enumerable.Range(0, 32).Select(_ => (object)(Func<Response>)(() => TestResponses.Create(Status.Ok))));
        var pipeline = Pipeline(policy, transport);

        var responses = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => SendAsync(pipeline), Ct)));

        Assert.All(responses, r => r.Dispose());
        Assert.All(transport.Requests, r => Assert.StartsWith("Basic ", r.Headers.Get("Authorization"), StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_rejects_null_and_a_descriptor_no_credential_can_serve()
    {
        Assert.Throws<ArgumentNullException>(() => new MultiSchemeAuthPolicy(null!, new AuthCredentials()));
        Assert.Throws<ArgumentNullException>(() => new MultiSchemeAuthPolicy(Of(AuthScheme.Basic), null!));
        Assert.Throws<ArgumentException>(() => new MultiSchemeAuthPolicy(Of(AuthScheme.Basic), new AuthCredentials()));
        Assert.NotNull(new MultiSchemeAuthPolicy(new AuthDescriptor(AuthRequirement.NoAuth), new AuthCredentials()));
    }

    [Fact]
    public async Task The_guard_refuses_http()
    {
        var policy = new MultiSchemeAuthPolicy(Of(AuthScheme.Basic), AllCredentials());
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var pipeline = Pipeline(policy, transport);

        await Assert.ThrowsAsync<HttpsRequiredException>(
            () => pipeline.SendAsync(Request.Get("http://api.example.com/"), RequestOptions.Empty, Ct).AsTask());

        Assert.Equal(0, transport.CallCount);
    }

    private sealed class SequenceTokens : TokenCredential
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public IReadOnlyList<string> LastScopes { get; private set; } = [];

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
        {
            LastScopes = context.Scopes;
            return new ValueTask<AccessToken>(new AccessToken("tok-" + Interlocked.Increment(ref _calls)));
        }
    }
}
