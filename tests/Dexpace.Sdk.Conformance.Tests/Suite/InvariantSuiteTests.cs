// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Suite;

/// <summary>Plan 1.3 (P10-4, R4): the invariant suite's shell. The assertions themselves are pinned by their control classes.</summary>
[Trait("Category", "Unit")]
public sealed class InvariantSuiteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_preamble_says_what_a_green_run_does_not_prove()
    {
        Assert.Contains("NotExercised", InvariantSuite.Preamble, StringComparison.Ordinal);
        Assert.Contains("never passed", InvariantSuite.Preamble, StringComparison.Ordinal);
        Assert.Contains("hook", InvariantSuite.Preamble, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_report_records_the_invariant_suite_and_its_preamble()
    {
        var report = await InvariantSuite.RunAllAsync(new InvariantSubject { Name = "probe" }, new SuiteOptions { AssertionTimeout = TimeSpan.FromSeconds(10) }, Ct);

        Assert.Equal("invariant", report.Suite);
        Assert.Equal("probe", report.SubjectName);
        Assert.Equal(InvariantSuite.Preamble, report.PreambleText);
        Assert.StartsWith(InvariantSuite.Preamble, report.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_absent_hook_ends_the_assertion_as_not_exercised_naming_the_hook()
    {
        var failure = Assert.Throws<NotExercisedException>(() => InvariantContext.RequireHook<Func<int>>(null, nameof(InvariantSubject.CreateBoundedMap)));

        Assert.Contains(nameof(InvariantSubject.CreateBoundedMap), failure.Message, StringComparison.Ordinal);
        Assert.Equal(3, InvariantContext.RequireHook<Func<int>>(() => 3, "x")());
    }

    [Fact]
    public async Task RunAsync_rejects_a_foreign_assertion_a_face_it_does_not_declare_and_null()
    {
        var foreign = TransportSuite.Assertions[0];

        await Assert.ThrowsAsync<ArgumentNullException>(() => InvariantSuite.RunAsync(null!, foreign, TransportFace.Async, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => InvariantSuite.RunAsync(new InvariantSubject(), null!, TransportFace.Async, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => InvariantSuite.RunAllAsync(null!, null, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => InvariantSuite.RunAsync(new InvariantSubject(), foreign, TransportFace.Async, null, Ct));
    }

    [Fact]
    public void The_subject_rejects_null_hooks_and_defaults_to_core()
    {
        var subject = new InvariantSubject();

        Assert.Equal("core", subject.Name);
        Assert.Null(subject.MapStrict);
        Assert.Null(subject.CreateCyclicException);
        Assert.Null(subject.CreateBoundedMap);
        Assert.Null(subject.SeamResolution);
        Assert.Null(subject.DrawCnonce);
        Assert.Equal(4, subject.Credentials.Count);
        Assert.Throws<ArgumentNullException>(() => new InvariantSubject { CreatePipeline = null! });
        Assert.Throws<ArgumentNullException>(() => new InvariantSubject { CreateResponseException = null! });
        Assert.Throws<ArgumentException>(() => new InvariantSubject { Name = " " });
    }

    [Fact]
    public async Task The_bound_is_ten_seconds_or_half_the_assertion_bound_when_shorter()
    {
        await using var shortRun = new InvariantContext(new InvariantSubject(), TransportFace.None, new SuiteOptions { AssertionTimeout = TimeSpan.FromSeconds(6) });
        await using var longRun = new InvariantContext(new InvariantSubject(), TransportFace.None, new SuiteOptions());

        Assert.Equal(TimeSpan.FromSeconds(3), shortRun.Bound);
        Assert.Equal(TimeSpan.FromSeconds(10), longRun.Bound);
    }

    [Fact]
    public async Task The_whole_suite_over_the_defaults_is_green_and_exactly_the_five_hookless_assertions_are_not_exercised()
    {
        // The pinned list of every driver that supplies no internal hook, the AOT smoke among them (P10-28, plan reading R13).
        var report = await InvariantSuite.RunAllAsync(new InvariantSubject(), new SuiteOptions { AssertionTimeout = TimeSpan.FromSeconds(20) }, Ct);

        Assert.True(report.IsGreen, report.ToString());
        Assert.Equal(
            [
                "xcut-14.bounded-under-burst",
                "xcut-14.drains-after-insert",
                "xcut-23.resolution-deterministic",
                "xcut-8.strict-factory-rejects",
                "xcut-9.cycle-safe-walk",
            ],
            report.Results.Where(r => r.Status == ConformanceStatus.NotExercised).Select(r => r.Assertion.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(report.Results, r => r.Status is ConformanceStatus.Vacuous or ConformanceStatus.Waived or ConformanceStatus.Failed or ConformanceStatus.Errored);
    }
}
