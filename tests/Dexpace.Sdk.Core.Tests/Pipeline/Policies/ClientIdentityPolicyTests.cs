// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

public sealed class ClientIdentityPolicyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/items");

    private sealed class CapturingTransport : IAsyncHttpClient
    {
        public Request? LastRequest { get; private set; }

        public Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new Response(Status.Ok));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // Stage
    // -------------------------------------------------------------------------

    [Fact]
    public void Stage_IsPerCall()
    {
        var policy = new ClientIdentityPolicy();
        Assert.Equal(PipelineStage.PerCall, policy.Stage);
    }

    // -------------------------------------------------------------------------
    // User-Agent stamping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_SetsUserAgentFromOptions()
    {
        var options = new DexpaceClientOptions { UserAgent = "my-client/1.0" };
        var transport = new CapturingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ClientIdentityPolicy())
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), options);

        var ua = transport.LastRequest!.Headers.Get("User-Agent");
        Assert.Equal("my-client/1.0", ua);
    }

    [Fact]
    public async Task ProcessAsync_ReplacesExistingUserAgentHeader()
    {
        var options = new DexpaceClientOptions { UserAgent = "override-agent/2.0" };

        // Request already carries an old User-Agent
        var request = MakeRequest() with
        {
            Headers = Headers.Empty.Set("User-Agent", "old-agent/0.1")
        };

        var transport = new CapturingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ClientIdentityPolicy())
            .Build(transport);

        await pipeline.SendAsync(request, options);

        // Must be replaced, not appended
        var values = transport.LastRequest!.Headers.GetAll("User-Agent");
        Assert.Single(values);
        Assert.Equal("override-agent/2.0", values[0]);
    }

    [Fact]
    public async Task ProcessAsync_UsesDefaultUserAgent_WhenOptionsIsDefault()
    {
        var options = new DexpaceClientOptions();   // default UA: dexpace-dotnet/<version>
        var transport = new CapturingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ClientIdentityPolicy())
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), options);

        var ua = transport.LastRequest!.Headers.Get("User-Agent");
        Assert.NotNull(ua);
        Assert.StartsWith("dexpace-dotnet/", ua, StringComparison.Ordinal);
    }
}
