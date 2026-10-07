// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/recovery/request-chain.test.ts.
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Recovery;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class RequestRecoveryChainTests
{
    private static readonly Request s_request = Request.Get("https://example.test/");

    private static DelegateRequestStep Logging(List<string> log, string name) =>
        new((r, _) =>
        {
            log.Add(name);
            return r;
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_empty_chain_returns_the_input_by_reference(bool async)
    {
        Assert.Same(s_request, await Run.Apply(RequestRecoveryChain.Empty, s_request, async));
        Assert.Same(s_request, await Run.Apply(new RequestRecoveryChain([]), s_request, async));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Steps_fold_left_to_right(bool async)
    {
        var log = new List<string>();
        var chain = new RequestRecoveryChain([Logging(log, "a"), Logging(log, "b"), Logging(log, "c")]);

        await Run.Apply(chain, s_request, async);

        Assert.Equal(["a", "b", "c"], log);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_throwing_step_aborts_the_rest_and_propagates(bool async)
    {
        var boom = new InvalidOperationException("boom");
        var after = new DelegateRequestStep((r, _) => r);
        var chain = new RequestRecoveryChain([new DelegateRequestStep((_, _) => throw boom), after]);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Run.Apply(chain, s_request, async));

        Assert.Same(boom, thrown);
        Assert.Equal(0, after.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_chain_copies_its_steps_at_construction(bool async)
    {
        var log = new List<string>();
        var steps = new List<IRequestStep> { Logging(log, "a") };
        var chain = new RequestRecoveryChain(steps);

        steps.Add(Logging(log, "late"));
        steps.Clear();
        await Run.Apply(chain, s_request, async);

        Assert.Equal(["a"], log);
        Assert.Single(chain.Steps);
        Assert.False(chain.Steps is IList<IRequestStep> { IsReadOnly: false });
    }

    [Fact]
    public void A_null_element_is_rejected_at_construction()
    {
        var thrown = Assert.Throws<ArgumentException>(() => new RequestRecoveryChain([null!]));

        Assert.Equal("steps", thrown.ParamName);
    }

    [Fact]
    public void A_null_steps_argument_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new RequestRecoveryChain(null!));
        Assert.Throws<ArgumentNullException>(() => RequestRecoveryChain.Empty.Apply(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task One_chain_serves_concurrent_applies(bool async)
    {
        var chain = new RequestRecoveryChain(
        [
            new DelegateRequestStep((r, _) => r.WithHeader("X-Echo", r.Headers.Get("X-In")!)),
        ]);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 64),
            TestContext.Current.CancellationToken,
            async (i, ct) =>
            {
                var result = await Run.ApplyWith(chain, s_request.WithHeader("X-In", "v" + i), async, ct);
                Assert.Equal("v" + i, result.Headers.Get("X-Echo"));
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_cancelled_token_reaches_each_step(bool async)
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var step = new DelegateRequestStep((r, _) => r);

        await Run.ApplyWith(new RequestRecoveryChain([step]), s_request, async, cts.Token);

        Assert.Equal(cts.Token, step.LastToken);
        Assert.True(step.LastToken.IsCancellationRequested);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_step_returning_null_aborts_with_InvalidOperationException_naming_the_step(bool async)
    {
        var chain = new RequestRecoveryChain([new DelegateRequestStep((_, _) => null!)]);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Run.Apply(chain, s_request, async));

        Assert.Contains(nameof(DelegateRequestStep), thrown.Message, StringComparison.Ordinal);
    }
}
