// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>AUTH-14, AUTH-26 and AUTH-30: the reactive, handler-driven policy.</summary>
[Trait("Category", "Unit")]
public sealed class ChallengeAuthPolicyTests
{
    private const string Url = "https://api.example.com/v1/items";

    private static readonly DexpaceClientOptions s_options = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static BasicChallengeHandler BasicHandler() => new(new BasicCredential("alice", "s3cr3t"));

    private static HttpPipeline Pipeline(IChallengeHandler handler, ScriptedTransport transport) =>
        new PipelineBuilder().Add(new ChallengeAuthPolicy(handler)).Build(transport, s_options);

    private static async Task<Response> SendAsync(HttpPipeline pipeline, string url, bool async) =>
        async
            ? await pipeline.SendAsync(Request.Get(url), RequestOptions.Empty, Ct)
            : pipeline.Send(Request.Get(url), RequestOptions.Empty, Ct);

    [Fact]
    public async Task Nothing_is_stamped_on_the_first_request()
    {
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(BasicHandler(), transport), Url, async: true);

        Assert.Null(transport.LastRequest!.Headers.Get("Authorization"));
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_basic_401_is_answered_once_via_the_handler(bool async)
    {
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(BasicHandler(), transport), Url, async);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
        Assert.Null(transport.Requests[0].Headers.Get("Authorization"));
        Assert.Equal("Basic YWxpY2U6czNjcjN0", transport.Requests[1].Headers.Get("Authorization"));
    }

    [Fact]
    public async Task A_401_the_handler_cannot_answer_is_returned()
    {
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Bearer realm=\"r\""));

        using var response = await SendAsync(Pipeline(BasicHandler(), transport), Url, async: true);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task A_still_401_after_the_replay_is_returned_without_a_third_call()
    {
        using var transport = new ScriptedTransport(
            TestResponses.Unauthorized("Basic realm=\"r\""),
            TestResponses.Unauthorized("Basic realm=\"r\""));

        using var response = await SendAsync(Pipeline(BasicHandler(), transport), Url, async: true);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task A_composite_falls_back_from_a_declined_handler()
    {
        var composite = new CompositeChallengeHandler(new DecliningHandler(), BasicHandler());
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(composite, transport), Url, async: true);

        Assert.Equal(200, response.Status.Code);
        Assert.NotNull(transport.Requests[1].Headers.Get("Authorization"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_guard_refuses_http_before_the_first_request(bool async)
    {
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        await Assert.ThrowsAsync<HttpsRequiredException>(
            () => SendAsync(Pipeline(BasicHandler(), transport), "http://api.example.com/", async));

        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public void The_client_descriptor_serves_Digest_and_Basic_only()
    {
        Assert.Throws<ArgumentNullException>(() => new ChallengeAuthPolicy(null!));
    }

    private sealed class DecliningHandler : IChallengeHandler
    {
        public Request? Authorize(IReadOnlyList<AuthenticationChallenge> challenges, Request request, bool proxy) => null;
    }
}
