// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S6 (RETRY-44, PIPE-16; design §5.1): a re-driving policy re-sends the request it held, never
/// one a downstream policy mutated. The verified defect: a probe above <c>Auth</c> saw attempt 2 enter carrying the
/// <c>Authorization</c> that attempt 1's <see cref="BasicAuthPolicy"/> stamped, and redirect hop n+1 was built from
/// hop n's stamped request. Permanent (roadmap constraint 5); phase 4c's request-in/response-out signature owns the
/// structural fix.
/// </summary>
[Trait("Category", "Security")]
public sealed class ReDriveRequestIsolationTests
{
    private static readonly DexpaceClientOptions s_options = new()
    {
        Retry = new RetryOptions { BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) },
    };

    [Fact]
    public async Task A_retry_attempt_does_not_enter_carrying_the_previous_attempts_credential()
    {
        var scenarioRequest = Request.Get("https://api.example.com/");
        var probe = new ProbePolicy(PipelineStage.PerAttempt);
        var transport = new ScriptedTransport(TestResponses.Create(Status.ServiceUnavailable, scenarioRequest), TestResponses.Create(Status.Ok, scenarioRequest));
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Add(probe)
            .Add(new BasicAuthPolicy(new BasicCredential("u", "p")))
            .Build(transport);

        using var response = await pipeline.SendAsync(
            scenarioRequest, s_options, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal([null, null], probe.SeenAuthorization);
        Assert.All(transport.Requests, sent => Assert.NotNull(sent.Headers.Get("Authorization")));
    }

    [Fact]
    public async Task A_retry_after_an_exception_re_sends_the_request_it_held()
    {
        var scenarioRequest = Request.Get("https://api.example.com/");
        var probe = new ProbePolicy(PipelineStage.PerAttempt);
        var transport = new ScriptedTransport(new ServiceRequestException("connection refused"), TestResponses.Create(Status.Ok, scenarioRequest));
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Add(probe)
            .Add(new BasicAuthPolicy(new BasicCredential("u", "p")))
            .Build(transport);

        using var response = await pipeline.SendAsync(
            scenarioRequest, s_options, TestContext.Current.CancellationToken);

        Assert.Equal([null, null], probe.SeenAuthorization);
    }

    [Fact]
    public async Task A_redirect_hop_is_not_built_from_the_previous_hops_stamped_request()
    {
        var scenarioRequest = Request.Get("https://api.example.com/start");
        var probe = new ProbePolicy(PipelineStage.PerCall);
        var transport = new ScriptedTransport(
            TestResponses.Redirect(307, "https://api.example.com/moved", scenarioRequest),
            TestResponses.Create(Status.Ok, scenarioRequest));
        var pipeline = new PipelineBuilder()
            .Add(new RedirectPolicy())
            .Add(probe)
            .Add(new BasicAuthPolicy(new BasicCredential("u", "p")))
            .Build(transport);

        using var response = await pipeline.SendAsync(
            scenarioRequest, s_options, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal([null, null], probe.SeenAuthorization);
        Assert.Equal(new Uri("https://api.example.com/moved"), transport.Requests[1].Url);
    }

    [Fact]
    public async Task A_downstream_rewrite_does_not_leak_into_the_next_attempt()
    {
        var scenarioRequest = Request.Get("https://api.example.com/");
        // Any policy below the retry pillar that writes context.Request is undone before the re-drive.
        var probe = new ProbePolicy(PipelineStage.PerAttempt, header: "X-Attempt-Marker");
        var transport = new ScriptedTransport(TestResponses.Create(Status.ServiceUnavailable, scenarioRequest), TestResponses.Create(Status.Ok, scenarioRequest));
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(new InstantTimeProvider()))
            .Add(probe)
            .Add(new MarkingPolicy())
            .Build(transport);

        using var response = await pipeline.SendAsync(
            scenarioRequest, s_options, TestContext.Current.CancellationToken);

        Assert.Equal([null, null], probe.SeenAuthorization);
        Assert.All(transport.Requests, sent => Assert.Equal("set", sent.Headers.Get("X-Attempt-Marker")));
    }

    /// <summary>Records the named header on the request each time the chain passes through it.</summary>
    private sealed class ProbePolicy(PipelineStage stage, string header = "Authorization") : HttpPipelinePolicy
    {
        public List<string?> SeenAuthorization { get; } = [];

        public override PipelineStage Stage => stage;

        public override ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
        {
            SeenAuthorization.Add(context.Request.Headers.Get(header));
            return continuation.RunAsync(context);
        }
    }

    /// <summary>A per-attempt policy that writes a header into <see cref="PipelineContext.Request"/>.</summary>
    private sealed class MarkingPolicy : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.PerAttempt;

        public override ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
        {
            context.Request = context.Request.WithHeaders(context.Request.Headers.Set("X-Attempt-Marker", "set"));
            return continuation.RunAsync(context);
        }
    }
}
