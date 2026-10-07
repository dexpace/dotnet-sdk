// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

[Trait("Category", "Unit")]
public sealed class ClientIdentityPolicyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/items");

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
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ClientIdentityPolicy())
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken);

        var ua = transport.LastRequest!.Headers.Get("User-Agent");
        Assert.Equal("my-client/1.0", ua);
    }

    [Fact]
    public async Task A_caller_supplied_User_Agent_is_followed_by_the_sdk_line_by_default()
    {
        var options = new DexpaceClientOptions { UserAgent = "override-agent/2.0" };
        var request = MakeRequest().WithHeaders(Headers.Empty.Set("User-Agent", "old-agent/0.1"));
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder().Add(new ClientIdentityPolicy()).Build(transport);

        await pipeline.SendAsync(request, options, TestContext.Current.CancellationToken);

        var values = transport.LastRequest!.Headers.GetAll("User-Agent");
        Assert.Equal("old-agent/0.1 override-agent/2.0", Assert.Single(values));
    }

    [Fact]
    public async Task Replace_mode_overwrites_the_callers_value()
    {
        var options = new DexpaceClientOptions { UserAgent = "override-agent/2.0" };
        var request = MakeRequest().WithHeaders(Headers.Empty.Set("User-Agent", "old-agent/0.1"));
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder().Add(new ClientIdentityPolicy(ClientIdentityMode.Replace)).Build(transport);

        await pipeline.SendAsync(request, options, TestContext.Current.CancellationToken);

        Assert.Equal("override-agent/2.0", Assert.Single(transport.LastRequest!.Headers.GetAll("User-Agent")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_UserAgent_option_emits_no_header(string userAgent)
    {
        var options = new DexpaceClientOptions { UserAgent = userAgent };
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder().Add(new ClientIdentityPolicy()).Build(transport);

        await pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken);

        Assert.False(transport.LastRequest!.Headers.Contains("User-Agent"));
    }

    [Fact]
    public async Task A_per_call_options_value_wins_over_the_captured_one()
    {
        var options = new DexpaceClientOptions { UserAgent = "first/1" };
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder().Add(new ClientIdentityPolicy()).Build(transport);

        await pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken);
        await pipeline.SendAsync(MakeRequest(), options with { UserAgent = "second/2" }, TestContext.Current.CancellationToken);

        Assert.Equal("first/1", transport.Requests[0].Headers.Get("User-Agent"));
        Assert.Equal("second/2", transport.Requests[1].Headers.Get("User-Agent"));
    }

    [Fact]
    public async Task ProcessAsync_UsesDefaultUserAgent_WhenOptionsIsDefault()
    {
        var options = new DexpaceClientOptions();   // default UA: dexpace-dotnet/<version>
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ClientIdentityPolicy())
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken);

        var ua = transport.LastRequest!.Headers.Get("User-Agent");
        Assert.NotNull(ua);
        Assert.StartsWith("dexpace-dotnet/", ua, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_and_async_agree()
    {
        var options = new DexpaceClientOptions { UserAgent = "my-client/1.0" };
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder().Add(new ClientIdentityPolicy()).Build(transport);

        using var sync = pipeline.Send(MakeRequest(), options, TestContext.Current.CancellationToken);
        using var async = await pipeline.SendAsync(MakeRequest(), options, TestContext.Current.CancellationToken);

        Assert.Equal(transport.Requests[0].Headers.Get("User-Agent"), transport.Requests[1].Headers.Get("User-Agent"));
        Assert.Equal("my-client/1.0", transport.Requests[0].Headers.Get("User-Agent"));
    }
}
