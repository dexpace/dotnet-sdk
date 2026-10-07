// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/recovery/response-chain.test.ts and the Ruby R5-R7 case lists
// (ruby-sdk@90075b1 docs/work/mvp/phase4/phase4b/2026-09-08-phase4b-recovery-primitives-design.md, "Testing strategy").
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

#pragma warning disable CA2201 // OutOfMemoryException is the fatal exception under test (design §5.2).

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class ResponseRecoveryChainTests
{
    private static Outcome.Success Ok(ResponseBody? body = null) => new(TestResponses.Create(Status.Ok, body: body));

    private static Outcome.Failure Fail(string message = "failed") => new(new IOException(message));

    private static DelegateResponseStep ResponseLog(List<string> log, string name) =>
        new((r, _) =>
        {
            log.Add(name);
            return r;
        });

    private static DelegateRecoveryStep RecoveryLog(List<string> log, string name) =>
        new((o, _) =>
        {
            log.Add(name);
            return o;
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_response_phase_runs_only_while_the_outcome_is_a_success(bool async)
    {
        var step = new DelegateResponseStep((r, _) => r);
        var chain = new ResponseRecoveryChain([step], []);
        var failure = Fail();

        var result = await Run.Apply(chain, failure, async);

        Assert.Same(failure, result);
        Assert.Equal(0, step.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recovery_steps_run_on_every_outcome_in_order(bool async)
    {
        foreach (var outcome in new Outcome[] { Ok(), Fail() })
        {
            var log = new List<string>();
            var chain = new ResponseRecoveryChain([], [RecoveryLog(log, "c1"), RecoveryLog(log, "c2")]);

            var result = await Run.Apply(chain, outcome, async);

            Assert.Same(outcome, result);
            Assert.Equal(["c1", "c2"], log);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Recovery_steps_observe_a_failure_a_response_step_just_produced(bool async)
    {
        var boom = new InvalidOperationException("boom");
        Outcome? seen = null;
        var chain = new ResponseRecoveryChain(
            [new DelegateResponseStep((_, _) => throw boom)],
            [new DelegateRecoveryStep((o, _) => seen = o)]);

        await Run.Apply(chain, Ok(), async);

        Assert.Same(boom, Assert.IsType<Outcome.Failure>(seen).Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_response_phase_runs_first_then_the_recovery_phase_in_declared_order(bool async)
    {
        var log = new List<string>();
        var chain = new ResponseRecoveryChain(
            [ResponseLog(log, "r1"), ResponseLog(log, "r2")],
            [RecoveryLog(log, "c1"), RecoveryLog(log, "c2")]);

        await Run.Apply(chain, Ok(), async);

        Assert.Equal(["r1", "r2", "c1", "c2"], log);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_throwing_response_step_becomes_a_failure_the_rest_are_skipped_and_nothing_propagates(bool async)
    {
        var boom = new InvalidOperationException("boom");
        var second = new DelegateResponseStep((r, _) => r);
        var chain = new ResponseRecoveryChain([new DelegateResponseStep((_, _) => throw boom), second], []);

        var result = await Run.Apply(chain, Ok(), async);

        Assert.Same(boom, Assert.IsType<Outcome.Failure>(result).Error);
        Assert.Equal(0, second.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_throwing_recovery_step_becomes_a_failure_fed_to_the_next_recovery_step(bool async)
    {
        var boom = new InvalidOperationException("boom");
        Outcome? seenByNext = null;
        var next = new DelegateRecoveryStep((o, _) => seenByNext = o);
        var chain = new ResponseRecoveryChain([], [new DelegateRecoveryStep((_, _) => throw boom), next]);

        var result = await Run.Apply(chain, Fail(), async);

        Assert.Equal(1, next.CallCount);
        Assert.Same(boom, Assert.IsType<Outcome.Failure>(seenByNext).Error);
        Assert.Same(seenByNext, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_step_returning_null_is_treated_as_an_InvalidOperationException_naming_the_step_type(bool async)
    {
        Outcome? seen = null;
        var chain = new ResponseRecoveryChain(
            [new DelegateResponseStep((_, _) => null!)],
            [new DelegateRecoveryStep((o, _) => seen = o)]);

        await Run.Apply(chain, Ok(), async);

        var error = Assert.IsType<InvalidOperationException>(Assert.IsType<Outcome.Failure>(seen).Error);
        Assert.Contains(nameof(DelegateResponseStep), error.Message, StringComparison.Ordinal);

        var recoveryChain = new ResponseRecoveryChain([], [new DelegateRecoveryStep((_, _) => null!)]);
        var result = await Run.Apply(recoveryChain, Fail(), async);
        var recoveryError = Assert.IsType<InvalidOperationException>(Assert.IsType<Outcome.Failure>(result).Error);
        Assert.Contains(nameof(DelegateRecoveryStep), recoveryError.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_chain_never_checks_the_token_itself(bool async)
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var chain = new ResponseRecoveryChain(
            [new DelegateResponseStep((r, _) => r)],
            [new DelegateRecoveryStep((o, _) => o)]);
        var outcome = Ok();

        var result = await Run.ApplyWith(chain, outcome, async, cts.Token);

        Assert.Same(outcome, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_returned_failure_reaches_the_next_recovery_step(bool async)
    {
        var returned = new IOException("returned, never thrown");
        Outcome? seen = null;
        var chain = new ResponseRecoveryChain(
            [],
            [new DelegateRecoveryStep((_, _) => new Outcome.Failure(returned)), new DelegateRecoveryStep((o, _) => seen = o)]);

        await Run.Apply(chain, Ok(), async);

        Assert.Same(returned, Assert.IsType<Outcome.Failure>(seen).Error);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_throwing_step_with_a_success_in_hand_releases_it_exactly_once(bool async)
    {
        var body = new DisposalCountingBody();
        var chain = new ResponseRecoveryChain(
            [
                new DelegateResponseStep((r, _) =>
                {
                    r.Dispose();
                    throw new InvalidOperationException("after releasing itself");
                }),
            ],
            []);

        await Run.Apply(chain, Ok(body), async);

        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_failing_release_lands_on_the_primarys_trail_and_the_primary_is_surfaced(bool async)
    {
        var releaseFailure = new IOException("close failed");
        var primary = new InvalidOperationException("primary");
        var chain = new ResponseRecoveryChain([new DelegateResponseStep((_, _) => throw primary)], []);

        var result = await Run.Apply(chain, Ok(new ProbeResponseBody(disposeFailure: releaseFailure)), async);

        Assert.Same(primary, Assert.IsType<Outcome.Failure>(result).Error);
        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_failure_in_hand_releases_nothing(bool async)
    {
        var primary = new InvalidOperationException("primary");
        var chain = new ResponseRecoveryChain([], [new DelegateRecoveryStep((_, _) => throw primary)]);

        var result = await Run.Apply(chain, Fail(), async);

        Assert.Same(primary, Assert.IsType<Outcome.Failure>(result).Error);
        Assert.Empty(ExceptionTrail.GetSuppressed(primary));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_recovery_step_returning_a_substitute_success_leaves_the_original_undisposed(bool async)
    {
        var original = new DisposalCountingBody();
        using var substituteResponse = TestResponses.Create(Status.Accepted);
        var substitute = new Outcome.Success(substituteResponse);
        var chain = new ResponseRecoveryChain([], [new DelegateRecoveryStep((_, _) => substitute)]);

        var result = await Run.Apply(chain, Ok(original), async);

        Assert.Same(substitute, result);
        Assert.Equal(0, original.DisposeCount);
    }

    [Fact]
    public void Both_lists_are_copied_at_construction()
    {
        var responseSteps = new List<IResponseStep> { new DelegateResponseStep((r, _) => r) };
        var recoverySteps = new List<IRecoveryStep> { new DelegateRecoveryStep((o, _) => o) };
        var chain = new ResponseRecoveryChain(responseSteps, recoverySteps);

        responseSteps.Clear();
        recoverySteps.Add(new DelegateRecoveryStep((o, _) => o));

        Assert.Single(chain.ResponseSteps);
        Assert.Single(chain.RecoverySteps);
        Assert.Equal("responseSteps", Assert.Throws<ArgumentException>(() => new ResponseRecoveryChain([null!], [])).ParamName);
        Assert.Equal("recoverySteps", Assert.Throws<ArgumentException>(() => new ResponseRecoveryChain([], [null!])).ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task One_chain_serves_concurrent_applies(bool async)
    {
        var chain = new ResponseRecoveryChain(
            [new DelegateResponseStep((r, _) => r)],
            [new DelegateRecoveryStep((o, _) => o)]);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 64),
            TestContext.Current.CancellationToken,
            async (_, ct) =>
            {
                var outcome = Ok();
                Assert.Same(outcome, await Run.ApplyWith(chain, outcome, async, ct));
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_fatal_exception_from_a_step_propagates(bool async)
    {
        var chain = new ResponseRecoveryChain([new DelegateResponseStep((_, _) => throw new OutOfMemoryException())], []);
        var recovery = new ResponseRecoveryChain([], [new DelegateRecoveryStep((_, _) => throw new OutOfMemoryException())]);

        await Assert.ThrowsAsync<OutOfMemoryException>(() => Run.Apply(chain, Ok(), async));
        await Assert.ThrowsAsync<OutOfMemoryException>(() => Run.Apply(recovery, Ok(), async));
    }
}
