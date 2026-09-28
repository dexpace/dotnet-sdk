// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// NFR-5's gate (design §9.3): aggregate line coverage over the library assemblies, across every test project.
//
//   dotnet run scripts/ci/coverage-gate.cs -- <results-dir> <minimum-percent>
//
// coverlet's data collector writes one Cobertura file per test project and cannot enforce a threshold itself
// (only coverlet.msbuild and coverlet.console can, and only per test project). This merges every
// coverage.cobertura.xml under <results-dir>: a line of a library counts as covered when any test project
// covered it, so a library exercised by two suites is measured once. Only assemblies built from src/ count;
// test projects, the AOT smoke consumer and repository tools are never measured. Exit 0 at or above the
// minimum, 1 below it, 2 on a usage or input error.

#:property RestorePackagesWithLockFile=false
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

if (args.Length != 2 || !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minimum))
{
    Console.Error.WriteLine("usage: coverage-gate.cs <results-dir> <minimum-percent>");
    return 2;
}

var reports = Directory.Exists(args[0])
    ? Directory.GetFiles(args[0], "coverage.cobertura.xml", SearchOption.AllDirectories)
    : [];
if (reports.Length == 0)
{
    Console.Error.WriteLine($"coverage-gate: no coverage.cobertura.xml under {args[0]}");
    return 2;
}

var libraries = Directory.GetDirectories("src").Select(Path.GetFileName).OfType<string>().ToHashSet(StringComparer.Ordinal);
var lines = new Dictionary<(string Assembly, string File, int Line), bool>();
foreach (var report in reports)
{
    foreach (var package in XDocument.Load(report).Descendants("package"))
    {
        var assembly = (string?)package.Attribute("name") ?? string.Empty;
        if (!libraries.Contains(assembly))
        {
            continue;
        }

        foreach (var cls in package.Descendants("class"))
        {
            var file = ((string?)cls.Attribute("filename") ?? string.Empty).Replace('\\', '/');
            foreach (var line in cls.Elements("lines").Elements("line"))
            {
                var key = (assembly, file, (int)line.Attribute("number")!);
                var hit = (long)line.Attribute("hits")! > 0;
                lines[key] = lines.TryGetValue(key, out var seen) ? seen || hit : hit;
            }
        }
    }
}

if (lines.Count == 0)
{
    Console.Error.WriteLine("coverage-gate: the reports contain no library assembly from src/");
    return 2;
}

Console.WriteLine($"coverage-gate: {reports.Length} report(s), line coverage per library:");
foreach (var group in lines.GroupBy(pair => pair.Key.Assembly).OrderBy(group => group.Key, StringComparer.Ordinal))
{
    var covered = group.Count(pair => pair.Value);
    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"  {group.Key,-45} {covered,6}/{group.Count(),-6} {100.0 * covered / group.Count(),6:F2}%"));
}

var total = 100.0 * lines.Count(pair => pair.Value) / lines.Count;
var verdict = total >= minimum ? "ok" : "FAILED";
Console.WriteLine(string.Create(
    CultureInfo.InvariantCulture,
    $"coverage-gate: aggregate {total:F2}% against a floor of {minimum:F2}%: {verdict}"));
return total >= minimum ? 0 : 1;
