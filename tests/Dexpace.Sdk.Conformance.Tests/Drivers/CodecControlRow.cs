// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Controls;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>One negative control for the codec suite: an assertion, a face, and a deliberately broken codec (a <see cref="ReferenceSerde"/> with one <see cref="SerdeFlaw"/>) that the assertion must catch.</summary>
internal sealed record CodecControlRow(string Assertion, TransportFace Face, string Name, SerdeFlaw Flaw, string Because)
{
    /// <summary>Short bounds so a control stays fast.</summary>
    internal static SuiteOptions Options { get; } = new() { AssertionTimeout = TimeSpan.FromSeconds(10) };

    /// <summary>The codec subject over a reference codec with <paramref name="flaw"/>.</summary>
    /// <param name="flaw">What the codec gets wrong.</param>
    internal static CodecSubject Subject(SerdeFlaw flaw = SerdeFlaw.None) =>
        new() { Name = "reference codec " + flaw, CreateSerde = () => new ReferenceSerde(flaw), ExpectedMediaType = Dexpace.Sdk.Core.Http.Common.CommonMediaTypes.ApplicationJson };

    /// <summary>The same row on every face the assertion declares.</summary>
    /// <param name="assertion">The assertion's name.</param>
    /// <param name="name">What the codec gets wrong, in words.</param>
    /// <param name="flaw">The flaw.</param>
    /// <param name="because">The text the failure must contain.</param>
    internal static CodecControlRow[] OnEveryFace(string assertion, string name, SerdeFlaw flaw, string because) =>
        [.. Find(assertion).Faces.Select(face => new CodecControlRow(assertion, face, name, flaw, because))];

    /// <summary>The distinct assertion names a table covers.</summary>
    internal static IReadOnlyCollection<string> CoveredBy(IEnumerable<CodecControlRow> rows) =>
        [.. rows.Select(row => row.Assertion).Distinct(StringComparer.Ordinal)];

    /// <summary>The theory data of a table: assertion, face and control name.</summary>
    internal static TheoryData<string, TransportFace, string> Cases(IEnumerable<CodecControlRow> rows)
    {
        var data = new TheoryData<string, TransportFace, string>();
        foreach (var row in rows)
        {
            data.Add(row.Assertion, row.Face, row.Name);
        }

        return data;
    }

    /// <summary>The theory data of the positive counterparts: every assertion a table covers, on each face it declares.</summary>
    internal static TheoryData<string, TransportFace> PositiveCases(IEnumerable<CodecControlRow> rows)
    {
        var data = new TheoryData<string, TransportFace>();
        foreach (var name in CoveredBy(rows))
        {
            foreach (var face in Find(name).Faces)
            {
                data.Add(name, face);
            }
        }

        return data;
    }

    /// <summary>The codec assertion named <paramref name="name"/>.</summary>
    internal static ConformanceAssertion Find(string name) =>
        CodecSuite.Assertions.SingleOrDefault(a => a.Name == name)
        ?? throw new InvalidOperationException($"There is no codec assertion named '{name}'.");

    /// <summary>Runs one control and fails unless the assertion failed for the row's reason.</summary>
    internal static async Task RunAsync(IEnumerable<CodecControlRow> rows, string assertion, TransportFace face, string name)
    {
        var row = rows.Single(r => r.Assertion == assertion && r.Face == face && r.Name == name);
        var result = await CodecSuite.RunAsync(Subject(row.Flaw), Find(assertion), face, Options, TestContext.Current.CancellationToken);

        Assert.True(result.Status == ConformanceStatus.Failed, $"control '{name}' on {face} expected Failed but the assertion reported {result.Status}: {result.Detail}{Trace(result)}");
        Assert.True(
            result.Detail.Contains(row.Because, StringComparison.OrdinalIgnoreCase),
            $"control '{name}' on {face} failed, but not for the expected reason '{row.Because}': {result.Detail}");
    }

    /// <summary>Runs the real assertion on one face against a conforming reference codec and fails unless it passed.</summary>
    internal static async Task RunPositiveAsync(string assertion, TransportFace face)
    {
        var result = await CodecSuite.RunAsync(Subject(), Find(assertion), face, Options, TestContext.Current.CancellationToken);

        Assert.True(result.Status == ConformanceStatus.Passed, $"{assertion} on {face} against the conforming reference codec: {result.Status}: {result.Detail}{Trace(result)}");
    }

    private static string Trace(ConformanceResult result) =>
        result.Status is ConformanceStatus.Errored && result.Exception is { } exception ? Environment.NewLine + exception : string.Empty;
}
