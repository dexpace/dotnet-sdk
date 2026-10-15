// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The appendix-C coverage check (phase 10, P10-20; roadmap "every MUST dispositioned", docs/first-release.md).
//
//   dotnet run scripts/ci/requirement-coverage.cs -- --report              # print the tallies; always exit 0 on a readable tree
//   dotnet run scripts/ci/requirement-coverage.cs -- --check               # exit 1 on a finding in a MUST row
//   dotnet run scripts/ci/requirement-coverage.cs -- --check --root <dir>  # read <dir> instead of the current directory
//
// Appendix C (docs/product-spec/appendix-c-consolidated-normative-requirement-index.md) is the canonical list of every normative requirement
// with its level. Every docs/work/**/*-checklist.md is read for its OWNING rows: a row whose first cell is a requirement ID, in a table whose header
// starts `| ID | Level |`. Tables with any other header (`| ID (owner) | ... |`, `| ID | As built | ... |`) hold contributed or carried evidence and own nothing.
// Four findings, each tied to a row:
//   unowned  an appendix-C ID with no owning row;
//   unknown  an owning row naming an ID appendix C does not hold;
//   level    an owning row whose level cell disagrees with appendix C (`MUST NOT` folds into MUST);
//   mark     an owning row whose first three cells carry no legend mark (a tick, a no-entry sign, an hourglass, N/A or "vacuous");
// and one for carried IDs: an ID with owning rows in several checklists is resolved by phase order (the checklist's phaseN[x] directory); the latest row's
// mark is the ID's mark, and every earlier row must carry an hourglass (deferred to the later phase), or the pair is reported.
// Exit 0 when the check holds (or for --report), 1 on a finding in a MUST row, 2 on a usage or input error.

#:property RestorePackagesWithLockFile=false
#:property PublishAot=false

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

const string AppendixPath = "docs/product-spec/appendix-c-consolidated-normative-requirement-index.md";

var mode = "--report";
var root = Directory.GetCurrentDirectory();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--report" or "--check")
    {
        mode = args[i];
    }
    else if (args[i] == "--root" && i + 1 < args.Length)
    {
        root = Path.GetFullPath(args[++i]);
    }
    else
    {
        Console.Error.WriteLine("usage: requirement-coverage.cs [--report | --check] [--root <dir>]");
        return 2;
    }
}

var appendixFile = Path.Combine(root, AppendixPath);
var workDir = Path.Combine(root, "docs", "work");
if (!File.Exists(appendixFile) || !Directory.Exists(workDir))
{
    Console.Error.WriteLine($"requirement-coverage: {AppendixPath} or docs/work was not found under {root}");
    return 2;
}

var appendixRow = new Regex(@"^\| ([A-Z]+-\d+) \| (MUST NOT|MUST|SHOULD|MAY) \|", RegexOptions.CultureInvariant);
var levels = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var match in File.ReadLines(appendixFile).Select(line => appendixRow.Match(line)).Where(match => match.Success))
{
    levels[match.Groups[1].Value] = match.Groups[2].Value == "MUST NOT" ? "MUST" : match.Groups[2].Value;
}

