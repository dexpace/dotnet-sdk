// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// The ledger-audit gate (phase 11, P11-5, P11-6; the roadmap's "a claim that something is absent needs a negative scan, and that scan becomes a gate"). It reads
/// <c>docs/deviations.md</c> against design §10 and §11, <c>docs/first-release.md</c>, appendix C, the sources and the first-party conformance drivers, and holds
/// eleven rules: L1 every §10 number has one audit row; L2 the status and disposition cells are in their closed sets (never <c>design only</c>); L3 every cited
/// <c>path:line</c> exists; L4 every retained row has a reason class and a <c>Why it stays</c> block; L5 every cited requirement ID is in appendix C; L6 the unmet-clause
/// rows, the count line and the <c>Unsatisfied MUSTs</c> of the release register agree, at most five; L7 every §11 item has a readings row; L8 every permanent conformance waiver
/// has a ledger row; L9 every negative scan matches only its allow-list; L10 every scan's probe matches its pattern and every scan reads at least one file (a path glob that does not parse, or whose directory holds no file, is a scan that proves nothing); L11 every cited test exists. Each rule is a permanent
/// test over the real files and has a fixture, held in this class, that must fail it (the repository's red/green discipline for a gate).
/// </summary>
[Trait("Category", "Unit")]
public sealed class LedgerAuditTests
{
    private const string LedgerPath = "docs/deviations.md";
    private const string Section10Path = "docs/sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md";
    private const string Section11Path = "docs/sdk-design-dotnet/11-appendix-reference-spec-ambiguities-and-how-this-port-resolves-them.md";
    private const string FirstReleasePath = "docs/first-release.md";
    private const string AppendixPath = "docs/product-spec/appendix-c-consolidated-normative-requirement-index.md";
    private const string DriverPath = "tests/Dexpace.Sdk.Foo.Tests/Conformance/Subject.cs";

    private static readonly Lazy<IReadOnlyList<LedgerFinding>> s_real = new(static () => LedgerAudit.Run(new DiskLedgerRepository()));

    public static TheoryData<string, string, string> Fixtures => new()
    {
        { "L1", "a §10 entry with no audit row", "row 2" },
        { "L2", "design only in a status cell", "status" },
        { "L3", "a path:line past the end of its file", "line" },
        { "L4", "a retain row with no reason class", "reason" },
        { "L4", "a retain row with no Why it stays block", "block" },
        { "L5", "an unknown requirement ID", "ID" },
        { "L6", "an unmet-clause row absent from the release register", "register" },
        { "L6", "a count of six over the target of five", "count" },
        { "L7", "a §11 item with no readings row", "item" },
        { "L8", "a permanent waiver with no ledger row", "waiver" },
        { "L8", "an unmet-clause row that cites a waiver which was deleted", "deleted waiver" },
        { "L9", "a scan matching outside its allow-list", "scan" },
        { "L10", "a scan whose probe does not match its pattern", "probe" },
        { "L10", "a scan whose path glob does not parse", "glob" },
        { "L10", "a scan whose directory yields no file", "empty" },
        { "L10", "a scan with no path glob at all", "no path" },
        { "L11", "a cited test that does not exist", "test" },
    };

    private static IReadOnlyList<LedgerFinding> Real => s_real.Value;

