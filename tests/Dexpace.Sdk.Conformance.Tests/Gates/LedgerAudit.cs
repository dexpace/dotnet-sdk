// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// The eleven rules of the ledger-audit gate over <c>docs/deviations.md</c> (phase 11, P11-5, P11-6). The ledger is parsed as plain markdown tables: the rows are
/// cells split on unescaped bars, the cells that hold a closed set are checked against it, and every backticked span is read for the two citations the ledger
/// makes, <c>path:line</c> and <c>Class.Method</c> of a test. Nothing here knows a particular entry; a row added to the ledger is held to the same rules.
/// </summary>
internal static partial class LedgerAudit
{
    private const string LedgerPath = "docs/deviations.md";
    private const string Section10Path = "docs/sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md";
    private const string Section11Path = "docs/sdk-design-dotnet/11-appendix-reference-spec-ambiguities-and-how-this-port-resolves-them.md";
    private const string FirstReleasePath = "docs/first-release.md";
    private const string AppendixPath = "docs/product-spec/appendix-c-consolidated-normative-requirement-index.md";

    private static readonly string[] s_reasonClasses = ["platform", "security", "aot-trim", "measured-cost", "styleguide", "spec-sanctioned"];
    private static readonly string[] s_verdicts = ["confirmed", "narrowed", "contradicted", "added"];

    [GeneratedRegex(@"^(as-built: (confirmed|narrowed|contradicted → doc corrected)|retired \(repaired, task \d+\.\d+\))$", RegexOptions.CultureInvariant)]
    private static partial Regex StatusCell();

    [GeneratedRegex(@"^(?<kind>retain|repaired|doc-corrected)(, unmet-clause: (?<id>[A-Z]+-\d+))?( \(\+.*\))?$", RegexOptions.CultureInvariant)]
    private static partial Regex DispositionCell();

    [GeneratedRegex(@"^(retain|doc-corrected|repaired|added)( \(\+.*\))?$", RegexOptions.CultureInvariant)]
    private static partial Regex ReadingDispositionCell();