var checklists = Directory.GetFiles(workDir, "*-checklist.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
if (levels.Count == 0 || checklists.Count == 0)
{
    Console.Error.WriteLine($"requirement-coverage: {(levels.Count == 0 ? "no requirement rows in appendix C" : "no checklist under docs/work")}");
    return 2;
}

var owning = new List<Owning>();
foreach (var file in checklists)
{
    owning.AddRange(OwningRows(file));
}

var findings = new List<Finding>();
var owners = owning.GroupBy(row => row.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.OrderBy(row => row.Phase).ThenBy(row => row.File, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

foreach (var id in levels.Keys.Where(id => !owners.ContainsKey(id)))
{
    findings.Add(new Finding("unowned", id, levels[id], "no checklist has an owning row for it"));
}

foreach (var (id, rows) in owners)
{
    if (!levels.TryGetValue(id, out var level))
    {
        findings.Add(new Finding("unknown", id, "MUST", $"{Relative(rows[0].File)} owns an ID that appendix C does not hold"));
        continue;
    }

    foreach (var row in rows)
    {
        if (Fold(row.Level) != level)
        {
            findings.Add(new Finding("level", id, level, $"{Relative(row.File)} says {row.Level}, appendix C says {level}"));
        }

        if (Mark(row.MarkText) is null)
        {
            findings.Add(new Finding("mark", id, level, $"{Relative(row.File)} carries no legend mark in its first three cells"));
        }
    }

    foreach (var earlier in rows.Take(rows.Count - 1).Where(row => !row.RowText.Contains('⏳', StringComparison.Ordinal)))
    {
        findings.Add(new Finding("carried", id, level, $"{Relative(earlier.File)} (phase {earlier.Phase}) is superseded by {Relative(rows[^1].File)} but does not carry an hourglass"));
    }
}

Print();
var failing = findings.Count(finding => finding.Level == "MUST");
Console.WriteLine(failing == 0
    ? "requirement-coverage: no finding in a MUST row"
    : $"requirement-coverage: {failing} finding(s) in MUST rows");
return mode == "--check" && failing > 0 ? 1 : 0;

string Relative(string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

// The level cell's leading word: a checklist annotates a split row ("MUST (+SHOULD log)", "SHOULD (embedded MUST)"), and the strength of the row is the first word.
static string Fold(string level)
{
    var word = Regex.Match(level.Trim(), @"^(MUST NOT|MUST|SHOULD|MAY)", RegexOptions.CultureInvariant);
    return !word.Success ? level.Trim() : word.Value == "MUST NOT" ? "MUST" : word.Value;
}

// The first legend mark in the text, in order of appearance, or null.
static string? Mark(string text)
{
    var best = (Index: int.MaxValue, Mark: (string?)null);
    foreach (var (token, mark) in new[] { ("✅", "✅"), ("🚫", "🚫"), ("⏳", "⏳"), ("N/A", "N/A"), ("vacuous", "N/A") })
    {
        var at = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (at >= 0 && at < best.Index)
        {
            best = (at, mark);
        }
    }

    return best.Mark;
}

// (phase number, sub-phase letter) from the nearest phaseN[x] directory; checklists outside one sort first.
static (int Number, string Letter) PhaseOf(string file)
{
    foreach (var directory in (Path.GetDirectoryName(file) ?? string.Empty).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Reverse())
    {
        var match = Regex.Match(directory, @"^phase(\d+)([a-z]?)$", RegexOptions.CultureInvariant);
        if (match.Success)
        {
            return (int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), match.Groups[2].Value);
        }
    }

    return (0, string.Empty);
}

static IEnumerable<Owning> OwningRows(string file)
{
    var phase = PhaseOf(file);
    var inOwning = false;
    var previousWasTable = false;
    foreach (var line in File.ReadLines(file))
    {
        var isTable = line.StartsWith('|');
        if (isTable && !previousWasTable)
        {
            inOwning = Regex.IsMatch(line, @"^\|\s*ID\s*\|\s*Level\s*\|", RegexOptions.CultureInvariant);
        }

        previousWasTable = isTable;
        if (!isTable || !inOwning)
        {
            continue;
        }

        var cells = line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
        var id = cells[0].Trim('`', ' ', '*');
        if (cells.Length >= 3 && Regex.IsMatch(id, @"^[A-Z]+-\d+$", RegexOptions.CultureInvariant))
        {
            yield return new Owning(id, cells[1], string.Join(' ', cells.Take(3)), line, file, phase);
        }
    }
}

void Print()
{
    var owned = owners.Keys.Where(levels.ContainsKey).ToList();
    Console.WriteLine($"requirement-coverage: {levels.Count} IDs in appendix C ({levels.Values.Count(l => l == "MUST")} MUST, {levels.Values.Count(l => l == "SHOULD")} SHOULD, {levels.Values.Count(l => l == "MAY")} MAY); {checklists.Count} checklists read");
    Console.WriteLine($"requirement-coverage: {owned.Count} of {levels.Count} IDs owned by a checklist row ({owners.Count(pair => pair.Value.Count > 1)} carried across checklists)");
    foreach (var prefix in levels.Keys.Select(id => id[..id.LastIndexOf('-')]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
    {
        var ids = levels.Keys.Where(id => id.StartsWith(prefix + "-", StringComparison.Ordinal)).ToList();
        Console.WriteLine($"  {prefix,-10} {ids.Count(owners.ContainsKey),3} of {ids.Count,3} owned");
    }

    foreach (var level in new[] { "MUST", "SHOULD", "MAY" })
    {
        var marks = owned.Where(id => levels[id] == level).GroupBy(id => Mark(owners[id][^1].MarkText) ?? "(none)").OrderBy(group => group.Key, StringComparer.Ordinal);
        Console.WriteLine($"  {level,-6} rows by mark: {string.Join(", ", marks.Select(group => $"{group.Key} {group.Count()}"))}");
    }

    foreach (var finding in findings.OrderBy(f => f.Kind, StringComparer.Ordinal).ThenBy(f => f.Id, StringComparer.Ordinal))
    {
        Console.WriteLine($"  {finding.Kind,-8} {finding.Id,-14} {finding.Level,-6} {finding.Message}");
    }
}

internal sealed record Owning(string Id, string Level, string MarkText, string RowText, string File, (int Number, string Letter) Phase);

internal sealed record Finding(string Kind, string Id, string Level, string Message);
