// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests.Security;

/// <summary>
/// Roadmap phase 1, S3, row 9 (TRANSPORT-1, REDIR-7, REDIR-8, REDIR-9, XCUT-17; P9-24): the DI package configures each named client's primary handler
/// with <c>AllowAutoRedirect = false</c>, so a 3xx comes back to the SDK's <c>RedirectPolicy</c> and no credential crosses an origin on the runtime's
/// say-so. Two loopback servers on different ports are two origins; every assertion reads the raw bytes each one received. Permanent (roadmap constraint 5):
/// never delete or loosen it. The phase-1 classes (<c>RedirectWireTests</c> and the like) are untouched.
/// </summary>
[Trait("Category", "Security")]
public sealed class NamedClientRedirectWireTests
{
    private static readonly string[] s_credentialHeaders = ["authorization", "cookie", "proxy-authorization"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Request WithCredentials(Uri url) =>
        Request.Create(
            Method.Get,
            url.AbsoluteUri,
            new Headers.Builder()
                .Add("Authorization", "Bearer seed-secret")
                .Add("Cookie", "session=seed-secret")
                .Add("Proxy-Authorization", "Basic c2VlZDpzZWNyZXQ=")
                .Add("X-Pass", "kept")
                .Build());

    private static void AssertCarriesNoCredential(RecordedRequest request)
    {
        foreach (var name in s_credentialHeaders)
        {
            Assert.Empty(request.HeaderValues(name));
        }

        Assert.DoesNotContain("seed-secret", request.RawText, StringComparison.Ordinal);
        Assert.DoesNotContain("c2VlZDpzZWNyZXQ=", request.RawText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_named_client_returns_the_3xx_and_never_contacts_the_target()
    {
        await using var target = LoopbackServer.Start(LoopbackResponse.Ok("followed"));
        await using var origin = LoopbackServer.Start(LoopbackResponse.Redirect(302, target.Url("/landing").AbsoluteUri));
        var services = new ServiceCollection();
        _ = TestClients.Add(services, "kit");
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredKeyedService<IAsyncHttpClient>("kit");

        await using var response = await transport.ExecuteAsync(WithCredentials(origin.Url("/start")), Ct);

        Assert.Equal(302, response.Status.Code);
        Assert.Equal(target.Url("/landing").AbsoluteUri, response.Headers.Get("Location"));
        Assert.Single(origin.Requests);
        Assert.Empty(target.Requests);
    }

    [Fact]
    public async Task Through_the_pipeline_a_cross_origin_hop_carries_no_credential_and_a_same_origin_hop_loses_authorization()
    {
        await using var target = LoopbackServer.Start(LoopbackResponse.Ok("landed"));
        await using var origin = LoopbackServer.Start(LoopbackResponse.Redirect(302, target.Url("/landing").AbsoluteUri));
        await using var same = LoopbackServer.Start(LoopbackResponse.Redirect(302, "/next"), LoopbackResponse.Ok("next"));
        var services = new ServiceCollection();
        _ = TestClients.Add(services, "kit", ("Redirect:MaxRedirects", "3"), ("Redirect:AllowedMethods", "GET"));
        await using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredKeyedService<HttpPipeline>("kit");

        using var cross = await pipeline.ExecuteAsync(WithCredentials(origin.Url("/start")), Ct);
        using var inside = await pipeline.ExecuteAsync(WithCredentials(same.Url("/start")), Ct);

        Assert.Equal(200, cross.Status.Code);
        Assert.Equal("Bearer seed-secret", Assert.Single(origin.Requests).Header("Authorization"));
        var hop = Assert.Single(target.Requests);
        Assert.Equal("GET /landing HTTP/1.1", hop.RequestLine);
        AssertCarriesNoCredential(hop);
        Assert.Equal("kept", hop.Header("X-Pass"));

        Assert.Equal(200, inside.Status.Code);
        var sameHop = same.Requests[1];
        Assert.Empty(sameHop.HeaderValues("Authorization"));
        Assert.Equal("session=seed-secret", sameHop.Header("Cookie"));
    }

    [Fact]
    public async Task A_substituted_redirect_following_primary_fails_the_resolution_and_contacts_neither_origin()
    {
        await using var target = LoopbackServer.Start(LoopbackResponse.Ok("followed"));
        await using var origin = LoopbackServer.Start(LoopbackResponse.Redirect(302, target.Url("/landing").AbsoluteUri));
        var services = new ServiceCollection();
        var builder = TestClients.Add(services, "kit");
        _ = builder.HttpClient.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = true });
        await using var provider = services.BuildServiceProvider();

        var failure = Assert.Throws<ArgumentException>(() => provider.GetRequiredKeyedService<IAsyncHttpClient>("kit"));

        Assert.Contains("AllowAutoRedirect", failure.Message, StringComparison.Ordinal);
        Assert.Empty(origin.Requests);
        Assert.Empty(target.Requests);
    }
}
