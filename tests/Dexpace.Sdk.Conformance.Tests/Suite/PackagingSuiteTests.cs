// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Suite;

/// <summary>Plan 3.1 (P10-10): the packaging suite's shell.</summary>
[Trait("Category", "Unit")]
public sealed class PackagingSuiteTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("packaging-suite");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void The_preamble_says_a_package_built_from_another_tree_is_a_different_subject()
    {
        Assert.Contains("it reads the packages it was given; a package built from another tree is a different subject", PackagingSuite.Preamble, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_assertion_is_a_face_less_value_check()
    {
        Assert.All(PackagingSuite.Assertions, a => Assert.Equal([TransportFace.None], a.Faces));
    }

    [Fact]
    public void A_conforming_set_is_green_with_the_packaging_suite_recorded()
    {
        var report = PackagingSuite.RunAll(NupkgBuilder.Subject(NupkgBuilder.WriteAll(_directory.FullName, NupkgBuilder.ConformingSet())));

        Assert.True(report.IsGreen, report.ToString());
        Assert.Equal("packaging", report.Suite);
        Assert.Equal(PackagingSuite.Preamble, report.PreambleText);
        Assert.Equal(PackagingSuite.Assertions.Count, report.Results.Count(r => r.Status == ConformanceStatus.Passed));
        Assert.Equal("synthetic packages", report.SubjectName);
    }

    [Fact]
    public void A_missing_package_is_an_errored_result_naming_the_file_not_an_exception()
    {
        var subject = NupkgBuilder.Subject([Path.Combine(_directory.FullName, "Absent.1.0.0.nupkg")]);

        var report = PackagingSuite.RunAll(subject);

        Assert.False(report.IsGreen);
        Assert.All(report.Results, r => Assert.Equal(ConformanceStatus.Errored, r.Status));
        Assert.All(report.Results, r => Assert.Contains("Absent.1.0.0.nupkg", r.Detail, StringComparison.Ordinal));
    }

    [Fact]
    public void A_subject_with_no_package_fails_every_assertion()
    {
        var report = PackagingSuite.RunAll(new PackagingSubject());

        Assert.All(report.Results, r => Assert.Equal(ConformanceStatus.Failed, r.Status));
        Assert.Contains("no package to judge", report.Results[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_set_without_the_core_package_fails_the_core_assertion_naming_it()
    {
        var paths = NupkgBuilder.WriteAll(_directory.FullName, NupkgBuilder.ConformingSet().Where(spec => spec.Id != "Dexpace.Sdk.Core"));

        var result = PackagingSuite.Run(NupkgBuilder.Subject(paths), PackagingSuite.Assertions[0]);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("Dexpace.Sdk.Core", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_waiver_turns_a_failure_into_Waived_and_a_stale_waiver_into_a_failure()
    {
        var broken = NupkgBuilder.ConformingSet().Select(spec => spec.Id == "Dexpace.Sdk.Core" ? spec with { License = null } : spec);
        var subject = NupkgBuilder.Subject(NupkgBuilder.WriteAll(_directory.FullName, broken));
        var waiver = new ConformanceWaiver("NFR-13", "a known gap") { Assertion = "nfr-13.license-declared" };

        var waived = PackagingSuite.Run(subject, PackagingSuite.Assertions.Single(a => a.Name == "nfr-13.license-declared"), new SuiteOptions { Waivers = [waiver] });
        var stale = PackagingSuite.RunAll(NupkgBuilder.Subject(NupkgBuilder.WriteAll(Directory.CreateTempSubdirectory("conforming").FullName, NupkgBuilder.ConformingSet())), new SuiteOptions { Waivers = [waiver] });

        Assert.Equal(ConformanceStatus.Waived, waived.Status);
        Assert.Contains(stale.Results, r => r.Status == ConformanceStatus.Failed && r.Detail.Contains("no longer needed", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_and_RunAll_reject_null_and_a_foreign_assertion()
    {
        var subject = new PackagingSubject();

        Assert.Throws<ArgumentNullException>(() => PackagingSuite.Run(null!, PackagingSuite.Assertions[0]));
        Assert.Throws<ArgumentNullException>(() => PackagingSuite.Run(subject, null!));
        Assert.Throws<ArgumentNullException>(() => PackagingSuite.RunAll(null!));
        Assert.Throws<ArgumentException>(() => PackagingSuite.Run(subject, TransportSuite.Assertions[0]));
    }

    [Fact]
    public void The_subject_copies_its_lists_and_rejects_null_and_blank()
    {
        var paths = new List<string> { "a.nupkg" };
        var subject = new PackagingSubject { PackagePaths = paths, Policies = new Dictionary<string, PackagePolicy> { ["x"] = new(null, []) } };
        paths.Clear();

        Assert.Single(subject.PackagePaths);
        Assert.Equal("Dexpace.Sdk.Core", subject.CoreId);
        Assert.Equal("net10.0", subject.DeclaredFloor);
        Assert.Throws<ArgumentNullException>(() => new PackagingSubject { PackagePaths = null! });
        Assert.Throws<ArgumentNullException>(() => new PackagingSubject { Policies = null! });
        Assert.Throws<ArgumentException>(() => new PackagingSubject { CoreId = " " });
        Assert.Throws<ArgumentException>(() => new PackagingSubject { DeclaredFloor = "" });
        Assert.Throws<ArgumentException>(() => new PackagingSubject { Name = " " });
    }
}