    [GeneratedRegex(@"^(?<number>\d+)\. \*\*", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex NumberedEntry();

    [GeneratedRegex(@"^\| (?<id>[A-Z]+-\d+) \|", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex AppendixRow();

    [GeneratedRegex(@"(?<prefix>[A-Z]+)-(?<from>\d+)(?:\.\.(?<to>\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex RequirementReference();

    [GeneratedRegex(@"`(?<span>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"^(?<path>[^\s:]*[./][^\s:]*):(?<from>\d+)(?:-(?<to>\d+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex LineCitation();

    [GeneratedRegex(@"^(?<type>[A-Z]\w*Tests)\.(?<method>\w+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TestCitation();

    [GeneratedRegex(@"^(?<dir>[^*]+)/\*\*/\*\.(?<ext>\{[^}]+\}|\w+)$", RegexOptions.CultureInvariant)]
    private static partial Regex PathGlob();

    [GeneratedRegex(@"Unmet clauses:\s*\*\*(?<count>\d+)\*\*\s*of at most\s*\*\*(?<target>\d+)\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex CountLine();

    [GeneratedRegex(@"^- \*\*`(?<id>[A-Z]+-\d+)`", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex RegisterBullet();

    [GeneratedRegex(@"ConformanceWaiver\s*(?:\w+\s*=\s*new\s*)?\(\s*""(?<id>[A-Z]+-\d+)""[^;]*;", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex WaiverDeclaration();

    /// <summary>Runs every rule over <paramref name="repository"/>.</summary>
    /// <param name="repository">The files to read.</param>
    /// <returns>Every finding; empty when the ledger holds.</returns>
    internal static IReadOnlyList<LedgerFinding> Run(ILedgerRepository repository)
    {
        var ledgerText = repository.ReadText(LedgerPath);
        if (ledgerText is null)
        {
            return [new LedgerFinding("L1", $"{LedgerPath} does not exist")];
        }

        var model = Parse(ledgerText);
        var findings = new List<LedgerFinding>();
        findings.AddRange(NumberingFindings(model, repository.ReadText(Section10Path) ?? string.Empty));
        findings.AddRange(CellFindings(model));
        findings.AddRange(CitationFindings(ledgerText, repository));
        findings.AddRange(RetainedFindings(model));
        findings.AddRange(IdentifierFindings(model, AppendixIds(repository)));
        findings.AddRange(CountFindings(model, repository.ReadText(FirstReleasePath) ?? string.Empty));
        findings.AddRange(ReadingFindings(model, repository.ReadText(Section11Path) ?? string.Empty));
        findings.AddRange(WaiverFindings(model, repository));
        findings.AddRange(ScanFindings(model, repository));
        return findings;
    }

    /// <summary>Reads the ledger.</summary>
    /// <param name="repository">The files to read.</param>
    /// <returns>The tables, the retained blocks and the count line; empty when the file is missing.</returns>
    internal static LedgerModel Load(ILedgerRepository repository) =>
        repository.ReadText(LedgerPath) is { } text ? Parse(text) : new LedgerModel([], new Dictionary<int, string>(), [], [], null);

    private static LedgerModel Parse(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        return new LedgerModel(
            TableUnder(lines, "## The audit"),
            RetainedBlocks(lines),
            TableUnder(lines, "## Readings"),
            TableUnder(lines, "## Negative scans"),
            lines.FirstOrDefault(static line => CountLine().IsMatch(line)));
    }

    // The data rows of the first table after the heading line that starts with <heading>: the header and the separator are skipped.
    private static List<IReadOnlyList<string>> TableUnder(string[] lines, string heading)
    {
        var rows = new List<IReadOnlyList<string>>();
        var start = Array.FindIndex(lines, line => line.StartsWith(heading, StringComparison.Ordinal));
        if (start < 0)
        {
            return rows;
        }

        var inTable = false;
        var seen = 0;
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }

            if (!lines[i].StartsWith('|'))
            {
                if (inTable)
                {
                    break;
                }

                continue;
            }

            inTable = true;
            if (++seen > 2)
            {
                rows.Add(Cells(lines[i]));
            }
        }

        return rows;
    }

    private static List<string> Cells(string line) =>
        [.. Regex.Split(line.Trim().Trim('|'), @"(?<!\\)\|").Select(static cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())];

    private static Dictionary<int, string> RetainedBlocks(string[] lines)
    {
        var blocks = new Dictionary<int, string>();
        var start = Array.FindIndex(lines, static line => line.StartsWith("## Retained deviations", StringComparison.Ordinal));
        if (start < 0)
        {
            return blocks;
        }

        var number = -1;
        var text = new List<string>();
        for (var i = start + 1; i <= lines.Length; i++)
        {
            var end = i == lines.Length || lines[i].StartsWith("## ", StringComparison.Ordinal);
            var heading = i < lines.Length ? Regex.Match(lines[i], @"^### (\d+)\.", RegexOptions.CultureInvariant) : Match.Empty;
            if (end || heading.Success)
            {
                if (number >= 0)
                {
                    blocks[number] = string.Join('\n', text);
                }

                if (end)
                {
                    break;
                }

                number = int.Parse(heading.Groups[1].Value, CultureInfo.InvariantCulture);
                text.Clear();
            }
            else if (number >= 0)
            {
                text.Add(lines[i]);
            }
        }

        return blocks;
    }

    // L1: one audit row per §10 entry.
    private static IEnumerable<LedgerFinding> NumberingFindings(LedgerModel model, string section10)
    {
        var expected = NumberedEntry().Matches(section10).Select(static m => int.Parse(m.Groups["number"].Value, CultureInfo.InvariantCulture)).ToHashSet();
        var rows = model.Audit.Select(static row => int.TryParse(row[0], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1).ToList();
        foreach (var number in expected.Where(number => !rows.Contains(number)).Order())
        {
            yield return new LedgerFinding("L1", $"§10 entry {number} has no audit row (row {number})");
        }

        foreach (var number in rows.Where(number => !expected.Contains(number)).Order())
        {
            yield return new LedgerFinding("L1", $"the audit has a row {number} that §10 does not number");
        }

        foreach (var number in rows.GroupBy(static n => n).Where(static g => g.Count() > 1).Select(static g => g.Key).Order())
        {
            yield return new LedgerFinding("L1", $"the audit numbers entry {number} twice");
        }
    }

    // L2: the closed sets (the audit's status, disposition and reason; the readings' verdict and disposition).
    private static IEnumerable<LedgerFinding> CellFindings(LedgerModel model)
    {
        foreach (var row in model.Audit.Where(static row => row.Count >= 7))
        {
            if (row[3].Contains("design only", StringComparison.OrdinalIgnoreCase) || !StatusCell().IsMatch(row[3]))
            {
                yield return new LedgerFinding("L2", $"audit row {row[0]}: status '{row[3]}' is not in the closed set");
            }

            if (!DispositionCell().IsMatch(row[4]))
            {
                yield return new LedgerFinding("L2", $"audit row {row[0]}: disposition '{row[4]}' is not in the closed set");
            }

            if (row[5] != "—" && !ReasonClasses(row[5]).All(s_reasonClasses.Contains))
            {
                yield return new LedgerFinding("L2", $"audit row {row[0]}: reason '{row[5]}' is not a class of the closed set");
            }
        }

        foreach (var row in model.Readings.Where(static row => row.Count >= 5))
        {
            if (!s_verdicts.Contains(row[2]) || !ReadingDispositionCell().IsMatch(row[3]))
            {
                yield return new LedgerFinding("L2", $"readings row {row[0]}: verdict '{row[2]}' or disposition '{row[3]}' is not in the closed set");
            }
        }
    }

    private static string[] ReasonClasses(string cell) => [.. cell.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];

    // L3 and L11: every backticked span that is a path:line must resolve, and every Class.Method of a test must be declared.
    private static IEnumerable<LedgerFinding> CitationFindings(string ledgerText, ILedgerRepository repository)
    {
        var tests = new Lazy<List<string>>(() => [.. repository.Files("tests", [".cs"]).Select(path => repository.ReadText(path) ?? string.Empty)]);
        foreach (var span in CodeSpan().Matches(ledgerText).Select(static m => m.Groups["span"].Value).Distinct(StringComparer.Ordinal))
        {
            if (LineCitation().Match(span) is { Success: true } citation)
            {
                var file = repository.ReadText(citation.Groups["path"].Value);
                var last = int.Parse(citation.Groups[citation.Groups["to"].Success ? "to" : "from"].Value, CultureInfo.InvariantCulture);
                var first = int.Parse(citation.Groups["from"].Value, CultureInfo.InvariantCulture);
                var count = file?.ReplaceLineEndings("\n").Split('\n').Length ?? 0;
                if (file is null || first < 1 || last > count || last < first)
                {
                    yield return new LedgerFinding("L3", file is null ? $"{span}: no such file" : $"{span}: line outside the {count} lines of the file");
                }
            }
            else if (TestCitation().Match(span) is { Success: true } test && !TestExists(tests.Value, test.Groups["type"].Value, test.Groups["method"].Value))
            {
                yield return new LedgerFinding("L11", $"{span}: no such test method under tests/");
            }
        }
    }

    private static bool TestExists(List<string> files, string type, string method) =>
        files.Any(text => Regex.IsMatch(text, $@"\bclass\s+{Regex.Escape(type)}\b", RegexOptions.CultureInvariant)
            && Regex.IsMatch(text, $@"\b{Regex.Escape(method)}\s*[(<]", RegexOptions.CultureInvariant));

    // L4: a retained row names a class and has a block that says why.
    private static IEnumerable<LedgerFinding> RetainedFindings(LedgerModel model)
    {
        foreach (var row in model.Audit.Where(static row => row.Count >= 7 && row[4].StartsWith("retain", StringComparison.Ordinal)))
        {
            if (row[5] == "—" || ReasonClasses(row[5]).Length == 0 || !ReasonClasses(row[5]).All(s_reasonClasses.Contains))
            {
                yield return new LedgerFinding("L4", $"audit row {row[0]} is retained and names no reason class");
            }

            if (!int.TryParse(row[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || !model.Retained.TryGetValue(number, out var block)
                || !block.Contains("**Why it stays.**", StringComparison.Ordinal))
            {
                yield return new LedgerFinding("L4", $"audit row {row[0]} is retained and has no '### {row[0]}.' block with a 'Why it stays' paragraph");
            }
        }
    }

    private static HashSet<string> AppendixIds(ILedgerRepository repository) =>
        [.. AppendixRow().Matches(repository.ReadText(AppendixPath) ?? string.Empty).Select(static m => m.Groups["id"].Value)];

    // L5: every requirement reference of the IDs columns is in appendix C; a range 'PAGE-21..22' stands for each ID it spans.
    private static IEnumerable<LedgerFinding> IdentifierFindings(LedgerModel model, HashSet<string> known)
    {
        var cells = model.Audit.Where(static row => row.Count >= 3).Select(static row => ("audit row " + row[0], row[2]))
            .Concat(model.Readings.Where(static row => row.Count >= 2).Select(static row => ("readings row " + row[0], row[1])));
        foreach (var (where, cell) in cells)
        {
            foreach (var id in Expand(cell).Where(id => !known.Contains(id)))
            {
                yield return new LedgerFinding("L5", $"{where}: {id} is not in appendix C");
            }
        }
    }

    private static IEnumerable<string> Expand(string cell)
    {
        foreach (var match in RequirementReference().Matches(cell).Cast<Match>())
        {
            var from = int.Parse(match.Groups["from"].Value, CultureInfo.InvariantCulture);
            var to = match.Groups["to"].Success ? int.Parse(match.Groups["to"].Value, CultureInfo.InvariantCulture) : from;
            for (var n = from; n <= to; n++)
            {
                yield return $"{match.Groups["prefix"].Value}-{n}";
            }
        }
    }

    // L6: the unmet-clause rows, the count line and the release register's Unsatisfied MUSTs are one fact, at most the target.
    private static IEnumerable<LedgerFinding> CountFindings(LedgerModel model, string firstRelease)
    {
        var unmet = UnmetIds(model).ToList();
        if (model.CountLine is null || CountLine().Match(model.CountLine) is not { Success: true } line)
        {
            yield return new LedgerFinding("L6", "the ledger has no 'Unmet clauses: **n** of at most **m**' line");
            yield break;
        }

        var count = int.Parse(line.Groups["count"].Value, CultureInfo.InvariantCulture);
        var target = int.Parse(line.Groups["target"].Value, CultureInfo.InvariantCulture);
        if (count != unmet.Count)
        {
            yield return new LedgerFinding("L6", $"the count line says {count} and the audit has {unmet.Count} unmet-clause rows");
        }

        if (unmet.Count > target || count > target)
        {
            yield return new LedgerFinding("L6", $"{Math.Max(count, unmet.Count)} unmet clauses are over the target of {target}");
        }

        var register = RegisterIds(firstRelease);
        foreach (var id in unmet.Except(register).Order(StringComparer.Ordinal))
        {
            yield return new LedgerFinding("L6", $"{id} is an unmet-clause row but not in the register's Unsatisfied MUSTs");
        }

        foreach (var id in register.Except(unmet).Order(StringComparer.Ordinal))
        {
            yield return new LedgerFinding("L6", $"{id} is in the register's Unsatisfied MUSTs but has no unmet-clause row");
        }
    }

    private static IEnumerable<string> UnmetIds(LedgerModel model) =>
        model.Audit.Where(static row => row.Count >= 5).Select(static row => DispositionCell().Match(row[4])).Where(static m => m.Success && m.Groups["id"].Success).Select(static m => m.Groups["id"].Value);

    private static HashSet<string> RegisterIds(string firstRelease)
    {
        var lines = firstRelease.ReplaceLineEndings("\n").Split('\n');
        var start = Array.FindIndex(lines, static line => line.StartsWith("### Unsatisfied MUSTs", StringComparison.Ordinal));
        if (start < 0)
        {
            return [];
        }

        var end = Array.FindIndex(lines, start + 1, static line => line.StartsWith('#'));
        var section = string.Join('\n', lines[(start + 1)..(end < 0 ? lines.Length : end)]);
        return [.. RegisterBullet().Matches(section).Select(static m => m.Groups["id"].Value)];
    }

    // L7: every §11 item has a readings row, once.
    private static IEnumerable<LedgerFinding> ReadingFindings(LedgerModel model, string section11)
    {
        var items = NumberedEntry().Matches(section11).Select(static m => int.Parse(m.Groups["number"].Value, CultureInfo.InvariantCulture)).ToHashSet();
        var rows = model.Readings.Select(static row => int.TryParse(row[0], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1).ToList();
        foreach (var item in items.Where(item => !rows.Contains(item)).Order())
        {
            yield return new LedgerFinding("L7", $"§11 item {item} has no readings row");
        }

        foreach (var item in rows.GroupBy(static n => n).Where(static g => g.Count() > 1).Select(static g => g.Key).Order())
        {
            yield return new LedgerFinding("L7", $"the readings number item {item} twice");
        }
    }

    // L8: a permanent waiver (no Owner) in a first-party driver is an unmet-clause row or a reading that scopes the clause out; an unmet row that cites a waiver finds one.
    private static IEnumerable<LedgerFinding> WaiverFindings(LedgerModel model, ILedgerRepository repository)
    {
        var permanent = PermanentWaivers(repository);
        var unmet = UnmetIds(model).ToHashSet(StringComparer.Ordinal);
        var scoped = model.Readings.Where(static row => row.Count >= 5 && row[2] == "added").SelectMany(static row => Expand(row[1])).ToHashSet(StringComparer.Ordinal);
        foreach (var id in permanent.Where(id => !unmet.Contains(id) && !scoped.Contains(id)).Order(StringComparer.Ordinal))
        {
            yield return new LedgerFinding("L8", $"the permanent waiver for {id} has no unmet-clause row and no added reading that scopes it out");
        }

        foreach (var row in model.Audit.Where(static row => row.Count >= 7 && row[6].Contains("waiver", StringComparison.OrdinalIgnoreCase)))
        {
            var ids = Expand(row[2]).ToList();
            if (UnmetIds(new LedgerModel([row], new Dictionary<int, string>(), [], [], null)).Any() && !ids.Any(permanent.Contains))
            {
                yield return new LedgerFinding("L8", $"audit row {row[0]} cites a waiver but no driver declares a permanent waiver for {string.Join(", ", ids)}");
            }
        }
    }

    // The permanent waivers: ConformanceWaiver declarations in the drivers (tests/**/Conformance/*.cs, the kit's own tests excepted) with no Owner.
    private static HashSet<string> PermanentWaivers(ILedgerRepository repository)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in repository.Files("tests", [".cs"]).Where(static path => path.Contains("/Conformance/", StringComparison.Ordinal) && !path.StartsWith("tests/Dexpace.Sdk.Conformance.Tests/", StringComparison.Ordinal)))
        {
            foreach (var declaration in WaiverDeclaration().Matches(repository.ReadText(path) ?? string.Empty).Cast<Match>().Where(static m => !m.Value.Contains("Owner =", StringComparison.Ordinal)))
            {
                ids.Add(declaration.Groups["id"].Value);
            }
        }

        return ids;
    }

    // L9 and L10: each scan matches only its allow-list, its probe matches its pattern (a vacuous pattern fails), and its paths read at least one
    // file (a glob that does not parse, or whose directory holds none, is a scan that proves nothing, so a renamed directory fails the rule).
    private static IEnumerable<LedgerFinding> ScanFindings(LedgerModel model, ILedgerRepository repository)
    {
        foreach (var row in model.Scans.Where(static row => row.Count >= 7))
        {
            var pattern = Spans(row[3]).FirstOrDefault() ?? string.Empty;
            var probe = Spans(row[6]).FirstOrDefault() ?? string.Empty;
            var regex = Compile(pattern);
            if (regex is null)
            {
                yield return new LedgerFinding("L10", $"scan {row[0]}: the pattern '{pattern}' is not a regular expression");
                continue;
            }

            if (!regex.IsMatch(probe))
            {
                yield return new LedgerFinding("L10", $"scan {row[0]}: the probe '{probe}' does not match the pattern");
            }

            if (row[5].StartsWith("gate:", StringComparison.Ordinal))
            {
                continue;
            }

            var globs = Spans(row[4]).ToList();
            foreach (var finding in GlobFindings(row[0], globs, repository))
            {
                yield return finding;
            }

            var allowed = Spans(row[5]).ToHashSet(StringComparer.Ordinal);
            foreach (var hit in Hits(regex, globs, repository).Where(hit => !allowed.Contains(Path.GetFileName(hit.File))))
            {
                yield return new LedgerFinding("L9", $"scan {row[0]} matches {hit.File}:{hit.Line}, which its allow-list does not name");
            }
        }
    }

    private static IEnumerable<LedgerFinding> GlobFindings(string scan, List<string> globs, ILedgerRepository repository)
    {
        if (globs.Count == 0)
        {
            yield return new LedgerFinding("L10", $"scan {scan}: names no path glob, so it reads nothing");
        }

        foreach (var glob in globs)
        {
            if (FilesOf(glob, repository) is not { } files)
            {
                yield return new LedgerFinding("L10", $"scan {scan}: the path glob '{glob}' is not <dir>/**/*.<ext> or <dir>/**/*.{{a,b}}, so it reads nothing");
            }
            else if (files.Count == 0)
            {
                yield return new LedgerFinding("L10", $"scan {scan}: the path glob '{glob}' matches no file (a renamed or emptied directory?), so it reads nothing");
            }
        }
    }

    private static Regex? Compile(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IEnumerable<string> Spans(string cell) => CodeSpan().Matches(cell).Select(static m => m.Groups["span"].Value);

    // The files a glob names, or null when it is not of the one shape the ledger documents.
    private static List<string>? FilesOf(string glob, ILedgerRepository repository)
    {
        var match = PathGlob().Match(glob);
        if (!match.Success)
        {
            return null;
        }

        var extensions = match.Groups["ext"].Value.Trim('{', '}').Split(',').Select(static e => "." + e).ToArray();
        return [.. repository.Files(match.Groups["dir"].Value, extensions)];
    }

    private static IEnumerable<(string File, int Line)> Hits(Regex regex, IEnumerable<string> globs, ILedgerRepository repository)
    {
        foreach (var file in globs.SelectMany(glob => FilesOf(glob, repository) ?? []))
        {
            var lines = (repository.ReadText(file) ?? string.Empty).ReplaceLineEndings("\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal) && regex.IsMatch(lines[i]))
                {
                    yield return (file, i + 1);
                }
            }
        }
    }
}
