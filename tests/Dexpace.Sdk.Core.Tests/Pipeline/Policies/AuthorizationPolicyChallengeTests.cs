// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Streams;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>
/// AUTH-4 to AUTH-6, AUTH-27 to AUTH-33 and AUTH-38: the authorization base's lifecycle, driven through a probe policy
/// over a scripted transport. Each sync/async pair is one theory over <c>async</c> (the Ruby port's pairs).
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthorizationPolicyChallengeTests
{
    private const string Url = "https://api.example.com/v1/items";

    private static readonly DexpaceClientOptions s_options = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Request Get(string url = Url) => Request.Get(url);

    private static async Task<Response> SendAsync(
        HttpPipeline pipeline,
        Request request,
        bool async,
        RequestOptions? options = null) =>
        async
            ? await pipeline.SendAsync(request, options ?? RequestOptions.Empty, Ct)
            : pipeline.Send(request, options ?? RequestOptions.Empty, Ct);

    private static HttpPipeline Pipeline(ProbePolicy policy, ScriptedTransport transport) =>
        new PipelineBuilder().Add(policy).Build(transport, s_options);

    // ---------------------------------------------------------------------------------------------------------------
    // Stage and order (AUTH-27)
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Stage_is_Auth_and_sealed()
    {
        var stage = typeof(AuthorizationPolicy).GetProperty(nameof(HttpPipelinePolicy.Stage))!.GetMethod!;

        Assert.True(stage.IsFinal);
        Assert.Equal(PipelineStage.Auth, new ProbePolicy().Stage);
        Assert.True(typeof(AuthorizationPolicy).GetMethod(nameof(HttpPipelinePolicy.ProcessAsync))!.IsFinal);
        Assert.True(typeof(AuthorizationPolicy).GetMethod(nameof(HttpPipelinePolicy.Process))!.IsFinal);
    }

    [Fact]
    public void The_stage_order_is_Redirect_then_Retry_then_Auth()
    {
        Assert.True(PipelineStage.Redirect < PipelineStage.Retry);
        Assert.True(PipelineStage.Retry < PipelineStage.Auth);
        Assert.Equal(200, (int)PipelineStage.Redirect);
        Assert.Equal(300, (int)PipelineStage.Retry);
        Assert.Equal(500, (int)PipelineStage.Auth);
    }

    [Fact]
    public void A_second_auth_policy_in_one_pipeline_is_rejected_by_the_builder()
    {
        var builder = new PipelineBuilder().Add(new ProbePolicy());

        Assert.Throws<InvalidOperationException>(() => builder.Add(new BasicAuthPolicy(new BasicCredential("u", "p"))));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Resolution wiring (AUTH-4 to AUTH-6)
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_per_call_NoAuth_sends_anonymously_and_returns_a_401_unchanged(bool async)
    {
        var probe = new ProbePolicy { Hook = _ => throw new InvalidOperationException("must not be called") };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""));
        var options = new RequestOptions { Auth = new AuthDescriptor(AuthRequirement.NoAuth) };

        // An http URL: the guard applies only where a credential will be attached.
        using var response = await SendAsync(Pipeline(probe, transport), Get("http://api.example.com/"), async, options);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
        Assert.Null(transport.LastRequest!.Headers.Get("Authorization"));
        Assert.Equal(0, probe.CredentialCalls);
        Assert.Equal(0, probe.ChallengeCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_per_call_scheme_the_policy_cannot_serve_throws_AuthResolutionException_before_sending(bool async)
    {
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var options = new RequestOptions { Auth = new AuthDescriptor(new AuthRequirement(AuthScheme.Digest)) };

        var ex = await Assert.ThrowsAsync<AuthResolutionException>(
            () => SendAsync(Pipeline(new ProbePolicy(), transport), Get(), async, options));

        Assert.Equal([AuthScheme.Digest], ex.Required);
        Assert.Equal([AuthScheme.ApiKey], ex.Available);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task The_operation_tier_is_used_when_no_per_call_tier_is_set()
    {
        var probe = new ProbePolicy();
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var operation = new RequestOptions { OperationAuth = new AuthDescriptor(AuthRequirement.NoAuth) };

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async: true, operation);

        Assert.Null(transport.LastRequest!.Headers.Get("Authorization"));
        Assert.Equal(0, probe.CredentialCalls);

        // The per-call tier beats the operation tier.
        var both = operation with { Auth = new AuthDescriptor(new AuthRequirement(AuthScheme.ApiKey)) };
        using var second = new ScriptedTransport(TestResponses.Create(Status.Ok));
        using var stamped = await SendAsync(Pipeline(probe, second), Get(), async: true, both);
        Assert.Equal("Probe-1", second.LastRequest!.Headers.Get("Authorization"));
    }

    [Fact]
    public async Task The_requirement_handed_to_the_credential_is_the_resolved_one()
    {
        AuthRequirement? seen = null;
        var probe = new ProbePolicy { Credential = (requirement, _) => { seen = requirement; return ("Authorization", "x"); } };
        var wanted = new AuthRequirement(AuthScheme.ApiKey) { Scopes = ["a"] };
        var options = new RequestOptions { Auth = new AuthDescriptor(wanted) };

        using var response = await SendAsync(Pipeline(probe, new ScriptedTransport(TestResponses.Create(Status.Ok))), Get(), async: true, options);

        Assert.Equal(wanted, seen);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Cross-origin (AUTH-29)
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_cross_origin_hop_has_its_credential_header_removed_and_is_not_guarded()
    {
        var probe = new ProbePolicy();
        var context = TestContexts.For(Get("https://a.example.com/"));
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var foreign = Get("http://b.example.com/")
            .WithHeaders(Headers.Empty.Set("Authorization", "stale").Set("X-Keep", "1"));

        using var response = await probe.ProcessAsync(foreign, context, TestContexts.RunnerOver(transport));

        Assert.Null(transport.LastRequest!.Headers.Get("Authorization"));
        Assert.Equal("1", transport.LastRequest.Headers.Get("X-Keep"));
        Assert.Equal(0, probe.CredentialCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_cross_origin_401_is_returned_without_challenge_handling(bool async)
    {
        var probe = new ProbePolicy { Hook = _ => throw new InvalidOperationException("must not be called") };
        var context = TestContexts.For(Get("https://a.example.com/"));
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\"", "Bearer realm=\"r\""));
        var runner = TestContexts.RunnerOver(transport);
        var foreign = Get("https://b.example.com/");

        using var response = async
            ? await probe.ProcessAsync(foreign, context, runner)
            : probe.Process(foreign, context, runner);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
        Assert.Equal(0, probe.ChallengeCalls);
    }

    [Fact]
    public async Task No_marker_property_is_set_on_the_context()
    {
        var probe = new ProbePolicy();
        var context = TestContexts.For(Get("https://a.example.com/"));
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok), TestResponses.Create(Status.Ok));

        using var same = await probe.ProcessAsync(Get("https://a.example.com/"), context, TestContexts.RunnerOver(transport));
        using var foreign = await probe.ProcessAsync(Get("https://b.example.com/"), context, TestContexts.RunnerOver(transport));

        var properties = typeof(CallState).GetField("_properties", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(context.State);
        Assert.Null(properties);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Guard (AUTH-28)
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_non_https_url_throws_HttpsRequiredException_before_any_credential_is_resolved(bool async)
    {
        var probe = new ProbePolicy();
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        var ex = await Assert.ThrowsAsync<HttpsRequiredException>(
            () => SendAsync(Pipeline(probe, transport), Get("http://api.example.com/"), async));

        Assert.Equal("http", ex.Scheme);
        Assert.Equal(nameof(ProbePolicy), ex.PolicyName);
        Assert.Equal(0, probe.CredentialCalls);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task The_guard_also_covers_a_reactive_schemes_outbound_pass()
    {
        var probe = new ProbePolicy { Credential = (_, _) => null };
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        await Assert.ThrowsAsync<HttpsRequiredException>(
            () => SendAsync(Pipeline(probe, transport), Get("http://api.example.com/"), async: true));

        Assert.Equal(0, transport.CallCount);
        Assert.Equal(0, probe.CredentialCalls);
    }

    [Fact]
    public async Task Case_insensitive_scheme_compare()
    {
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(new ProbePolicy(), transport), Get("HTTPS://api.example.com/"), async: true);

        Assert.Equal(1, transport.CallCount);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Stamp
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_non_null_credential_replaces_an_existing_header_value()
    {
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var request = Get().WithHeaders(Headers.Empty.Set("Authorization", "old"));

        using var response = await SendAsync(Pipeline(new ProbePolicy(), transport), request, async: true);

        Assert.Equal(["Probe-1"], transport.LastRequest!.Headers.GetAll("Authorization"));
    }

    [Fact]
    public async Task A_null_credential_stamps_nothing()
    {
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var probe = new ProbePolicy { Credential = (_, _) => null };

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async: true);

        Assert.Null(transport.LastRequest!.Headers.Get("Authorization"));
        Assert.Equal(1, probe.CredentialCalls);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The 401 hook (AUTH-30)
    // ---------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_401_with_WWW_Authenticate_calls_the_hook_with_the_parsed_challenges(bool async)
    {
        AuthChallengeContext? seen = null;
        var probe = new ProbePolicy { Hook = c => { seen = c; return null; } };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\", Digest realm=\"d\", nonce=\"n\""));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async);

        Assert.NotNull(seen);
        Assert.Equal(["basic", "digest"], seen.Challenges.Select(c => c.Scheme));
        Assert.Equal(AuthScheme.ApiKey, seen.Requirement.Scheme);
        Assert.Equal("Probe-1", seen.Request.Headers.Get("Authorization"));
        Assert.Same(response, seen.Response);
        Assert.NotNull(seen.Context);
        Assert.Equal(1, probe.ChallengeCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_non_null_replacement_is_driven_once_through_the_continuation(bool async)
    {
        var probe = new ProbePolicy { Hook = c => c.Request.WithHeaders(c.Request.Headers.Set("Authorization", "Retry-2")) };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal("Probe-1", transport.Requests[0].Headers.Get("Authorization"));
        Assert.Equal("Retry-2", transport.Requests[1].Headers.Get("Authorization"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task No_further_challenge_handling_after_the_replay(bool async)
    {
        var probe = new ProbePolicy { Hook = c => c.Request.WithHeaders(c.Request.Headers.Set("Authorization", "Retry-2")) };
        using var transport = new ScriptedTransport(
            TestResponses.Unauthorized("Basic realm=\"r\""),
            TestResponses.Unauthorized("Basic realm=\"r\""));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal(1, probe.ChallengeCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_default_hook_yields_null_and_the_401_is_returned(bool async)
    {
        var probe = new ProbePolicy();
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_401_is_disposed_before_the_replay_and_the_returned_response_is_the_replays(bool async)
    {
        var log = new List<string>();
        using var first = new TrackingResponseBody(log, "first");
        using var second = new TrackingResponseBody(log, "second");
        var probe = new ProbePolicy { Hook = c => c.Request.WithHeaders(c.Request.Headers.Set("Authorization", "Retry-2")) };
        using var transport = new ScriptedTransport(
            TestResponses.Unauthorized(null, first, "Basic realm=\"r\""),
            (Func<Request, Response>)(r =>
            {
                lock (log)
                {
                    log.Add("transport:second");
                }

                return TestResponses.Create(Status.Ok, r, body: second);
            }));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async);

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(0, second.DisposeCount);
        Assert.Equal(200, response.Status.Code);
        Assert.True(log.IndexOf("first:dispose") < log.IndexOf("transport:second"), string.Join(",", log));
    }

    [Fact]
    public async Task Each_WWW_Authenticate_field_is_parsed_on_its_own()
    {
        AuthChallengeContext? seen = null;
        var probe = new ProbePolicy { Hook = c => { seen = c; return null; } };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Digest realm=\"unterminated", "Bearer realm=\"r\""));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async: true);

        Assert.Equal(["digest", "bearer"], seen!.Challenges.Select(c => c.Scheme));
        Assert.Equal("r", seen.Challenges[1].Parameters["realm"]);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // AUTH-33
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_401_without_WWW_Authenticate_is_returned_unchanged_and_the_hook_is_not_called()
    {
        var probe = new ProbePolicy { Hook = _ => throw new InvalidOperationException("must not be called") };
        using var transport = new ScriptedTransport(TestResponses.Create(Status.Unauthorized));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async: true);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(0, probe.ChallengeCalls);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(200)]
    public async Task A_non_401_with_the_header_is_returned_unchanged(int code)
    {
        var probe = new ProbePolicy { Hook = _ => throw new InvalidOperationException("must not be called") };
        var headers = new Headers.Builder().Add("WWW-Authenticate", "Basic realm=\"r\"").Build();
        using var transport = new ScriptedTransport(TestResponses.Create(Status.FromCode(code), headers: headers));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async: true);

        Assert.Equal(code, response.Status.Code);
        Assert.Equal(0, probe.ChallengeCalls);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // AUTH-32
    // ---------------------------------------------------------------------------------------------------------------

    public static TheoryData<string> ThrowingArms => ["sync-hook", "async-hook", "synchronous-throw-from-async-override"];

    [Theory]
    [MemberData(nameof(ThrowingArms))]
    public async Task A_hook_that_throws_disposes_the_401_and_propagates_the_exception(string arm)
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log, "401");
        var failure = new SdkException("hook failed");
        var probe = arm switch
        {
            "sync-hook" => new ProbePolicy { Hook = _ => throw failure },
            "async-hook" => new ProbePolicy { AsyncHook = async _ => { await Task.Yield(); throw failure; } },
            _ => new ProbePolicy { AsyncHook = _ => throw failure },
        };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized(null, body, "Basic realm=\"r\""));

        var ex = await Assert.ThrowsAsync<SdkException>(
            () => SendAsync(Pipeline(probe, transport), Get(), async: arm != "sync-hook"));

        Assert.Same(failure, ex);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task A_failing_dispose_is_suppressed_onto_the_hook_exception()
    {
        var disposeFailure = new InvalidOperationException("dispose failed");
        using var body = new TrackingResponseBody([], "401", disposeFailure);
        var hookFailure = new SdkException("hook failed");
        var probe = new ProbePolicy { Hook = _ => throw hookFailure };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized(null, body, "Basic realm=\"r\""));

        var ex = await Assert.ThrowsAsync<SdkException>(() => SendAsync(Pipeline(probe, transport), Get(), async: true));

        Assert.Same(hookFailure, ex);
        Assert.Contains(disposeFailure, ex.Suppressed);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // AUTH-31
    // ---------------------------------------------------------------------------------------------------------------

    private static Request WithStreamBody(Request request) =>
        request.WithBody(RequestBody.FromStream(new ChunkedReadStream([1, 2, 3], 2)));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_replacement_with_a_non_replayable_body_is_not_sent_and_the_original_401_is_returned_undisposed(bool async)
    {
        using var body = new TrackingResponseBody([], "401");
        var probe = new ProbePolicy { Hook = c => WithStreamBody(c.Request.WithMethod(Method.Post)) };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized(null, body, "Basic realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(probe, transport), Get().WithMethod(Method.Post).WithBody(RequestBody.FromString("x")), async);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
        Assert.Equal(0, body.DisposeCount);
    }

    [Theory]
    [InlineData("replayable")]
    [InlineData("none")]
    public async Task A_replacement_with_a_replayable_or_no_body_is_sent(string kind)
    {
        var probe = new ProbePolicy
        {
            Hook = c => kind == "none"
                ? c.Request
                : c.Request.WithMethod(Method.Post).WithBody(RequestBody.FromString("again")),
        };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async: true);

        Assert.Equal(200, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task The_gate_applies_to_every_replacement_regardless_of_which_hook_built_it()
    {
        var probe = new ProbePolicy { AsyncHook = c => new ValueTask<Request?>(WithStreamBody(c.Request.WithMethod(Method.Put))) };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""), TestResponses.Create(Status.Ok));

        using var response = await SendAsync(Pipeline(probe, transport), Get(), async: true);

        Assert.Equal(401, response.Status.Code);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task A_cross_origin_replacement_throws_InvalidOperationException_after_disposing_the_401()
    {
        using var body = new TrackingResponseBody([], "401");
        var probe = new ProbePolicy { Hook = c => c.Request.WithUrl(new Uri("https://evil.example.org/")) };
        using var transport = new ScriptedTransport(TestResponses.Unauthorized(null, body, "Basic realm=\"r\""), TestResponses.Create(Status.Ok));

        await Assert.ThrowsAsync<InvalidOperationException>(() => SendAsync(Pipeline(probe, transport), Get(), async: true));

        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(1, transport.CallCount);
    }

    // ---------------------------------------------------------------------------------------------------------------
    // AUTH-38
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_returns_a_faulted_task_instead_of_throwing_for_an_http_url()
    {
        var probe = new ProbePolicy();
        var context = TestContexts.For(Get("http://api.example.com/"));
        var runner = TestContexts.RunnerOver(new ScriptedTransport(TestResponses.Create(Status.Ok)));

        var task = probe.ProcessAsync(Get("http://api.example.com/"), context, runner);

        await Assert.ThrowsAsync<HttpsRequiredException>(async () => await task);
    }

    [Fact]
    public async Task ProcessAsync_faults_for_a_resolver_failure_and_a_credential_that_throws_synchronously()
    {
        var options = new RequestOptions { Auth = new AuthDescriptor(new AuthRequirement(AuthScheme.Digest)) };
        var resolver = new ProbePolicy();
        var task = resolver.ProcessAsync(
            Get(),
            TestContexts.For(Get(), requestOptions: options),
            TestContexts.RunnerOver(new ScriptedTransport(TestResponses.Create(Status.Ok))));
        await Assert.ThrowsAsync<AuthResolutionException>(async () => await task);

        var throwing = new ProbePolicy { AsyncCredential = (_, _) => throw new SdkException("provider failed") };
        var second = throwing.ProcessAsync(
            Get(),
            TestContexts.For(Get()),
            TestContexts.RunnerOver(new ScriptedTransport(TestResponses.Create(Status.Ok))));
        await Assert.ThrowsAsync<SdkException>(async () => await second);
    }

    [Fact]
    public void Process_throws_the_original_exception_on_the_sync_path()
    {
        var probe = new ProbePolicy();

        Assert.Throws<HttpsRequiredException>(() => probe.Process(
            Get("http://api.example.com/"),
            TestContexts.For(Get("http://api.example.com/")),
            TestContexts.RunnerOver(new ScriptedTransport(TestResponses.Create(Status.Ok)))));
    }

    [Fact]
    public async Task The_sync_and_async_paths_produce_identical_requests()
    {
        async Task<IReadOnlyList<string?>> Run(bool async)
        {
            var probe = new ProbePolicy { Hook = c => c.Request.WithHeaders(c.Request.Headers.Set("Authorization", "Retry-2")) };
            using var transport = new ScriptedTransport(TestResponses.Unauthorized("Basic realm=\"r\""), TestResponses.Create(Status.Ok));
            using var response = await SendAsync(Pipeline(probe, transport), Get(), async);
            return [.. transport.Requests.Select(r => r.Headers.Get("Authorization"))];
        }

        Assert.Equal(await Run(async: true), await Run(async: false));
    }

    // ---------------------------------------------------------------------------------------------------------------
    // The probe
    // ---------------------------------------------------------------------------------------------------------------

    private sealed class ProbePolicy()
        : AuthorizationPolicy(new AuthDescriptor(new AuthRequirement(AuthScheme.ApiKey)), [AuthScheme.ApiKey])
    {
        private int _credentialCalls;
        private int _challengeCalls;

        public int CredentialCalls => Volatile.Read(ref _credentialCalls);

        public int ChallengeCalls => Volatile.Read(ref _challengeCalls);

        public Func<AuthRequirement, Request, (string, string)?> Credential { get; init; } = (_, _) => ("Authorization", "Probe-1");

        public Func<AuthRequirement, Request, ValueTask<(string, string)?>>? AsyncCredential { get; init; }

        public Func<AuthChallengeContext, Request?>? Hook { get; init; }

        public Func<AuthChallengeContext, ValueTask<Request?>>? AsyncHook { get; init; }

        protected override IReadOnlyList<HttpHeaderName> WithheldHeaderNames { get; } = [HttpHeaderName.WellKnown.Authorization];

        protected override async ValueTask<(string HeaderName, string HeaderValue)?> GetCredentialAsync(
            AuthRequirement requirement,
            Request request,
            PipelineContext context)
        {
            Interlocked.Increment(ref _credentialCalls);
            return AsyncCredential is { } asyncCredential
                ? await asyncCredential(requirement, request)
                : Credential(requirement, request);
        }

        protected override (string HeaderName, string HeaderValue)? GetCredential(
            AuthRequirement requirement,
            Request request,
            PipelineContext context)
        {
            Interlocked.Increment(ref _credentialCalls);
            return Credential(requirement, request);
        }

        protected override ValueTask<Request?> OnChallengeAsync(AuthChallengeContext challenge)
        {
            Interlocked.Increment(ref _challengeCalls);
            if (AsyncHook is { } asyncHook)
            {
                return asyncHook(challenge);
            }

            return Hook is { } hook ? new ValueTask<Request?>(hook(challenge)) : base.OnChallengeAsync(challenge);
        }

        protected override Request? OnChallenge(AuthChallengeContext challenge)
        {
            Interlocked.Increment(ref _challengeCalls);
            return Hook is { } hook ? hook(challenge) : base.OnChallenge(challenge);
        }
    }
}
