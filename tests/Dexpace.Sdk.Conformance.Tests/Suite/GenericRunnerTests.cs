// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Suite;

/// <summary>
/// Plan 1.1 (P10-7, R1): the six statuses, the bound and the waiver rule come out of the generic core, so the invariant, codec
/// and packaging suites mean the same by a status as the transport suite does. The context here is a trivial one.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GenericRunnerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ConformanceAssertion Asserting(Func<Probe, CancellationToken, Task> body, TransportFace[]? faces = null, string name = "xcut-1.probe", string id = "XCUT-1") =>
        ConformanceAssertion.Create(name, [id], RequirementLevel.Must, faces ?? [TransportFace.None], body);

    private static Task<ConformanceResult> RunAsync(ConformanceAssertion assertion, TransportFace face = TransportFace.None, TimeSpan? timeout = null, IReadOnlyList<ConformanceWaiver>? waivers = null, Probe? probe = null) =>
        SuiteRunnerCore.RunAsync(assertion, face, () => probe ?? new Probe(), timeout ?? TimeSpan.FromSeconds(10), waivers ?? [], Ct);

    [Fact]
    public async Task A_none_face_assertion_runs_without_a_factory()
    {
        var result = await RunAsync(Asserting((_, _) => Task.CompletedTask));

        Assert.Equal(ConformanceStatus.Passed, result.Status);
        Assert.Equal(TransportFace.None, result.Face);
    }

    [Fact]
    public async Task The_body_is_given_the_context_the_runner_built()
    {
        await using var probe = new Probe();
        ConformanceAssertion assertion = Asserting((context, _) =>
        {
            context.Seen = true;
            return Task.CompletedTask;
        });

        await RunAsync(assertion, probe: probe);

        Assert.True(probe.Seen);
        Assert.Equal(1, probe.Disposed);
    }

    [Fact]
    public async Task The_six_statuses_come_out_of_the_generic_core()
    {
        var failed = await RunAsync(Asserting((_, _) => throw new ConformanceException("broken", "a", "b")));
        var vacuous = await RunAsync(Asserting((_, _) => throw new ConformanceVacuousException("no antecedent")));
        var notExercised = await RunAsync(Asserting((_, _) => throw new NotExercisedException("needs the hook")));
        var errored = await RunAsync(Asserting((_, _) => throw new InvalidOperationException("kit bug")));
        var passed = await RunAsync(Asserting((_, _) => Task.CompletedTask));

        Assert.Equal(ConformanceStatus.Failed, failed.Status);
        Assert.Contains("expected: a; actual: b", failed.Detail, StringComparison.Ordinal);
        Assert.Equal(ConformanceStatus.Vacuous, vacuous.Status);
        Assert.Equal(ConformanceStatus.NotExercised, notExercised.Status);
        Assert.Equal("needs the hook", notExercised.Detail);
        Assert.Equal(ConformanceStatus.Errored, errored.Status);
        Assert.Equal(ConformanceStatus.Passed, passed.Status);
    }

    [Fact]
    public async Task A_body_exceeding_the_assertion_timeout_is_Failed_naming_the_bound_and_the_assertion()
    {
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await RunAsync(Asserting((_, _) => never.Task, name: "xcut-1.hangs"), timeout: TimeSpan.FromMilliseconds(200));

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("200 ms", result.Detail, StringComparison.Ordinal);
        Assert.Contains("xcut-1.hangs", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_waiver_turns_failed_into_waived_and_a_passing_covered_assertion_into_a_stale_waiver_failure()
    {
        var waiver = new ConformanceWaiver("XCUT-1", "known gap") { Face = TransportFace.None };

        var waived = await RunAsync(Asserting((_, _) => throw new ConformanceException("x")), waivers: [waiver]);
        var stale = await RunAsync(Asserting((_, _) => Task.CompletedTask), waivers: [waiver]);

        Assert.Equal(ConformanceStatus.Waived, waived.Status);
        Assert.Same(waiver, waived.Waiver);
        Assert.Equal(ConformanceStatus.Failed, stale.Status);
        Assert.Contains("no longer needed", stale.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_waiver_target_is_a_failed_synthetic_row()
    {
        var waiver = new ConformanceWaiver("XCUT-1", "typo") { Assertion = "xcut-1.does-not-exist" };
        var assertion = Asserting((_, _) => Task.CompletedTask);
        var identity = new SuiteIdentity("invariant", "preamble");

        var report = await SuiteRunnerCore.RunAllAsync(
            "subject",
            identity,
            [assertion],
            (a, f) => RunAsync(a, f),
            [waiver]);

        Assert.False(report.IsGreen);
        Assert.Contains(report.Results, r => r.Status == ConformanceStatus.Failed && r.Detail.Contains("unknown waiver target", StringComparison.Ordinal));
        Assert.Equal("invariant", report.Suite);
        Assert.Equal("preamble", report.PreambleText);
    }

    [Fact]
    public async Task The_generic_core_rejects_a_face_the_assertion_does_not_declare()
    {
        var assertion = Asserting((_, _) => Task.CompletedTask, faces: [TransportFace.Async]);

        await Assert.ThrowsAsync<ArgumentException>(() => RunAsync(assertion, TransportFace.None));
    }

    [Fact]
    public async Task The_outer_token_cancelled_abandons_the_run_with_cancellation_and_still_disposes()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var probe = new Probe();
        var assertion = Asserting(async (_, token) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SuiteRunnerCore.RunAsync(assertion, TransportFace.None, () => probe, TimeSpan.FromSeconds(10), [], cts.Token));

        Assert.Equal(1, probe.Disposed);
    }

    [Fact]
    public void TransportFace_has_three_members_with_stable_values()
    {
        // A pin: the enum is part of the kit's public surface, and None was added by phase 10 (P10-7).
        Assert.Equal(0, (int)TransportFace.Async);
        Assert.Equal(1, (int)TransportFace.Blocking);
        Assert.Equal(2, (int)TransportFace.None);
        Assert.Equal(3, Enum.GetValues<TransportFace>().Length);
    }

    private sealed class Probe : IAsyncDisposable
    {
        public bool Seen { get; set; }

        public int Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed++;
            return ValueTask.CompletedTask;
        }
    }
}
