// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>
/// One negative control: an assertion, a face, and a deliberately broken subject that the assertion must catch. The tables of
/// rows are static and declarative (plan 2.4): a class lists them, and <c>AssertionCatalogueTests</c> reads the assertion
/// names they cover without any test having run first.
/// </summary>
internal sealed record ControlRow(
    string Assertion,
    TransportFace Face,
    string Name,
    Func<TransportSubject> Subject,
    ConformanceStatus Expected = ConformanceStatus.Failed,
    TransportSuiteOptions? Options = null)
{
    /// <summary>A control over a broken transport (<paramref name="broken"/> builds a fresh one per run).</summary>
    internal static ControlRow Over(string assertion, TransportFace face, string name, Func<IAsyncHttpClient> broken, ConformanceStatus expected = ConformanceStatus.Failed, TransportSuiteOptions? options = null) =>
        new(
            assertion,
            face,
            name,
            () => new TransportSubject { Name = "broken: " + name, CreateAsync = _ => broken(), CreateBlocking = _ => broken().AsBlocking() },
            expected,
            options);

    /// <summary>The distinct assertion names a table covers.</summary>
    internal static IReadOnlyCollection<string> CoveredBy(IEnumerable<ControlRow> rows) => [.. rows.Select(row => row.Assertion).Distinct(StringComparer.Ordinal)];

    /// <summary>The theory data of a table: assertion, face and control name.</summary>
    internal static TheoryData<string, TransportFace, string> Cases(IEnumerable<ControlRow> rows)
    {
        var data = new TheoryData<string, TransportFace, string>();
        foreach (var row in rows)
        {
            data.Add(row.Assertion, row.Face, row.Name);
        }

        return data;
    }

    /// <summary>
    /// The theory data of the positive counterparts: every assertion a table covers, on each face it declares, except the ones
    /// the raw-socket client is documented not to satisfy (it is waived for them in its driver).
    /// </summary>
    internal static TheoryData<string, TransportFace> PositiveCases(IEnumerable<ControlRow> rows, params string[] except)
    {
        var data = new TheoryData<string, TransportFace>();
        foreach (var name in CoveredBy(rows).Except(except, StringComparer.Ordinal))
        {
            foreach (var face in AssertionControl.Find(name).Faces)
            {
                data.Add(name, face);
            }
        }

        return data;
    }

    /// <summary>Runs one control and fails unless the assertion produced the expected status.</summary>
    internal static async Task RunAsync(IEnumerable<ControlRow> rows, string assertion, TransportFace face, string name)
    {
        var row = rows.Single(r => r.Assertion == assertion && r.Face == face && r.Name == name);
        var result = await AssertionControl.RunAsync(assertion, face, row.Subject(), row.Options);

        Assert.True(
            result.Status == row.Expected,
            $"control '{name}' on {face} expected {row.Expected} but the assertion reported {result.Status}: {result.Detail}");
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
    }

    /// <summary>Runs the real assertion on one face against the raw-socket client and fails unless it passed.</summary>
    internal static async Task RunPositiveAsync(string assertion, TransportFace face)
    {
        var result = await AssertionControl.RunAsync(assertion, face, RawSocketSubject.Create(), RawSocketSubject.Options);

        Assert.True(result.Status == ConformanceStatus.Passed, $"{assertion} on {face} against the raw-socket client: {result.Status}: {result.Detail}");
    }
}
