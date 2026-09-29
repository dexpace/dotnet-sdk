// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// NFR-5's gate (design §9.3): aggregate line coverage over the library assemblies, across every test project.
//
//   dotnet run scripts/ci/coverage-gate.cs -- <results-dir> <minimum-percent>
//
// Run from the repository root. coverlet's Microsoft.Testing.Platform extension (coverlet.MTP) writes one Cobertura
// file per test project and enforces no threshold across projects. Directory.Build.targets gives every test project
// the report name <TestProject>.coverage.cobertura.<timestamp>.xml, since coverlet's own name is a millisecond
// timestamp two test hosts can share (issue #33). This merges every *coverage.cobertura*.xml under <results-dir>: a
// line of a library counts as covered when any test project covered it, so a library exercised by two suites is
// measured once. Only the libraries count: test projects, Dexpace.Sdk.TestSupport, the AOT smoke consumer and
// repository tools are never measured.
//
// The gate fails closed. The expected sets come from the repository, not from the reports: every test project
// (tests/*/*.csproj declaring IsTestProject true) must have left exactly one report, and every library
// (src/*/*.csproj not declaring IsPackable false, by its AssemblyName or else its file name) must have coverage data
// in one, so a lost report, a stale one, or a new package no suite measures, cannot move the aggregate unnoticed.
//
// Exit 0 at or above the minimum, 1 below it, 2 on a usage or input error, 3 when a test project's report is missing
// or duplicated, or a library's coverage data is missing.

#:property RestorePackagesWithLockFile=false
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

const string ReportPattern = "*coverage.cobertura*.xml";

if (args.Length != 2 || !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minimum))
{
    Console.Error.WriteLine("usage: coverage-gate.cs <results-dir> <minimum-percent>");
    return 2;
}

if (!Directory.Exists("src") || !Directory.Exists("tests"))
{
    Console.Error.WriteLine("coverage-gate: run from the repository root (src/ and tests/ not found)");
    return 2;
}

var reports = Directory.Exists(args[0])
    ? Directory.GetFiles(args[0], ReportPattern, SearchOption.AllDirectories)
    : [];
if (reports.Length == 0)
{
    Console.Error.WriteLine($"coverage-gate: no {ReportPattern} under {args[0]}");
    return 2;
}

var libraries = Projects("src").Where(project => Property(project, "IsPackable") != "false")
    .Select(project => Property(project, "AssemblyName") ?? Path.GetFileNameWithoutExtension(project))
    .ToHashSet(StringComparer.Ordinal);
var testProjects = Projects("tests").Where(project => Property(project, "IsTestProject") == "true")
    .Select(Path.GetFileNameWithoutExtension).OfType<string>().ToList();
if (libraries.Count == 0)
{
    Console.Error.WriteLine("coverage-gate: no library project under src/");
    return 2;
}

var lines = new Dictionary<(string Assembly, string File, int Line), bool>();
foreach (var report in reports)
{
    Merge(XDocument.Load(report), libraries, lines);
}

Console.WriteLine($"coverage-gate: {reports.Length} report(s), line coverage per library:");
foreach (var group in lines.GroupBy(pair => pair.Key.Assembly).OrderBy(group => group.Key, StringComparer.Ordinal))
{
    var covered = group.Count(pair => pair.Value);
    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"  {group.Key,-45} {covered,6}/{group.Count(),-6} {100.0 * covered / group.Count(),6:F2}%"));
}

// Fail closed before any verdict on the floor: an aggregate over a subset of the libraries or suites is no measure,
// and neither is one inflated by a stale report of an earlier run (or a second target framework) beside the fresh one.
var reportsOf = testProjects.ToDictionary(
    project => project,
    project => reports.Where(report =>
            Path.GetFileName(report).StartsWith($"{project}.coverage.cobertura.", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal).ToList(),
    StringComparer.Ordinal);
var missing = testProjects
    .Where(project => reportsOf[project].Count == 0)
    .Select(project => $"test project {project} left no report ({project}.coverage.cobertura.*.xml)")
    .Concat(testProjects.Where(project => reportsOf[project].Count > 1).Select(project =>
        $"test project {project} has {reportsOf[project].Count} reports, expected one (clear the results directory " +
        $"before the run): {string.Join(", ", reportsOf[project].Select(Path.GetFileName))}"))
    .Concat(libraries.Except(lines.Keys.Select(key => key.Assembly)).Order(StringComparer.Ordinal)
        .Select(library => $"library {library} has no coverage data in any report"))
    .ToList();
if (missing.Count > 0)
{
    foreach (var gap in missing)
    {
        Console.Error.WriteLine($"coverage-gate: {gap}");
    }

    Console.Error.WriteLine("coverage-gate: coverage is incomplete or ambiguous, so the floor is not checked: FAILED");
    return 3;
}

var total = 100.0 * lines.Count(pair => pair.Value) / lines.Count;
var verdict = total >= minimum ? "ok" : "FAILED";
Console.WriteLine(string.Create(
    CultureInfo.InvariantCulture,
    $"coverage-gate: aggregate {total:F2}% against a floor of {minimum:F2}%: {verdict}"));
return total >= minimum ? 0 : 1;

// The project files one level below root (src/<Project>/<Project>.csproj, tests/<Project>/<Project>.csproj).
static IEnumerable<string> Projects(string root) => Directory.GetDirectories(root)
    .SelectMany(directory => Directory.GetFiles(directory, "*.csproj"))
    .Order(StringComparer.Ordinal);

// A property's literal value in a project file, ignoring conditions; null when the file does not set it. Enough for
// the unconditional IsPackable, IsTestProject and AssemblyName this repository's project files declare.
static string? Property(string project, string name) =>
    XDocument.Load(project).Descendants().LastOrDefault(element => element.Name.LocalName == name)?.Value.Trim();

// Folds one report's library lines into the merged set: a line is covered when any report covered it.
static void Merge(XDocument document, HashSet<string> libraries, Dictionary<(string, string, int), bool> lines)
{
    var sources = document.Descendants("source").Select(source => source.Value).ToList();
    foreach (var package in document.Descendants("package"))
    {
        var assembly = (string?)package.Attribute("name") ?? string.Empty;
        if (!libraries.Contains(assembly))
        {
            continue;
        }

        foreach (var cls in package.Descendants("class"))
        {
            var file = SourcePath(sources, (string?)cls.Attribute("filename") ?? string.Empty);
            foreach (var line in cls.Elements("lines").Elements("line"))
            {
                var key = (assembly, file, (int)line.Attribute("number")!);
                var hit = (long)line.Attribute("hits")! > 0;
                lines[key] = lines.TryGetValue(key, out var seen) ? seen || hit : hit;
            }
        }
    }
}

// A class filename is relative to one of the report's <source> roots. The reports CI produces today share one root,
// but a report's root is derived from the files it instrumented, so two reports can differ, and then the same line
// would appear under two relative names. Resolving to the absolute path keeps the merge key the file itself either way.
static string SourcePath(List<string> sources, string filename)
{
    var resolved = Path.IsPathRooted(filename)
        ? filename
        : sources.Select(source => Path.Combine(source, filename)).FirstOrDefault(File.Exists)
            ?? Path.Combine(sources.FirstOrDefault() ?? string.Empty, filename);
    return Path.GetFullPath(resolved).Replace('\\', '/');
}
