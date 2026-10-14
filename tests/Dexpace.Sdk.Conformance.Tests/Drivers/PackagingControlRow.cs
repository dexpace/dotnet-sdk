// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Drivers;

/// <summary>One negative control for the packaging suite: an assertion, and a synthetic set of packages with one thing wrong that the assertion must catch.</summary>
internal sealed record PackagingControlRow(string Assertion, string Name, Func<PackageSpec[], PackageSpec[]> Break, string Because)
{
    /// <summary>The distinct assertion names a table covers.</summary>
    internal static IReadOnlyCollection<string> CoveredBy(IEnumerable<PackagingControlRow> rows) =>
        [.. rows.Select(row => row.Assertion).Distinct(StringComparer.Ordinal)];

    /// <summary>The theory data of a table: assertion and control name.</summary>
    internal static TheoryData<string, string> Cases(IEnumerable<PackagingControlRow> rows)
    {
        var data = new TheoryData<string, string>();
        foreach (var row in rows)
        {
            data.Add(row.Assertion, row.Name);
        }

        return data;
    }

    /// <summary>The theory data of the positive counterparts: every assertion a table covers.</summary>
    internal static TheoryData<string> PositiveCases(IEnumerable<PackagingControlRow> rows)
    {
        var data = new TheoryData<string>();
        foreach (var name in CoveredBy(rows))
        {
            data.Add(name);
        }

        return data;
    }

    /// <summary>The packaging assertion named <paramref name="name"/>.</summary>
    internal static ConformanceAssertion Find(string name) =>
        PackagingSuite.Assertions.SingleOrDefault(a => a.Name == name)
        ?? throw new InvalidOperationException($"There is no packaging assertion named '{name}'.");

    /// <summary>Replaces the package with id <paramref name="id"/> by <paramref name="edit"/> of it.</summary>
    /// <param name="specs">The set.</param>
    /// <param name="id">The package to change.</param>
    /// <param name="edit">The change.</param>
    internal static PackageSpec[] Edit(PackageSpec[] specs, string id, Func<PackageSpec, PackageSpec> edit) =>
        [.. specs.Select(spec => spec.Id == id ? edit(spec) : spec)];

    /// <summary>Runs one control and fails unless the assertion failed for the row's reason.</summary>
    internal static void Run(IEnumerable<PackagingControlRow> rows, string assertion, string name)
    {
        var row = rows.Single(r => r.Assertion == assertion && r.Name == name);
        var directory = Directory.CreateTempSubdirectory("nupkg");
        try
        {
            var subject = NupkgBuilder.Subject(NupkgBuilder.WriteAll(directory.FullName, row.Break(NupkgBuilder.ConformingSet())));

            var result = PackagingSuite.Run(subject, Find(assertion));

            Assert.True(result.Status == ConformanceStatus.Failed, $"control '{name}' expected Failed but the assertion reported {result.Status}: {result.Detail}");
            Assert.True(
                result.Detail.Contains(row.Because, StringComparison.OrdinalIgnoreCase),
                $"control '{name}' failed, but not for the expected reason '{row.Because}': {result.Detail}");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Runs the real assertion over the conforming synthetic set and fails unless it passed.</summary>
    internal static void RunPositive(string assertion)
    {
        var directory = Directory.CreateTempSubdirectory("nupkg");
        try
        {
            var result = PackagingSuite.Run(NupkgBuilder.Subject(NupkgBuilder.WriteAll(directory.FullName, NupkgBuilder.ConformingSet())), Find(assertion));

            Assert.True(result.Status == ConformanceStatus.Passed, $"{assertion} over the conforming set: {result.Status}: {result.Detail}");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
