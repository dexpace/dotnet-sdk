// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The packaging conformance suite (phase 10, design section E): eight assertions over the <c>NFR</c> requirements that are properties of the
/// published packages (<c>NFR-1</c> to <c>-3</c>, <c>-8</c> to <c>-10</c>, <c>-13</c> to <c>-15</c>), run against a <see cref="PackagingSubject"/>.
/// It reads the <c>.nupkg</c> files with the framework's own zip, XML and metadata readers (no package is added) and never loads a packed assembly,
/// so it runs as a CI step after <c>dotnet pack</c> and a third party can run it over their own packages.
/// </summary>
/// <remarks>
/// It is synchronous: nothing in it waits. Every assertion judges a value, so its face is <see cref="TransportFace.None"/>. The dependency audit
/// (<c>scripts/ci/dependency-audit.cs</c>) stays beside it: it checks the closure of the build output, this suite the metadata that was published
/// (two checks, two subjects).
/// </remarks>
public static class PackagingSuite
{
    /// <summary>What a green run of the packaging suite does not prove.</summary>
    public static string Preamble { get; } = PackagingIdentity.Preamble;

    /// <summary>Every assertion, in a stable order. Names are stable public identifiers, frozen at the first shipped surface.</summary>
    public static IReadOnlyList<ConformanceAssertion> Assertions => PackagingCatalogue.All;

    /// <summary>Runs one assertion over <paramref name="subject"/>.</summary>
    /// <param name="subject">The packages under test.</param>
    /// <param name="assertion">One of <see cref="Assertions"/>.</param>
    /// <param name="options">Waivers; defaults when <see langword="null"/>. The assertion bound does not apply: nothing here waits.</param>
    /// <returns>The result. A missing or corrupt package is an errored or failed result, not an exception.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> or <paramref name="assertion"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="assertion"/> is not one of <see cref="Assertions"/>.</exception>
    public static ConformanceResult Run(PackagingSubject subject, ConformanceAssertion assertion, SuiteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(assertion);
        if (!Assertions.Contains(assertion))
        {
            throw new ArgumentException($"Assertion '{assertion.Name}' is not a packaging assertion.", nameof(assertion));
        }

        return RunWith(subject, assertion, options ?? new SuiteOptions(), new Lazy<IReadOnlyList<PackageFacts>>(() => PackagingContext.Load(subject)));
    }

    /// <summary>Runs every assertion, in order, and builds the report. The packages are read once and shared by the run.</summary>
    /// <param name="subject">The packages under test.</param>
    /// <param name="options">Waivers; defaults when <see langword="null"/>.</param>
    /// <returns>The report; its <see cref="ConformanceReport.IsGreen"/> is the verdict.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="subject"/> is null.</exception>
    public static ConformanceReport RunAll(PackagingSubject subject, SuiteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var effective = options ?? new SuiteOptions();
        var packages = new Lazy<IReadOnlyList<PackageFacts>>(() => PackagingContext.Load(subject));
        return SuiteRunnerCore.RunAllSync(
            subject.Name,
            PackagingIdentity.Identity,
            Assertions,
            (assertion, _) => RunWith(subject, assertion, effective, packages),
            effective.Waivers);
    }

    private static ConformanceResult RunWith(PackagingSubject subject, ConformanceAssertion assertion, SuiteOptions options, Lazy<IReadOnlyList<PackageFacts>> packages) =>
        SuiteRunnerCore.RunSync(assertion, TransportFace.None, () => new PackagingContext(subject, packages), options.Waivers);
}
