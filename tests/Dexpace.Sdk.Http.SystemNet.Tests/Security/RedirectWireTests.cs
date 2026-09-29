// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Http.SystemNet.Tests.Loopback;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Security;

/// <summary>
/// Roadmap phase 1, S3 at the wire (TRANSPORT-1, REDIR-7, REDIR-8, REDIR-9, XCUT-17(a)–(b); design §3.2, §6.2). The
/// verified defect: <c>new SystemNetHttpClient()</c> built an <c>HttpClient</c> that followed redirects itself, so a
/// caller-set <c>Cookie</c>, <c>Proxy-Authorization</c> and custom header reached a second origin and
/// <see cref="RedirectPolicy"/> never saw a 3xx. Two loopback servers on different ports are two origins; every
/// assertion reads the raw bytes each one received. Permanent (roadmap constraint 5); phases 6b (redirect rewrite), 8b
/// (handler rules) and 9 (DI-configured primary handler) own the structural form. REDIR-12's userinfo drop is not
/// visible on the wire (<c>HttpClient</c> never sends userinfo as a header), so it is proven on the model in
/// <c>Dexpace.Sdk.Core.Tests.Security.RedirectCredentialHygieneTests</c>.
/// </summary>
[Trait("Category", "Security")]
public sealed class RedirectWireTests
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

    // The SDK-owned construction (the redirect-refusing handler this class is about), with the proxy pinned off so a
    // proxy in the environment (HTTP_PROXY, HTTPS_PROXY) cannot route the loopback exchange elsewhere. Per instance,
    // so it is safe under parallel test execution.
    private static SystemNetHttpClient OwnedTransport() => new(handler => handler.UseProxy = false);

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
    public async Task The_sdk_managed_transport_returns_the_3xx_and_never_contacts_the_target()
    {
        await using var target = LoopbackServer.Start(LoopbackResponse.Ok("followed"));
        await using var origin = LoopbackServer.Start(LoopbackResponse.Redirect(302, target.Url("/landing").AbsoluteUri));
        await using var transport = OwnedTransport();

        await using var response = await transport.ExecuteAsync(WithCredentials(origin.Url("/start")), Ct);

        Assert.Equal(302, response.Status.Code);
        Assert.Equal(target.Url("/landing").AbsoluteUri, response.Headers.Get("Location"));
        Assert.Single(origin.Requests);
        Assert.Empty(target.Requests);
    }

    [Fact]
    public async Task A_borrowed_client_that_followed_a_redirect_fails_loudly()
    {
        await using var target = LoopbackServer.Start(LoopbackResponse.Ok("followed"));
        await using var origin = LoopbackServer.Start(LoopbackResponse.Redirect(302, target.Url("/landing").AbsoluteUri));
        using var client = new SystemHttpClient(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = true });
        await using var transport = new SystemNetHttpClient(client);

        var error = await Record.ExceptionAsync(async () =>
        {
            await using var response = await transport.ExecuteAsync(Request.Get(origin.Url("/start").AbsoluteUri), Ct);
        });

        var failure = Assert.IsType<SdkException>(error);
        Assert.Contains("AllowAutoRedirect", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(target.BaseUri.Authority, failure.Message, StringComparison.Ordinal);

        // The borrowed client is the caller's: failing the call does not dispose it.
        using var stillUsable = await client.GetAsync(target.Url("/after"), Ct);
    }

    [Fact]
    public async Task A_borrowed_client_whose_handler_only_rewrites_the_query_is_not_mistaken_for_a_redirect()
    {
        // A corporate DelegatingHandler that appends api-version changes RequestMessage.RequestUri without any redirect.
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("plain"));
        using var client = new SystemHttpClient(new AppendQueryHandler
        {
            InnerHandler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false },
        });
        await using var transport = new SystemNetHttpClient(client);

        await using var response = await transport.ExecuteAsync(Request.Get(server.Url("/plain#frag").AbsoluteUri), Ct);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal("plain", await response.Body.ReadAsStringAsync(Ct));
        Assert.Equal("GET /plain?api-version=2026-09-29 HTTP/1.1", Assert.Single(server.Requests).RequestLine);
    }

    [Fact]
    public async Task A_cross_origin_hop_reaches_the_target_without_any_credential()
    {
        await using var target = LoopbackServer.Start(LoopbackResponse.Ok("landed"));
        await using var origin = LoopbackServer.Start(LoopbackResponse.Redirect(302, target.Url("/landing").AbsoluteUri));
        await using var transport = OwnedTransport();
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(WithCredentials(origin.Url("/start")), new DexpaceClientOptions(), Ct);

        Assert.Equal(Status.Ok, response.Status);
        var seed = Assert.Single(origin.Requests);
        Assert.Equal("Bearer seed-secret", seed.Header("Authorization"));
        var hop = Assert.Single(target.Requests);
        Assert.Equal("GET /landing HTTP/1.1", hop.RequestLine);
        AssertCarriesNoCredential(hop);
        Assert.Equal("kept", hop.Header("X-Pass"));
    }

    [Fact]
    public async Task A_hop_back_to_the_seed_origin_after_a_foreign_hop_carries_no_credential()
    {
        // A -> B -> A. Against the seed, the last hop is same-origin again; what the foreign hop stripped stays stripped.
        var originScript = new Queue<LoopbackResponse>();
        await using var origin = LoopbackServer.Start(_ => originScript.Dequeue());
        await using var foreign = LoopbackServer.Start(LoopbackResponse.Redirect(307, origin.Url("/back").AbsoluteUri));
        originScript.Enqueue(LoopbackResponse.Redirect(302, foreign.Url("/away").AbsoluteUri));
        originScript.Enqueue(LoopbackResponse.Ok("home"));
        await using var transport = OwnedTransport();
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(WithCredentials(origin.Url("/start")), new DexpaceClientOptions(), Ct);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Collection(
            origin.Requests,
            seed => Assert.Equal("session=seed-secret", seed.Header("Cookie")),
            back =>
            {
                Assert.Equal("GET /back HTTP/1.1", back.RequestLine);
                AssertCarriesNoCredential(back);
            });
        AssertCarriesNoCredential(Assert.Single(foreign.Requests));
    }

    [Fact]
    public async Task A_same_origin_hop_drops_authorization_and_keeps_the_cookie()
    {
        await using var origin = LoopbackServer.Start(
            LoopbackResponse.Redirect(302, "/next"),
            LoopbackResponse.Ok("next"));
        await using var transport = OwnedTransport();
        var pipeline = new PipelineBuilder().Add(new RedirectPolicy()).Build(transport);

        using var response = await pipeline.SendAsync(WithCredentials(origin.Url("/start")), new DexpaceClientOptions(), Ct);

        Assert.Equal(Status.Ok, response.Status);
        var hop = origin.Requests[1];
        Assert.Equal("GET /next HTTP/1.1", hop.RequestLine);
        Assert.Empty(hop.HeaderValues("Authorization"));
        Assert.Equal("session=seed-secret", hop.Header("Cookie"));
        Assert.Equal("Basic c2VlZDpzZWNyZXQ=", hop.Header("Proxy-Authorization"));
    }

    private sealed class AppendQueryHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var builder = new UriBuilder(request.RequestUri!) { Query = "api-version=2026-09-29", Fragment = string.Empty };
            request.RequestUri = builder.Uri;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
