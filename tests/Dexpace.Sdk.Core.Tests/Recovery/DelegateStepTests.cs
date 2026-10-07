// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class DelegateStepTests
{
    private static readonly Request s_request = Request.Get("https://example.test/");

    [Fact]
    public async Task The_request_fake_runs_the_sync_delegate_on_Apply_and_the_async_one_on_ApplyAsync()
    {
        var step = new DelegateRequestStep(
            (r, _) => r.WithHeader("X-Form", "sync"),
            (r, _) => ValueTask.FromResult(r.WithHeader("X-Form", "async")));
        using var cts = new CancellationTokenSource();

        var sync = step.Apply(s_request, cts.Token);
        var async = await step.ApplyAsync(s_request, cts.Token);

        Assert.Equal("sync", sync.Headers.Get("X-Form"));
        Assert.Equal("async", async.Headers.Get("X-Form"));
        Assert.Equal(1, step.SyncCalls);
        Assert.Equal(1, step.AsyncCalls);
        Assert.Equal(2, step.CallCount);
        Assert.Equal(cts.Token, step.LastToken);
    }

    [Fact]
    public async Task The_request_fake_wraps_the_sync_delegate_when_no_async_one_is_given()
    {
        var step = new DelegateRequestStep((r, _) => r.WithHeader("X-Form", "only"));

        var result = await step.ApplyAsync(s_request, CancellationToken.None);

        Assert.Equal("only", result.Headers.Get("X-Form"));
        Assert.Equal(1, step.AsyncCalls);
    }

    [Fact]
    public async Task The_response_fake_counts_and_records_the_token()
    {
        using var response = TestResponses.Create(Status.Ok);
        var step = new DelegateResponseStep((r, _) => r);
        using var cts = new CancellationTokenSource();

        Assert.Same(response, step.Apply(response, cts.Token));
        Assert.Same(response, await step.ApplyAsync(response, cts.Token));

        Assert.Equal(2, step.CallCount);
        Assert.Equal(cts.Token, step.LastToken);
    }

    [Fact]
    public async Task The_recovery_fake_counts_and_records_the_token()
    {
        using var response = TestResponses.Create(Status.Ok);
        var outcome = new Outcome.Success(response);
        var step = new DelegateRecoveryStep((o, _) => o);
        using var cts = new CancellationTokenSource();

        Assert.Same(outcome, step.Apply(outcome, cts.Token));
        Assert.Same(outcome, await step.ApplyAsync(outcome, cts.Token));

        Assert.Equal(1, step.SyncCalls);
        Assert.Equal(1, step.AsyncCalls);
        Assert.Equal(cts.Token, step.LastToken);
    }
}
