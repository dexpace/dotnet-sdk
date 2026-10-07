// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/cursor.test.ts (fork independence, substitution sticking). Not
// ported: the one-shot Next reuse cases, which test a mutable cursor .NET does not have (P4c-12).

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class ForkTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    [Fact]
    public async Task Driving_the_same_runner_twice_re_runs_the_whole_downstream_tail()
    {
        var log = new List<string>();
        var perAttempt = new ProbePolicy("perAttempt", PipelineStage.PerAttempt, log);
        var auth = new ProbePolicy("auth", PipelineStage.Auth, log);
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new ForkingProbe(PipelineStage.Retry, drives: 2))
            .Add(perAttempt)
            .Add(auth)
            .Build(transport);

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(2, perAttempt.Entered);
        Assert.Equal(2, auth.Entered);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task Each_fork_carries_the_request_its_forker_passed()
    {
        var first = MakeRequest();
        var second = first.WithHeaders(first.Headers.Set("X-Fork", "2"));
        var seen = new List<Request>();
        var forker = new DelegatePolicy(
            PipelineStage.Retry,
            async (_, context, next) =>
            {
                (await next.RunAsync(first, context).ConfigureAwait(false)).Dispose();
                return await next.RunAsync(second, context).ConfigureAwait(false);
            });
        var recorder = new DelegatePolicy(PipelineStage.PerAttempt, (request, context, next) =>
        {
            seen.Add(request);
            return next.RunAsync(request, context);
        });
        var pipeline = new PipelineBuilder().Add(forker).Add(recorder).Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(2, seen.Count);
        Assert.Same(first, seen[0]);
        Assert.Same(second, seen[1]);
    }

    [Fact]
    public async Task Forks_share_call_scoped_state_and_not_per_drive_state()
    {
        var key = new PipelinePropertyKey<string>("shared");
        var attempts = new List<int>();
        var sharedSeen = new List<bool>();
        var forker = new DelegatePolicy(
            PipelineStage.Retry,
            async (request, context, next) =>
            {
                (await next.RunAsync(request, context.ForAttempt(1)).ConfigureAwait(false)).Dispose();
                return await next.RunAsync(request, context.ForAttempt(2)).ConfigureAwait(false);
            });
        var recorder = new DelegatePolicy(PipelineStage.PerAttempt, (request, context, next) =>
        {
            attempts.Add(context.AttemptNumber);
            sharedSeen.Add(context.TryGetProperty(key, out _));
            context.SetProperty(key, "set");
            return next.RunAsync(request, context);
        });
        var pipeline = new PipelineBuilder().Add(forker).Add(recorder).Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], attempts);
        Assert.Equal([false, true], sharedSeen);
    }
}
