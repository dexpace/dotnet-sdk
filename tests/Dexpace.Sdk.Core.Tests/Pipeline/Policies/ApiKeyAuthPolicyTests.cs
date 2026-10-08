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
public sealed class ApiKeyAuthPolicyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Request MakeRequest(string url = "https://api.example.com/v1/items")
        => Request.Get(url);

    private static DexpaceClientOptions MakeOptions() => new();

    // -------------------------------------------------------------------------
    // Stage
    // -------------------------------------------------------------------------

    [Fact]
    public void Stage_IsAuth()
    {
        var policy = new ApiKeyAuthPolicy(new ApiKeyCredential("key123"));
        Assert.Equal(PipelineStage.Auth, policy.Stage);
    }

    // -------------------------------------------------------------------------
    // Header stamping — no scheme
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_NoScheme_StampsKeyAsEntireHeaderValue()
    {
        var credential = new ApiKeyCredential("sk-test-abc");
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ApiKeyAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        var value = transport.LastRequest!.Headers.Get("Authorization");
        Assert.Equal("sk-test-abc", value);
    }

    [Fact]
    public async Task ProcessAsync_WithScheme_PrefixesSchemeBeforeKey()
    {
        var credential = new ApiKeyCredential("sk-test-abc", scheme: "Bearer");
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ApiKeyAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        var value = transport.LastRequest!.Headers.Get("Authorization");
        Assert.Equal("Bearer sk-test-abc", value);
    }

    // -------------------------------------------------------------------------
    // Custom header name
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_CustomHeader_StampsConfiguredHeader()
    {
        var xApiKey = HttpHeaderName.Of("X-Api-Key");
        var credential = new ApiKeyCredential("my-key", header: xApiKey);
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ApiKeyAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        var value = transport.LastRequest!.Headers.Get("X-Api-Key");
        Assert.Equal("my-key", value);
        // Authorization header must not be set
        Assert.Null(transport.LastRequest.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_CustomHeaderWithScheme_StampsCorrectValue()
    {
        var xApiKey = HttpHeaderName.Of("X-Api-Key");
        var credential = new ApiKeyCredential("my-key", header: xApiKey, scheme: "Token");
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ApiKeyAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        var value = transport.LastRequest!.Headers.Get("X-Api-Key");
        Assert.Equal("Token my-key", value);
    }

    // -------------------------------------------------------------------------
    // Replaces pre-existing header value
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_ReplacesExistingAuthorizationHeader()
    {
        var credential = new ApiKeyCredential("new-key");
        var request = MakeRequest().WithHeaders(Headers.Empty.Set("Authorization", "old-value"));

        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ApiKeyAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(request, MakeOptions(), TestContext.Current.CancellationToken);

        var values = transport.LastRequest!.Headers.GetAll("Authorization");
        Assert.Single(values);
        Assert.Equal("new-key", values[0]);
    }

    // -------------------------------------------------------------------------
    // Cross-origin withholding
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_SameOriginAfterRedirect_StampsCredential()
    {
        // Simulate the pipeline being called twice on contexts with same origin.
        var credential = new ApiKeyCredential("sk-secret", scheme: "Bearer");
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ApiKeyAuthPolicy(credential))
            .Build(transport);

        // Two independent calls — each gets a fresh PipelineContext, same origin.
        await pipeline.SendAsync(MakeRequest("https://api.example.com/v1/a"), MakeOptions(), TestContext.Current.CancellationToken);
        var firstAuth = transport.LastRequest!.Headers.Get("Authorization");

        await pipeline.SendAsync(MakeRequest("https://api.example.com/v1/b"), MakeOptions(), TestContext.Current.CancellationToken);
        var secondAuth = transport.LastRequest!.Headers.Get("Authorization");

        Assert.Equal("Bearer sk-secret", firstAuth);
        Assert.Equal("Bearer sk-secret", secondAuth);
    }

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_WithholdsCredential()
    {
        var policy = new ApiKeyAuthPolicy(new ApiKeyCredential("sk-secret", scheme: "Bearer"));
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var first = await TestContexts.SentAsync(policy, context.SeedRequest, context);
        Assert.Equal("Bearer sk-secret", first.Headers.Get("Authorization"));

        // A hop to a different origin: the credential must be withheld.
        var foreign = await TestContexts.SentAsync(
            policy, MakeRequest("https://other-service.example.org/callback").WithHeaders(Headers.Empty), context);

        Assert.Null(foreign.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_SameOriginRerun_StampsCredentialAgain()
    {
        var policy = new ApiKeyAuthPolicy(new ApiKeyCredential("retry-key"));
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var first = await TestContexts.SentAsync(policy, context.SeedRequest, context);
        Assert.Equal("retry-key", first.Headers.Get("Authorization"));

        var second = await TestContexts.SentAsync(policy, first.WithHeaders(Headers.Empty), context);
        Assert.Equal("retry-key", second.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_StripsStalCredentialHeader()
    {
        // A stale Authorization header (RedirectPolicy was NOT in the pipeline) must be absent on a cross-origin hop.
        var policy = new ApiKeyAuthPolicy(new ApiKeyCredential("sk-secret", scheme: "Bearer"));
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var foreign = await TestContexts.SentAsync(
            policy,
            MakeRequest("https://other-service.example.org/callback")
                .WithHeaders(Headers.Empty.Set("Authorization", "Bearer sk-secret")),
            context);

        Assert.Null(foreign.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_StripsStaleCustomHeader()
    {
        // Same defense-in-depth check for a custom header (X-Api-Key) on cross-origin.
        var xApiKey = HttpHeaderName.Of("X-Api-Key");
        var policy = new ApiKeyAuthPolicy(new ApiKeyCredential("my-key", header: xApiKey));
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var foreign = await TestContexts.SentAsync(
            policy,
            MakeRequest("https://other-service.example.org/callback").WithHeaders(Headers.Empty.Set("X-Api-Key", "my-key")),
            context);

        Assert.Null(foreign.Headers.Get("X-Api-Key"));
    }

    [Fact]
    public void Process_sync_stamps_like_ProcessAsync()
    {
        var policy = new ApiKeyAuthPolicy(new ApiKeyCredential("sk-secret", scheme: "Bearer"));
        var context = TestContexts.For();

        var sent = TestContexts.Sent(policy, context.SeedRequest, context);

        Assert.Equal("Bearer sk-secret", sent.Headers.Get("Authorization"));
    }

    [Fact]
    public void The_header_value_is_stamped_from_the_credential_computed_once()
    {
        var credential = new ApiKeyCredential("k-1", HttpHeaderName.Of("X-Api-Key"), "SharedAccessKey");
        var policy = new ApiKeyAuthPolicy(credential);
        var context = TestContexts.For();

        var first = TestContexts.Sent(policy, context.SeedRequest, context);
        var second = TestContexts.Sent(policy, context.SeedRequest, context);

        Assert.Equal("SharedAccessKey k-1", first.Headers.Get("X-Api-Key"));
        Assert.Equal(credential.HeaderValue, second.Headers.Get("X-Api-Key"));
    }

    [Fact]
    public async Task A_custom_header_is_withheld_cross_origin()
    {
        var policy = new ApiKeyAuthPolicy(new ApiKeyCredential("my-key", HttpHeaderName.Of("X-Api-Key")));
        var context = TestContexts.For(MakeRequest("https://api.example.com/"));

        var foreign = await TestContexts.SentAsync(
            policy,
            MakeRequest("https://other.example.org/").WithHeaders(Headers.Empty.Set("X-Api-Key", "stale").Set("Authorization", "keep")),
            context);

        Assert.Null(foreign.Headers.Get("X-Api-Key"));
        Assert.Equal("keep", foreign.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task A_per_call_NoAuth_sends_anonymously()
    {
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder().Add(new ApiKeyAuthPolicy(new ApiKeyCredential("k"))).Build(transport);

        using var response = await pipeline.SendAsync(
            MakeRequest(),
            new RequestOptions { Auth = new AuthDescriptor(AuthRequirement.NoAuth) },
            TestContext.Current.CancellationToken);

        Assert.Null(transport.LastRequest!.Headers.Get("Authorization"));
    }
}
