// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;
using static Dexpace.Sdk.Conformance.Tests.Support.TestAssertions;

namespace Dexpace.Sdk.Conformance.Tests.Suite;

/// <summary>The public entry points: argument checks, the stable assertion list, and the delegation to the runner.</summary>
[Trait("Category", "Unit")]
public sealed class TransportSuiteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Assertions_is_a_stable_read_only_list()
    {
        var first = TransportSuite.Assertions;
        var second = TransportSuite.Assertions;

        Assert.Same(first, second);
        Assert.IsAssignableFrom<IReadOnlyList<ConformanceAssertion>>(first);
        Assert.False(first is IList<ConformanceAssertion> { IsReadOnly: false });
    }

    [Fact]
    public void RunAsync_rejects_null_arguments_and_a_face_the_assertion_does_not_declare()
    {
        var subject = new FakeSubject().Subject();
        var assertion = Make("transport-1.a", ["TRANSPORT-1"], faces: [TransportFace.Async]);

        Assert.Throws<ArgumentNullException>(() => { _ = TransportSuite.RunAsync(null!, assertion, TransportFace.Async, null, Ct); });
        Assert.Throws<ArgumentNullException>(() => { _ = TransportSuite.RunAsync(subject, null!, TransportFace.Async, null, Ct); });
        Assert.Throws<ArgumentException>(() => { _ = TransportSuite.RunAsync(subject, assertion, TransportFace.Blocking, null, Ct); });
        Assert.Throws<ArgumentException>(() => { _ = TransportSuite.RunAsync(subject, assertion, (TransportFace)7, null, Ct); });
    }

    [Fact]
    public void RunAllAsync_rejects_a_null_subject()
    {
        Assert.Throws<ArgumentNullException>(() => { _ = TransportSuite.RunAllAsync(null!, null, Ct); });
    }

    [Fact]
    public async Task RunAsync_runs_the_assertion_with_default_options_when_none_are_given()
    {
        var assertion = Make("transport-1.a", ["TRANSPORT-1"], body: (_, _) => Task.CompletedTask);

        var result = await TransportSuite.RunAsync(new FakeSubject().Subject(), assertion, TransportFace.Async, cancellationToken: Ct);

        Assert.Equal(ConformanceStatus.Passed, result.Status);
    }

    [Fact]
    public async Task RunAllAsync_returns_a_green_report_for_a_subject_when_nothing_has_failed()
    {
        var report = await TransportSuite.RunAllAsync(new FakeSubject().Subject(), cancellationToken: Ct);

        Assert.Equal("fake", report.SubjectName);
        Assert.Equal(TransportSuite.Assertions.Sum(a => a.Faces.Count), report.Results.Count);
    }
}