    [Fact]
    public void The_baseline_fixture_is_clean()
    {
        Assert.Empty(LedgerAudit.Run(Baseline()));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Each_rule_fails_its_fixture(string rule, string defect, string label)
    {
        var findings = LedgerAudit.Run(Broken(rule, label));

        Assert.True(findings.Any(finding => finding.Rule == rule), $"{rule} did not fail for {defect} ({label}); findings: {string.Join("; ", findings.Select(f => $"{f.Rule}: {f.Message}"))}");
        Assert.All(findings, finding => Assert.Equal(rule, finding.Rule));
    }

    [Fact]
    public void L1_every_section_10_entry_has_one_audit_row() => Assert.Empty(Of("L1"));

    [Fact]
    public void L2_status_and_disposition_cells_are_in_their_closed_sets() => Assert.Empty(Of("L2"));

    [Fact]
    public void L3_every_cited_line_exists() => Assert.Empty(Of("L3"));

    [Fact]
    public void L4_every_retained_row_has_a_reason_class_and_a_block() => Assert.Empty(Of("L4"));

    [Fact]
    public void L5_every_cited_requirement_id_is_in_appendix_C() => Assert.Empty(Of("L5"));

    [Fact]
    public void L6_the_unmet_clauses_agree_with_the_release_register_and_the_target() => Assert.Empty(Of("L6"));

    [Fact]
    public void L7_every_section_11_item_has_a_readings_row() => Assert.Empty(Of("L7"));

    [Fact]
    public void L8_every_permanent_waiver_has_a_ledger_row() => Assert.Empty(Of("L8"));

    [Fact]
    public void L9_every_negative_scan_matches_only_its_allow_list() => Assert.Empty(Of("L9"));

    [Fact]
    public void L10_every_scan_probe_matches_its_pattern_and_its_paths_read_a_file() => Assert.Empty(Of("L10"));

    [Fact]
    public void L11_every_cited_test_exists() => Assert.Empty(Of("L11"));

    [Fact]
    public void The_real_ledger_has_the_shape_the_rules_assume()
    {
        // Not vacuous: a parser that read nothing would pass every rule above for the wrong reason.
        var ledger = LedgerAudit.Load(new DiskLedgerRepository());

        Assert.Equal(33, ledger.Audit.Count);
        Assert.Equal(72, ledger.Readings.Count);
        Assert.True(ledger.Scans.Count >= 20, $"{ledger.Scans.Count} scans");
        Assert.NotNull(ledger.CountLine);
        Assert.Equal(4, ledger.Audit.Count(static row => row[4].Contains("unmet-clause", StringComparison.Ordinal)));
        Assert.True(ledger.Retained.Count >= 25, $"{ledger.Retained.Count} retained blocks");
    }

    private static IEnumerable<LedgerFinding> Of(string rule) => Real.Where(finding => finding.Rule == rule);

    // The smallest tree the rules accept: two §10 entries, two §11 items, one retained unmet-clause row, one repaired row, a source file, a test, a driver with two waivers.
    private static MemoryRepository Baseline() => new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [LedgerPath] = """
            # Deviations

            Unmet clauses: **1** of at most **5**.

            ## The audit

            | # | Title | IDs touched | Status | Disposition | Reason | Evidence |
            |---|---|---|---|---|---|---|
            | 1 | First | `AUTH-15`, `PAGE-21..22` | as-built: confirmed | retain, unmet-clause: AUTH-15 | security | `src/Sample.cs:3`, `WidgetTests.Works`, the waiver in the driver |
            | 2 | Second | — | retired (repaired, task 1.1) | repaired | — | `src/Sample.cs:1-4` |

            ## Retained deviations

            ### 1. First

            **Verified.** Yes.

            **Why it stays.** Because.

            ## Readings (design §11)

            | Item | IDs | Verdict | Disposition | Evidence |
            |---|---|---|---|---|
            | 1 | `AUTH-15` | confirmed | retain | `src/Sample.cs:2` |
            | 2 | `TRANSPORT-2` | added | added | `src/Sample.cs:2` |

            ## Negative scans

            | Scan | Entry/item | Claim | Pattern | Paths | Allowed | Probe |
            |---|---|---|---|---|---|---|
            | N1 | 1 | no widgets | `class\s+Widget\b` | `src/**/*.cs` | `Allowed.cs` | `class Widget` |
            | N2 | 2 | gated elsewhere | `catch\s*$` | `src/**/*.cs` | gate: `WidgetTests.Works` | `catch` |

            ## Amendment set N11
            """,
        [Section10Path] = "10. x\n1. **First.** text\n2. **Second.** text\n",
        [Section11Path] = "1. **One.** text\n2. **Two.** text\n",
        [FirstReleasePath] = "## What v1 ships without\n\n### Unsatisfied MUSTs\n\n- **`AUTH-15` (MUST), the clause.** why.\n\n### SHOULD- and MAY-level requirements declined for v1\n",
        [AppendixPath] = "| AUTH-15 | MUST | t | text |\n| PAGE-21 | MUST | t | text |\n| PAGE-22 | MUST | t | text |\n| TRANSPORT-2 | MUST | t | text |\n",
        ["src/Sample.cs"] = "// one\nclass Gadget { }\nclass Gizmo { }\n}\n",
        ["src/Allowed.cs"] = "class Widget { }\n",
        ["tests/Dexpace.Sdk.Foo.Tests/WidgetTests.cs"] = "public sealed class WidgetTests\n{\n    public void Works() { }\n}\n",
        [DriverPath] = "var a = new ConformanceWaiver(\"AUTH-15\", \"reason\") { Assertion = \"a\" };\nvar b = new ConformanceWaiver(\"TRANSPORT-2\", \"reason\") { Assertion = \"b\" };\nvar c = new ConformanceWaiver(\"TRANSPORT-8\", \"later\") { Owner = \"8b\" };\n",
    });

    // The baseline with one defect, chosen by the rule and the label of the fixture row.
    private static MemoryRepository Broken(string rule, string label) => (rule, label) switch
    {
        ("L1", _) => Baseline().Replace(LedgerPath, "| 2 | Second | — | retired (repaired, task 1.1) | repaired | — | `src/Sample.cs:1-4` |\n", string.Empty),
        ("L2", _) => Baseline().Replace(LedgerPath, "as-built: confirmed", "design only — not yet built"),
        ("L3", _) => Baseline().Replace(LedgerPath, "`src/Sample.cs:3`", "`src/Sample.cs:99`"),
        ("L4", "reason") => Baseline().Replace(LedgerPath, "| security |", "| — |"),
        ("L4", _) => Baseline().Replace(LedgerPath, "**Why it stays.** Because.", "Because."),
        ("L5", _) => Baseline().Replace(LedgerPath, "`PAGE-21..22`", "`ZZZ-9`"),
        ("L6", "register") => Baseline().Replace(FirstReleasePath, "- **`AUTH-15` (MUST), the clause.** why.", "- **`PAGE-21` (MUST), another clause.** why."),
        ("L6", _) => Baseline().Replace(LedgerPath, "**1** of at most **5**", "**6** of at most **5**"),
        ("L7", _) => Baseline().Replace(Section11Path, "2. **Two.** text\n", "2. **Two.** text\n3. **Three.** text\n"),
        ("L8", "waiver") => Baseline().Replace(DriverPath, "var c =", "var d = new ConformanceWaiver(\"PAGE-22\", \"x\") { Assertion = \"d\" };\nvar c ="),
        ("L8", _) => Baseline().Replace(DriverPath, "new ConformanceWaiver(\"AUTH-15\", \"reason\") { Assertion = \"a\" }", "new Placeholder()"),
        ("L9", _) => Baseline().With("src/Other.cs", "class Widget { }\n"),
        ("L10", "glob") => Baseline().Replace(LedgerPath, "| `src/**/*.cs` | `Allowed.cs` |", "| `src/*.cs` | `Allowed.cs` |"),
        ("L10", "empty") => Baseline().Replace(LedgerPath, "| `src/**/*.cs` | `Allowed.cs` |", "| `source/**/*.cs` | `Allowed.cs` |"),
        ("L10", "no path") => Baseline().Replace(LedgerPath, "| `src/**/*.cs` | `Allowed.cs` |", "| — | `Allowed.cs` |"),
        ("L10", _) => Baseline().Replace(LedgerPath, "`class Widget`", "`class Gadget`"),
        ("L11", _) => Baseline().Replace(LedgerPath, "`WidgetTests.Works`, the waiver", "`WidgetTests.Missing`, the waiver"),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "No fixture for this rule."),
    };

    // An in-memory tree. Replace asserts the text it replaces is there, so a fixture cannot silently stop mutating.
    private sealed class MemoryRepository(Dictionary<string, string> files) : ILedgerRepository
    {
        public string? ReadText(string relative) => files.TryGetValue(relative, out var text) ? text : null;

        public IEnumerable<string> Files(string directory, IReadOnlyCollection<string> extensions) =>
            files.Keys.Where(path => path.StartsWith(directory + "/", StringComparison.Ordinal) && extensions.Any(extension => path.EndsWith(extension, StringComparison.Ordinal)));

        public MemoryRepository Replace(string path, string from, string to)
        {
            Assert.Contains(from, files[path], StringComparison.Ordinal);
            var copy = new Dictionary<string, string>(files, StringComparer.Ordinal) { [path] = files[path].Replace(from, to, StringComparison.Ordinal) };
            return new MemoryRepository(copy);
        }

        public MemoryRepository With(string path, string text) =>
            new(new Dictionary<string, string>(files, StringComparer.Ordinal) { [path] = text });
    }
}
