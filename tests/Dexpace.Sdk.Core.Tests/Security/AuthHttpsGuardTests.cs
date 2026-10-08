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
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S4 (AUTH-28, XCUT-16; design §6.3, §11 item 25): a credential is never stamped over a
/// non-HTTPS URL. The guard runs before any credential resolution, raises a non-retryable
/// <see cref="SdkException"/> naming the policy and the scheme, has no loopback exemption, and is skipped on a
/// credential-free cross-origin hop (AUTH-29). Permanent (roadmap constraint 5); phase 6c owns the rework.
/// </summary>
[Trait("Category", "Security")]
public sealed class AuthHttpsGuardTests
{
    private static readonly DexpaceClientOptions s_options = new()
    {
        Retry = new RetryOptions { BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) },
        Redirect = new RedirectOptions { AllowHttpsToHttpDowngrade = true },
    };

    [Theory]
    [InlineData("http://api.example.com/v1/items")]
    [InlineData("HTTP://api.example.com/v1/items")]
    [InlineData("http://localhost:5000/v1/items")]
    [InlineData("http://127.0.0.1:5000/v1/items")]
    [InlineData("http://[::1]:5000/v1/items")]
    public async Task Basic_auth_over_plain_http_is_rejected_before_anything_is_sent(string url)
    {
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BasicAuthPolicy(new BasicCredential("u", "p")))
            .Build(transport);

        var ex = await Assert.ThrowsAsync<HttpsRequiredException>(
            () => pipeline.SendAsync(Request.Get(url), s_options, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(typeof(HttpsRequiredException), ex.GetType());
        Assert.Equal("http", ex.Scheme);
        Assert.Contains(nameof(BasicAuthPolicy), ex.Message, StringComparison.Ordinal);
        Assert.Contains("'http'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task The_guard_runs_before_the_credential_is_resolved()
    {
        var policy = new CountingAuthPolicy();
        var pipeline = new PipelineBuilder().Add(policy).Build(new RecordingTransport());

        await Assert.ThrowsAsync<HttpsRequiredException>(
            () => pipeline.SendAsync(Request.Get("http://api.example.com/"), s_options, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(0, policy.CredentialCalls);
    }

    [Fact]
    public async Task A_bearer_token_is_never_fetched_for_a_plain_http_url()
    {
        var credential = new CountingTokenCredential();
        var pipeline = new PipelineBuilder()
            .Add(new BearerTokenAuthPolicy(credential, "https://api.example.com/.default"))
            .Build(new RecordingTransport());

        await Assert.ThrowsAsync<HttpsRequiredException>(
            () => pipeline.SendAsync(Request.Get("http://api.example.com/"), s_options, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(0, credential.Calls);
    }

    [Fact]
    public async Task The_rejection_is_not_retried()
    {
        var scenarioRequest = Request.Get("http://api.example.com/");
        // The verified defect: 503 then 200 to an http:// URL stamped "Authorization: Basic" on both attempts.
        var transport = new ScriptedTransport(TestResponses.Create(Status.ServiceUnavailable, scenarioRequest), TestResponses.Create(Status.Ok, scenarioRequest));
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Add(new BasicAuthPolicy(new BasicCredential("u", "p")))
            .Build(transport);

        await Assert.ThrowsAsync<HttpsRequiredException>(
            () => pipeline.SendAsync(scenarioRequest, s_options, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task Https_is_stamped_as_before()
    {
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new BasicAuthPolicy(new BasicCredential("u", "p")))
            .Build(transport);

        using var response = await pipeline.SendAsync(
            Request.Get("HTTPS://api.example.com/"), s_options, TestContext.Current.CancellationToken);

        Assert.StartsWith("Basic ", transport.LastRequest!.Headers.Get("Authorization"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_credential_free_cross_origin_downgrade_hop_skips_the_guard()
    {
        var scenarioRequest = Request.Get("https://api.example.com/start");
        // AUTH-29 / XCUT-16's carve-out: the permitted https -> http hop carries no credential, so it proceeds.
        var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "http://other.example.org/landing", scenarioRequest),
            TestResponses.Create(Status.Ok, scenarioRequest));
        var pipeline = new PipelineBuilder()
            .Add(new RedirectPolicy())
            .Add(new BasicAuthPolicy(new BasicCredential("u", "p")))
            .Build(transport);

        using var response = await pipeline.SendAsync(
            scenarioRequest, s_options, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(2, transport.CallCount);
        Assert.NotNull(transport.Requests[0].Headers.Get("Authorization"));
        Assert.Null(transport.Requests[1].Headers.Get("Authorization"));
    }

    private sealed class CountingAuthPolicy() : AuthorizationPolicy(
        new AuthDescriptor(new AuthRequirement(AuthScheme.ApiKey)),
        [AuthScheme.ApiKey])
    {
        public int CredentialCalls { get; private set; }

        protected override IReadOnlyList<HttpHeaderName> WithheldHeaderNames { get; } = [HttpHeaderName.WellKnown.Authorization];

        protected override ValueTask<(string HeaderName, string HeaderValue)?> GetCredentialAsync(
            AuthRequirement requirement,
            Request request,
            PipelineContext context)
        {
            CredentialCalls++;
            return new(("Authorization", "Secret"));
        }

        protected override (string HeaderName, string HeaderValue)? GetCredential(
            AuthRequirement requirement,
            Request request,
            PipelineContext context)
        {
            CredentialCalls++;
            return ("Authorization", "Secret");
        }
    }

    private sealed class CountingTokenCredential : TokenCredential
    {
        public int Calls { get; private set; }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default)
        {
            Calls++;
            return new(new AccessToken("token", DateTimeOffset.MaxValue));
        }
    }
}
