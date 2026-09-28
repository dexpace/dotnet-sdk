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
        var request = MakeRequest() with
        {
            Headers = Headers.Empty.Set("Authorization", "old-value")
        };

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
        // Drive the policy directly against a context whose Request has been redirected
        // to a different origin after the origin was first recorded.
        var credential = new ApiKeyCredential("sk-secret", scheme: "Bearer");
        var options = MakeOptions();

        // Context starts on the original origin.
        var originalRequest = MakeRequest("https://api.example.com/v1/resource");
        var context = new PipelineContext(originalRequest, options);

        // Record the original origin by running the policy once against a no-op continuation.
        var recordingTransport = new RecordingTransport();
        var recordingRunner = new PipelineRunner([], 0, recordingTransport);
        var policy = new ApiKeyAuthPolicy(credential);

        // First run: records origin + stamps header, then calls continuation (which hits transport).
        await policy.ProcessAsync(context, recordingRunner);
        Assert.Equal("Bearer sk-secret", context.Request.Headers.Get("Authorization"));

        // Now mutate the context to simulate a cross-origin redirect.
        context.Request = MakeRequest("https://other-service.example.org/callback");

        // Reset Authorization so we can observe whether it gets stamped.
        context.Request = context.Request with
        {
            Headers = Headers.Empty
        };

        var foreignTransport = new RecordingTransport();
        var foreignRunner = new PipelineRunner([], 0, foreignTransport);

        // Second run on same context: origin differs → credential must be withheld.
        await policy.ProcessAsync(context, foreignRunner);

        Assert.Null(context.Request.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_SameOriginRerun_StampsCredentialAgain()
    {
        // Same-origin retry: the policy should stamp on each run.
        var credential = new ApiKeyCredential("retry-key");
        var options = MakeOptions();
        var request = MakeRequest("https://api.example.com/v1/resource");
        var context = new PipelineContext(request, options);
        var transport = new RecordingTransport();
        var runner = new PipelineRunner([], 0, transport);
        var policy = new ApiKeyAuthPolicy(credential);

        // First invocation records origin and stamps.
        await policy.ProcessAsync(context, runner);
        Assert.Equal("retry-key", context.Request.Headers.Get("Authorization"));

        // Reset the header to confirm the second stamp.
        context.Request = context.Request with { Headers = Headers.Empty };

        // Second invocation on same origin: must stamp again.
        await policy.ProcessAsync(context, runner);
        Assert.Equal("retry-key", context.Request.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_StripsStalCredentialHeader()
    {
        // The request carries a stale Authorization header from the original hop.
        // After the policy runs on a cross-origin request, that header must be absent —
        // the foreign origin must never see it.
        var credential = new ApiKeyCredential("sk-secret", scheme: "Bearer");
        var options = MakeOptions();

        // Context starts on the original origin.
        var originalRequest = MakeRequest("https://api.example.com/v1/resource");
        var context = new PipelineContext(originalRequest, options);

        var recordingTransport = new RecordingTransport();
        var recordingRunner = new PipelineRunner([], 0, recordingTransport);
        var policy = new ApiKeyAuthPolicy(credential);

        // First run: records origin and stamps header.
        await policy.ProcessAsync(context, recordingRunner);
        Assert.Equal("Bearer sk-secret", context.Request.Headers.Get("Authorization"));

        // Simulate a cross-origin redirect where the credential header is still present
        // (i.e. RedirectPolicy was NOT in the pipeline).
        context.Request = MakeRequest("https://other-service.example.org/callback") with
        {
            Headers = Headers.Empty.Set("Authorization", "Bearer sk-secret")
        };

        var foreignTransport = new RecordingTransport();
        var foreignRunner = new PipelineRunner([], 0, foreignTransport);

        // Second run: different origin → stale header must be stripped.
        await policy.ProcessAsync(context, foreignRunner);

        Assert.Null(context.Request.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_StripsStaleCustomHeader()
    {
        // Same defense-in-depth check for a custom header (X-Api-Key) on cross-origin.
        var xApiKey = HttpHeaderName.Of("X-Api-Key");
        var credential = new ApiKeyCredential("my-key", header: xApiKey);
        var options = MakeOptions();

        var originalRequest = MakeRequest("https://api.example.com/v1/resource");
        var context = new PipelineContext(originalRequest, options);

        var recordingTransport = new RecordingTransport();
        var recordingRunner = new PipelineRunner([], 0, recordingTransport);
        var policy = new ApiKeyAuthPolicy(credential);

        // First run: records origin and stamps X-Api-Key.
        await policy.ProcessAsync(context, recordingRunner);
        Assert.Equal("my-key", context.Request.Headers.Get("X-Api-Key"));

        // Simulate cross-origin redirect with stale custom header still present.
        context.Request = MakeRequest("https://other-service.example.org/callback") with
        {
            Headers = Headers.Empty.Set("X-Api-Key", "my-key")
        };

        var foreignTransport = new RecordingTransport();
        var foreignRunner = new PipelineRunner([], 0, foreignTransport);

        // Second run: different origin → stale X-Api-Key header must be stripped.
        await policy.ProcessAsync(context, foreignRunner);

        Assert.Null(context.Request.Headers.Get("X-Api-Key"));
    }
}
