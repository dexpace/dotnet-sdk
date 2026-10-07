// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>
/// Placed in the "Instrumentation" collection so these tests do not run in parallel with
/// InstrumentationPolicyTests — both exercise DexpaceDiagnostics.ActivitySource and
/// concurrent execution causes activity leakage across test instances.
/// </summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class DexpacePipelineTests
{
    private static Request MakeGetRequest() => Request.Get("https://api.example.com/v1/items");

    private static DexpaceClientOptions ZeroRetryOptions() => new()
    {
        Retry = new RetryOptions
        {
            MaxRetryAttempts = 3,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(5),
        }
    };

    // ─── Basic wiring ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefault_ReturnsWorkingPipeline_200()
    {
        var transport = new ScriptedTransport([TestResponses.Create(Status.Ok)]);
        using var pipeline = DexpacePipeline.CreateDefault(transport);

        var response = await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task CreateDefault_AuthPolicy_IsIncluded_WhenProvided()
    {
        var auth = new MarkingPolicy("x-auth-stamped", "true");
        string? authHeaderSeen = null;
        var transport = new RecordingTransport(req =>
        {
            authHeaderSeen = req.Headers.Get("x-auth-stamped");
            return TestResponses.Create(Status.Ok);
        });

        using var pipeline = DexpacePipeline.CreateDefault(transport, authPolicy: auth);
        await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions(), TestContext.Current.CancellationToken);

        Assert.Equal("true", authHeaderSeen);
    }

    // ─── Retry wired ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefault_RetryIsWired_503ThenSuccess()
    {
        var transport = new ScriptedTransport([
            TestResponses.Create(Status.ServiceUnavailable),
            TestResponses.Create(Status.Ok),
        ]);
        using var pipeline = DexpacePipeline.CreateDefault(transport, timeProvider: new InstantTimeProvider());

        var response = await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task CreateDefault_RetryExhausted_ReturnsLastResponse()
    {
        // MaxRetryAttempts = 3 → 1 initial + 3 retries = 4 calls
        var transport = new ScriptedTransport(Enumerable.Repeat(TestResponses.Create(Status.ServiceUnavailable), 4));
        using var pipeline = DexpacePipeline.CreateDefault(transport, timeProvider: new InstantTimeProvider());

        var response = await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.ServiceUnavailable, response.Status);
        Assert.Equal(4, transport.CallCount);
    }

    // ─── Redirect wired ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDefault_RedirectIsWired_302ThenSuccess()
    {
        var redirectHeaders = new Headers.Builder()
            .Set("Location", "https://api.example.com/v1/redirected")
            .Build();

        Uri? finalUrl = null;
        var transport = new RecordingTransport(req =>
        {
            finalUrl = req.Url;
            if (req.Url.AbsolutePath == "/v1/items")
            {
                return TestResponses.Create(Status.Found, headers: redirectHeaders);
            }

            return TestResponses.Create(Status.Ok);
        });

        using var pipeline = DexpacePipeline.CreateDefault(transport);
        var response = await pipeline.SendAsync(MakeGetRequest(), ZeroRetryOptions(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.NotNull(finalUrl);
        Assert.Equal("/v1/redirected", finalUrl.AbsolutePath);
    }

    // ─── Composition (PIPE-39, RECOV-32, RECOV-33 wiring) ─────────────────────────────────────────

    [Fact]
    public void CreateDefault_installs_the_standard_pillars_and_the_per_call_defaults_in_order()
    {
        // Presence, type and stage only: the defaults and behaviour of the idempotency and identity policies are 4b's.
        using var pipeline = DexpacePipeline.CreateDefault(new RecordingTransport(), authPolicy: new MarkingPolicy("x-auth", "1"));

        Assert.Equal(
            [
                (typeof(OperationPolicy), PipelineStage.Operation),
                (typeof(IdempotencyPolicy), PipelineStage.PerCall),
                (typeof(ClientIdentityPolicy), PipelineStage.PerCall),
                (typeof(RedirectPolicy), PipelineStage.Redirect),
                (typeof(RetryPolicy), PipelineStage.Retry),
                (typeof(SetDatePolicy), PipelineStage.PerAttempt),
                (typeof(MarkingPolicy), PipelineStage.Auth),
                (typeof(InstrumentationPolicy), PipelineStage.Diagnostics),
            ],
            pipeline.Policies.Select(p => (p.GetType(), p.Stage)));
    }

    [Fact]
    public void CreateDefault_without_an_auth_policy_has_no_auth_stage()
    {
        using var pipeline = DexpacePipeline.CreateDefault(new RecordingTransport());

        Assert.DoesNotContain(pipeline.Policies, p => p.Stage == PipelineStage.Auth);
    }

    // ─── Nested helpers ──────────────────────────────────────────────────────

    /// <summary>A policy that stamps a fixed header — used to verify auth policy injection.</summary>
    private sealed class MarkingPolicy(string header, string value) : HttpPipelinePolicy
    {
        // Auth stage so it participates correctly in the default pipeline ordering.
        public override PipelineStage Stage => PipelineStage.Auth;

        public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
            continuation.RunAsync(request.WithHeaders(request.Headers.Set(header, value)), context);
    }

}
