// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// NFR-4's source half (design §9.1; phase 10, plan 3.7): every library under <c>src/</c> keeps both public-API files, so the analyzers
/// (<c>RS0016</c> for an undeclared public member, <c>RS0017</c> for a removed one) have a snapshot to compare against, and
/// <c>PublicAPI.Shipped.txt</c> stays empty (the <c>#nullable enable</c> line alone) until the first release, when phase 12 fills it and sets
/// <c>PackageValidationBaselineVersion</c>. The drift gate itself is the build: V4 added a public type and got three <c>RS0016</c> errors.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NFR4ArchitectureTests
{
    private static readonly string[] s_files = ["PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt"];

    [Fact]
    public void Every_library_has_both_public_api_files()
    {
        var missing = MissingFiles(SourceRoot());

        Assert.Empty(missing);
    }

    [Fact]
    public void Shipped_is_empty_or_nullable_enable_only_until_phase_12()
    {
        var offenders = NonEmptyShipped(SourceRoot());

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_scan_sees_all_five_libraries()
    {
        var libraries = Directory.GetDirectories(SourceRoot()).Select(static d => Path.GetFileName(d)).Order(StringComparer.Ordinal);

        Assert.Equal(
            [
                "Dexpace.Sdk.Conformance",
                "Dexpace.Sdk.Core",
                "Dexpace.Sdk.Extensions.DependencyInjection",
                "Dexpace.Sdk.Http.SystemNet",
                "Dexpace.Sdk.Serialization.SystemTextJson",
            ],
            libraries);
    }

    [Fact]
    public void The_scan_can_fail()
    {
        var root = Directory.CreateTempSubdirectory("nfr4");
        try
        {
            var both = Directory.CreateDirectory(Path.Combine(root.FullName, "Both"));
            File.WriteAllText(Path.Combine(both.FullName, "PublicAPI.Shipped.txt"), "#nullable enable\n");
            File.WriteAllText(Path.Combine(both.FullName, "PublicAPI.Unshipped.txt"), "#nullable enable\n");
            var lacking = Directory.CreateDirectory(Path.Combine(root.FullName, "Lacking"));
            File.WriteAllText(Path.Combine(lacking.FullName, "PublicAPI.Shipped.txt"), "#nullable enable\nSome.Type\n");

            Assert.Equal(["Lacking/PublicAPI.Unshipped.txt"], MissingFiles(root.FullName));
            Assert.Equal(["Lacking/PublicAPI.Shipped.txt"], NonEmptyShipped(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static List<string> MissingFiles(string root) =>
        [.. Directory.GetDirectories(root)
            .SelectMany(static library => s_files.Select(file => (Library: Path.GetFileName(library), File: file, Path: Path.Combine(library, file))))
            .Where(static entry => !File.Exists(entry.Path))
            .Select(static entry => $"{entry.Library}/{entry.File}")
            .Order(StringComparer.Ordinal)];

    private static List<string> NonEmptyShipped(string root) =>
        [.. Directory.GetDirectories(root)
            .Select(static library => (Library: Path.GetFileName(library), Path: Path.Combine(library, "PublicAPI.Shipped.txt")))
            .Where(static entry => File.Exists(entry.Path)
                && File.ReadAllLines(entry.Path).Any(static line => !string.IsNullOrWhiteSpace(line) && line.Trim() != "#nullable enable"))
            .Select(static entry => $"{entry.Library}/PublicAPI.Shipped.txt")
            .Order(StringComparer.Ordinal)];

    private static string SourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dexpace.Sdk.sln")))
            {
                return Path.Combine(directory.FullName, "src");
            }
        }

        throw new DirectoryNotFoundException("Dexpace.Sdk.sln was not found above the test output.");
    }
}
