// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>AUTH-27, AUTH-29, AUTH-30, AUTH-36: the bearer policy inside the default composition.</summary>
[Trait("Category", "Unit")]
public sealed class BearerPipelineTests
{
    private static readonly DateTimeOffset s_farFuture = new(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class CountingCredential : TokenCredential
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken ct = default) =>
            new(new AccessToken("tok-" + Interlocked.Increment(ref _calls), s_farFuture));
    }

    [Fact]
    public async Task A_challenged_bearer_call_through_the_default_pipeline_succeeds_with_one_resend()
    {
        using var recorder = new ActivityRecorder("Dexpace.Sdk");
        var credential = new CountingCredential();
        using var transport = new ScriptedTransport(TestResponses.Unauthorized("Bearer realm=\"r\""), TestResponses.Create(Status.Ok));
        using var pipeline = DexpacePipeline.CreateDefault(
            transport,
            new BearerTokenAuthPolicy(credential, "s"),
            timeProvider: new InstantTimeProvider());

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/v1/items"), TestContext.Current.CancellationToken);

        // The retry stage does not treat the 401 as retryable (RETRY-1, CFG-35): the resend is the auth step's, below it.
        Assert.Equal(200, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
        Assert.Equal(2, credential.Calls);
        var attempts = recorder.StartedOfKind(ActivityKind.Client);
        Assert.Equal(2, attempts.Count);
        Assert.Equal(1, Convert.ToInt32(attempts[1].GetTagItem("http.request.resend_count"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task A_pipeline_with_redirect_forwards_no_bearer_cross_origin()
    {
        var credential = new CountingCredential();
        using var transport = new ScriptedTransport(
            TestResponses.Redirect(302, "https://other.example.org/landing"),
            TestResponses.Unauthorized("Bearer realm=\"r\""));
        using var pipeline = DexpacePipeline.CreateDefault(
            transport,
            new BearerTokenAuthPolicy(credential, "s"),
            timeProvider: new InstantTimeProvider());

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/v1/items"), TestContext.Current.CancellationToken);

        // The foreign hop carries no token, is not guarded and a 401 from it is not challenge-handled (AUTH-29, P6c-12).
        Assert.Equal(401, response.Status.Code);
        Assert.Equal(2, transport.CallCount);
        Assert.NotNull(transport.Requests[0].Headers.Get("Authorization"));
        Assert.Null(transport.Requests[1].Headers.Get("Authorization"));
        Assert.Equal(1, credential.Calls);
    }
}
