// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Core.Tests.Architecture;

/// <summary>
/// NFR-4's source half (design §9.1; phase 10, plan 3.7): every library under <c>src/</c> keeps both public-API files, so the analyzers
/// (<c>RS0016</c> for an undeclared public member, <c>RS0017</c> for a removed one) have a snapshot to compare against, and
/// <c>PublicAPI.Shipped.txt</c> holds the surface (phase 12 moved <c>Unshipped</c> into it for the first release: <c>#nullable enable</c> first, then every
/// member), while <c>PublicAPI.Unshipped.txt</c> is not asserted empty (a later change legitimately fills it; "settled at a tag" is the release preflight's rule,
/// <c>scripts/ci/release-preflight.cs</c>). <c>PackageValidationBaselineVersion</c> is held until the first publish. The drift gate itself is the build: V4 added a
/// public type and got three <c>RS0016</c> errors.
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
    public void Shipped_holds_the_surface_and_starts_with_nullable_enable()
    {
        var offenders = ShippedWithoutSurface(SourceRoot());

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

            var headerless = Directory.CreateDirectory(Path.Combine(root.FullName, "Headerless"));
            File.WriteAllText(Path.Combine(headerless.FullName, "PublicAPI.Shipped.txt"), "Some.Type\n");
            File.WriteAllText(Path.Combine(headerless.FullName, "PublicAPI.Unshipped.txt"), "#nullable enable\n");
            var empty = Directory.CreateDirectory(Path.Combine(root.FullName, "Empty"));
            File.WriteAllText(Path.Combine(empty.FullName, "PublicAPI.Shipped.txt"), string.Empty);
            File.WriteAllText(Path.Combine(empty.FullName, "PublicAPI.Unshipped.txt"), "#nullable enable\n");

            Assert.Equal(["Lacking/PublicAPI.Unshipped.txt"], MissingFiles(root.FullName));

            // Both holds the header alone, Empty holds nothing and Headerless has no header line: none holds the surface; Lacking does.
            Assert.Equal(
                ["Both/PublicAPI.Shipped.txt", "Empty/PublicAPI.Shipped.txt", "Headerless/PublicAPI.Shipped.txt"],
                ShippedWithoutSurface(root.FullName));
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

    private static List<string> ShippedWithoutSurface(string root) =>
        [.. Directory.GetDirectories(root)
            .Select(static library => (Library: Path.GetFileName(library), Path: Path.Combine(library, "PublicAPI.Shipped.txt")))
            .Where(static entry => File.Exists(entry.Path) && !HoldsTheSurface(File.ReadAllLines(entry.Path)))
            .Select(static entry => $"{entry.Library}/PublicAPI.Shipped.txt")
            .Order(StringComparer.Ordinal)];

    private static bool HoldsTheSurface(string[] lines) =>
        lines.Length > 0
        && lines[0].Trim() == "#nullable enable"
        && lines.Skip(1).Any(static line => !string.IsNullOrWhiteSpace(line));

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
