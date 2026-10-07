// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
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
public sealed class BasicAuthPolicyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Request MakeRequest(string url = "https://api.example.com/v1/items")
        => Request.Get(url);

    private static DexpaceClientOptions MakeOptions() => new();

    private static string Base64(string user, string pass)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));

    // -------------------------------------------------------------------------
    // Stage
    // -------------------------------------------------------------------------

    [Fact]
    public void Stage_IsAuth()
    {
        var policy = new BasicAuthPolicy(new BasicCredential("user", "pass"));
        Assert.Equal(PipelineStage.Auth, policy.Stage);
    }

    // -------------------------------------------------------------------------
    // Header stamping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_StampsBasicAuthorizationHeader()
    {
        var credential = new BasicCredential("alice", "s3cr3t");
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BasicAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        var value = transport.LastRequest!.Headers.Get("Authorization");
        Assert.Equal($"Basic {Base64("alice", "s3cr3t")}", value);
    }

    [Fact]
    public async Task ProcessAsync_EmptyPassword_StampsCorrectly()
    {
        var credential = new BasicCredential("user", string.Empty);
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BasicAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        var value = transport.LastRequest!.Headers.Get("Authorization");
        Assert.Equal($"Basic {Base64("user", "")}", value);
    }

    [Fact]
    public async Task ProcessAsync_ReplacesExistingAuthorizationHeader()
    {
        var credential = new BasicCredential("bob", "hunter2");
        var request = MakeRequest().WithHeaders(Headers.Empty.Set("Authorization", "Bearer old-token"));

        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BasicAuthPolicy(credential))
            .Build(transport);

        await pipeline.SendAsync(request, MakeOptions(), TestContext.Current.CancellationToken);

        var values = transport.LastRequest!.Headers.GetAll("Authorization");
        Assert.Single(values);
        Assert.StartsWith("Basic ", values[0], StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------
    // Cross-origin withholding
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_WithholdsCredential()
    {
        var policy = new BasicAuthPolicy(new BasicCredential("user", "pass"));
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        // Same origin as the seed: stamps.
        var first = await TestContexts.SentAsync(policy, context.SeedRequest, context);
        Assert.NotNull(first.Headers.Get("Authorization"));

        // Cross-origin hop: the credential must be withheld.
        var foreign = await TestContexts.SentAsync(
            policy, MakeRequest("https://other-service.example.org/callback").WithHeaders(Headers.Empty), context);
        Assert.Null(foreign.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task ProcessAsync_SameOriginRerun_StampsCredentialAgain()
    {
        var policy = new BasicAuthPolicy(new BasicCredential("user", "pass"));
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var first = await TestContexts.SentAsync(policy, context.SeedRequest, context);
        Assert.NotNull(first.Headers.Get("Authorization"));

        // Same origin: must stamp again.
        var second = await TestContexts.SentAsync(policy, first.WithHeaders(Headers.Empty), context);
        Assert.StartsWith("Basic ", second.Headers.Get("Authorization"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_CrossOriginRequest_StripsStaleAuthorizationHeader()
    {
        // The request carries a stale Authorization header from the original hop; on a cross-origin hop it must be
        // absent, even without RedirectPolicy in the pipeline.
        var policy = new BasicAuthPolicy(new BasicCredential("user", "pass"));
        var context = TestContexts.For(MakeRequest("https://api.example.com/v1/resource"));

        var foreign = await TestContexts.SentAsync(
            policy,
            MakeRequest("https://other-service.example.org/callback")
                .WithHeaders(Headers.Empty.Set("Authorization", $"Basic {Base64("user", "pass")}")),
            context);

        Assert.Null(foreign.Headers.Get("Authorization"));
    }

    [Fact]
    public void Process_sync_stamps_like_ProcessAsync()
    {
        var policy = new BasicAuthPolicy(new BasicCredential("alice", "s3cr3t"));
        var context = TestContexts.For();

        var sent = TestContexts.Sent(policy, context.SeedRequest, context);

        Assert.Equal($"Basic {Base64("alice", "s3cr3t")}", sent.Headers.Get("Authorization"));
    }
}
