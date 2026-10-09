// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Time;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Security;

/// <summary>
/// Phase 6b's wire half of the credential-leak convergence test (P6b-25; TRANSPORT-1, REDIR-7, REDIR-9, REDIR-10): the
/// scenario "cross-origin hop, then a retry" over two loopback servers (two ports, so two origins) and the SDK-managed
/// transport, reading the raw bytes the second server received on <em>both</em> attempts. The loopback is plain http, so no
/// auth policy runs here (it would refuse a credential over http, AUTH-28): this proves the caller-set half on the wire,
/// and <c>Dexpace.Sdk.Core.Tests.Security.RedirectCredentialLeakTests</c> proves the stamped half. Permanent (roadmap
/// constraint 5).
/// </summary>
[Trait("Category", "Security")]
public sealed class RedirectCredentialLeakWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Request WithCredentials(Uri url) =>
        Request.Create(
            Method.Get,
            url.AbsoluteUri,
            new Headers.Builder()
                .Add("Authorization", "Bearer seed-secret")
                .Add("Cookie", "session=seed-secret")
                .Add("Proxy-Authorization", "Basic c2VlZDpzZWNyZXQ=")
                .Build());

    private static SystemNetHttpClient OwnedTransport() => new(handler => handler.UseProxy = false);

    private static HttpPipeline Pipeline(SystemNetHttpClient transport) =>
        new PipelineBuilder().Add(new RedirectPolicy()).Add(new RetryPolicy(new InstantTimeProvider())).Build(transport);

    [Fact]
    public async Task A_cross_origin_hop_then_a_retry_carries_no_credential_on_either_attempt()
    {
        await using var foreign = LoopbackServer.Start(LoopbackResponse.Status(503, "Service Unavailable"), LoopbackResponse.Ok("landed"));
        await using var origin = LoopbackServer.Start(LoopbackResponse.Redirect(302, foreign.Url("/landing").AbsoluteUri));
        await using var transport = OwnedTransport();

        using var response = await Pipeline(transport).SendAsync(WithCredentials(origin.Url("/start")), new DexpaceClientOptions(), Ct);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal("Bearer seed-secret", Assert.Single(origin.Requests).Header("Authorization"));
        Assert.Equal(2, foreign.Requests.Count);
        foreach (var attempt in foreign.Requests)
        {
            Assert.Equal("GET /landing HTTP/1.1", attempt.RequestLine);
            Assert.Empty(attempt.HeaderValues("authorization"));
            Assert.Empty(attempt.HeaderValues("cookie"));
            Assert.Empty(attempt.HeaderValues("proxy-authorization"));
            Assert.DoesNotContain("seed-secret", attempt.RawText, StringComparison.Ordinal);
            Assert.DoesNotContain("c2VlZDpzZWNyZXQ=", attempt.RawText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task A_same_origin_hop_keeps_cookie()
    {
        await using var origin = LoopbackServer.Start(
            LoopbackResponse.Redirect(302, "/next"),
            LoopbackResponse.Status(503, "Service Unavailable"),
            LoopbackResponse.Ok("next"));
        await using var transport = OwnedTransport();

        using var response = await Pipeline(transport).SendAsync(WithCredentials(origin.Url("/start")), new DexpaceClientOptions(), Ct);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(3, origin.Requests.Count);
        foreach (var hop in origin.Requests.Skip(1))
        {
            Assert.Equal("session=seed-secret", hop.Header("Cookie"));
            Assert.Equal("Basic c2VlZDpzZWNyZXQ=", hop.Header("Proxy-Authorization"));
            Assert.Empty(hop.HeaderValues("authorization"));
        }
    }
}
