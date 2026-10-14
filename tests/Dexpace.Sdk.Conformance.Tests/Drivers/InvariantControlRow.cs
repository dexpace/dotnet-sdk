// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>
/// One negative control for the invariant suite: an assertion, a face, and a deliberately broken <see cref="InvariantSubject"/>
/// that the assertion must catch (phase 10, plan convention 1). The tables of rows are static and declarative, so
/// <c>InvariantCatalogueTests</c> reads the assertion names they cover without any test having run first.
/// </summary>
internal sealed record InvariantControlRow(
    string Assertion,
    TransportFace Face,
    string Name,
    Func<InvariantSubject> Subject,
    ConformanceStatus Expected = ConformanceStatus.Failed)
{
    /// <summary>Short bounds so a hung control stays fast: the assertion's bound is six seconds, a wait inside it three.</summary>
    internal static SuiteOptions Options { get; } = new() { AssertionTimeout = TimeSpan.FromSeconds(6) };

    /// <summary>The distinct assertion names a table covers.</summary>
    internal static IReadOnlyCollection<string> CoveredBy(IEnumerable<InvariantControlRow> rows) =>
        [.. rows.Select(row => row.Assertion).Distinct(StringComparer.Ordinal)];

    /// <summary>The theory data of a table: assertion, face and control name.</summary>
    internal static TheoryData<string, TransportFace, string> Cases(IEnumerable<InvariantControlRow> rows)
    {
        var data = new TheoryData<string, TransportFace, string>();
        foreach (var row in rows)
        {
            data.Add(row.Assertion, row.Face, row.Name);
        }

        return data;
    }

    /// <summary>The theory data of the positive counterparts: every assertion a table covers, on each face it declares.</summary>
    internal static TheoryData<string, TransportFace> PositiveCases(IEnumerable<InvariantControlRow> rows, params string[] except)
    {
        var data = new TheoryData<string, TransportFace>();
        foreach (var name in CoveredBy(rows).Except(except, StringComparer.Ordinal))
        {
            foreach (var face in Find(name).Faces)
            {
                data.Add(name, face);
            }
        }

        return data;
    }

    /// <summary>The invariant assertion named <paramref name="name"/>.</summary>
    internal static ConformanceAssertion Find(string name) =>
        InvariantSuite.Assertions.SingleOrDefault(a => a.Name == name)
        ?? throw new InvalidOperationException($"There is no invariant assertion named '{name}'.");

    /// <summary>Runs one control and fails unless the assertion produced the expected status.</summary>
    internal static async Task RunAsync(IEnumerable<InvariantControlRow> rows, string assertion, TransportFace face, string name)
    {
        var row = rows.Single(r => r.Assertion == assertion && r.Face == face && r.Name == name);
        var result = await InvariantSuite.RunAsync(row.Subject(), Find(assertion), face, Options, TestContext.Current.CancellationToken);

        Assert.True(
            result.Status == row.Expected,
            $"control '{name}' on {face} expected {row.Expected} but the assertion reported {result.Status}: {result.Detail}");
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
    }

    /// <summary>Runs the real assertion on one face against <paramref name="subject"/> (the defaults when null) and fails unless it passed.</summary>
    internal static async Task RunPositiveAsync(string assertion, TransportFace face, InvariantSubject? subject = null)
    {
        var result = await InvariantSuite.RunAsync(subject ?? new InvariantSubject(), Find(assertion), face, Options, TestContext.Current.CancellationToken);

        Assert.True(result.Status == ConformanceStatus.Passed, $"{assertion} on {face} against {subject?.Name ?? "the defaults"}: {result.Status}: {result.Detail}");
    }
}
