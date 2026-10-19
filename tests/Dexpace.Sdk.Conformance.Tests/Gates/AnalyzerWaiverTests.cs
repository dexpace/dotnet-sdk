// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// NFR-7 (phase 10, P10-13, V12, plan reading R6): "analyzer exemptions are documented with a reason and a re-enable condition". Every
/// <c>dotnet_diagnostic.*.severity = none</c> line and every relaxing <c>dotnet_code_quality.*</c> option of <c>.editorconfig</c> sits in a block whose
/// comment gives a rationale and a <c>Re-enable when:</c> line, and every <c>#pragma warning disable</c> under <c>src/</c> carries a why-comment. Consecutive
/// setting lines are one block sharing the comment above it (above the section header when the block opens the section). One option is exempt by
/// name, as a hand-off rule rather than a relaxation: <c>CA2000.dispose_ownership_transfer_at_constructor</c> in library scope, which makes the analyzer follow a
/// constructor that takes ownership (styleguide 13.1) and silences no diagnostic.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class AnalyzerWaiverTests
{
    private const string Exempt = "dotnet_code_quality.CA2000.dispose_ownership_transfer_at_constructor";

    [GeneratedRegex(@"^\s*dotnet_diagnostic\.[A-Za-z0-9]+\.severity\s*=\s*none\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex SeverityNone();

    [GeneratedRegex(@"^\s*(?<key>dotnet_code_quality\.[^=\s]+)\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex CodeQuality();

    [Fact]
    public void Every_severity_none_has_a_rationale_and_a_re_enable_condition()
    {
        var violations = ExemptionViolations(File.ReadAllLines(RepositoryRoot.Of(".editorconfig")));

        Assert.Empty(violations);
    }

    [Fact]
    public void The_scan_finds_the_seven_none_lines_and_the_three_code_quality_options()
    {
        // V12 and plan reading R6: a scan that found fewer would pass for the wrong reason.
        var lines = File.ReadAllLines(RepositoryRoot.Of(".editorconfig"));

        Assert.Equal(7, lines.Count(static line => SeverityNone().IsMatch(line)));
        Assert.Equal(3, lines.Count(static line => CodeQuality().IsMatch(line)));
    }

    [Fact]
    public void The_exemption_is_exactly_one_key()
    {
        var real = File.ReadAllLines(RepositoryRoot.Of(".editorconfig"));

        // Another library-scope option without a condition is a violation...
        var another = InLibraryScope(real, "dotnet_code_quality.CA1000.api_surface = public");
        // ...and so is the exempt key's sibling in library scope.
        var sibling = InLibraryScope(real, "dotnet_code_quality.CA2000.dispose_ownership_transfer_at_method_call = true");

        Assert.Empty(ExemptionViolations(real));
        Assert.Single(ExemptionViolations(another));
        Assert.Single(ExemptionViolations(sibling));
    }

    [Fact]
    public void Every_pragma_disable_under_src_has_a_why_comment()
    {
        var offenders = PragmaOffenders(RepositoryRoot.Of("src"));

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_scan_sees_the_pragmas_under_src()
    {
        // V12 counted 43 in 31 files at the time of the design; phase 10 adds a few more. A scan that found none would pass for the wrong reason.
        var count = Directory.EnumerateFiles(RepositoryRoot.Of("src"), "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Sum(static f => File.ReadAllLines(f).Count(static line => line.Contains("#pragma warning disable", StringComparison.Ordinal)));

        Assert.True(count >= 43, $"{count} pragmas");
    }

    [Fact]
    public void The_checks_can_fail()
    {
        string[] noRationale = ["[*.cs]", "dotnet_diagnostic.CA9999.severity = none"];
        string[] rationaleOnly = ["[*.cs]", "# Because we say so.", "dotnet_diagnostic.CA9999.severity = none"];
        string[] conditionOnly = ["[*.cs]", "# Re-enable when: never.", "dotnet_diagnostic.CA9999.severity = none"];
        string[] complete = ["[*.cs]", "# Because we say so.", "# Re-enable when: never for src/.", "dotnet_diagnostic.CA9999.severity = none"];
        string[] sharedBlock = ["[*.cs]", "# Because we say so.", "# Re-enable when: the URL entry points go.", "dotnet_diagnostic.CA1054.severity = none", "dotnet_diagnostic.CA1055.severity = none"];
        string[] aboveHeader = ["# Because we say so.", "# Re-enable when: never for src/.", "[tests/**/*.cs]", "dotnet_diagnostic.CA2007.severity = none"];

        Assert.Single(ExemptionViolations(noRationale));
        Assert.Single(ExemptionViolations(rationaleOnly));
        Assert.Single(ExemptionViolations(conditionOnly));
        Assert.Empty(ExemptionViolations(complete));
        Assert.Empty(ExemptionViolations(sharedBlock));
        Assert.Empty(ExemptionViolations(aboveHeader));

        var root = Directory.CreateTempSubdirectory("pragma");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, "a.cs"), "#pragma warning disable CA1031 // why\n#pragma warning restore CA1031\n");
            File.WriteAllText(Path.Combine(root.FullName, "b.cs"), "// why\n#pragma warning disable CA1031\n#pragma warning restore CA1031\n");
            File.WriteAllText(Path.Combine(root.FullName, "c.cs"), "int x;\n#pragma warning disable CA1031\n#pragma warning restore CA1031\n");

            Assert.Equal(["c.cs:2"], PragmaOffenders(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    // The option as its own block in the first (library-scope) section, after the first setting and a blank line, so it shares no comment.
    private static string[] InLibraryScope(string[] lines, string option)
    {
        var at = Array.FindIndex(lines, static line => line.StartsWith("charset", StringComparison.Ordinal)) + 1;
        return [.. lines[..at], string.Empty, option, string.Empty, .. lines[at..]];
    }

    private static List<string> ExemptionViolations(string[] lines)
    {
        var violations = new List<string>();
        var section = string.Empty;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith('['))
            {
                section = line;
                continue;
            }

            if (!IsSetting(line) || (i > 0 && IsSetting(lines[i - 1].Trim())))
            {
                continue;
            }

            // The first line of a run of consecutive settings: collect the relaxing lines of the run.
            var run = new List<string>();
            for (var j = i; j < lines.Length && IsSetting(lines[j].Trim()); j++)
            {
                run.Add(lines[j].Trim());
            }

            var relaxing = run.Where(setting => SeverityNone().IsMatch(setting) || (CodeQuality().IsMatch(setting) && !IsExempt(setting, section))).ToArray();
            if (relaxing.Length == 0)
            {
                continue;
            }

            var comment = CommentAbove(lines, i);
            var conditions = comment.Count(static c => c.StartsWith("# Re-enable when:", StringComparison.Ordinal));
            var rationale = comment.Count(static c => !c.StartsWith("# Re-enable when:", StringComparison.Ordinal) && c.Length > 1);
            if (rationale == 0 || conditions != 1)
            {
                violations.Add($"line {i + 1} ({relaxing[0]}): {(rationale == 0 ? "no rationale" : string.Empty)}{(rationale == 0 && conditions != 1 ? " and " : string.Empty)}{(conditions != 1 ? $"{conditions} 'Re-enable when:' lines" : string.Empty)}");
            }
        }

        return violations;
    }

    private static bool IsSetting(string line) => line.Length > 0 && !line.StartsWith('#') && !line.StartsWith('[') && line.Contains('=', StringComparison.Ordinal);

    private static bool IsExempt(string setting, string section)
    {
        var key = CodeQuality().Match(setting).Groups["key"].Value;
        var testScope = section.Contains("tests", StringComparison.Ordinal) || section.Contains("tools", StringComparison.Ordinal) || section.Contains(".claude", StringComparison.Ordinal);
        return key == Exempt && !testScope;
    }

    // The contiguous comment lines directly above line i, looking above a section header when the setting opens its section.
    private static List<string> CommentAbove(string[] lines, int i)
    {
        var at = i - 1;
        if (at >= 0 && lines[at].Trim().StartsWith('['))
        {
            at--;
        }

        var comment = new List<string>();
        for (; at >= 0 && lines[at].Trim().StartsWith('#'); at--)
        {
            comment.Add(lines[at].Trim());
        }

        return comment;
    }

    private static List<string> PragmaOffenders(string root) =>
        [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file =>
            {
                var lines = File.ReadAllLines(file);
                return Enumerable.Range(0, lines.Length)
                    .Where(i => lines[i].Contains("#pragma warning disable", StringComparison.Ordinal))
                    .Where(i => !lines[i].Contains("//", StringComparison.Ordinal) && !(i > 0 && lines[i - 1].TrimStart().StartsWith("//", StringComparison.Ordinal)))
                    .Select(i => $"{Path.GetRelativePath(root, file)}:{i + 1}");
            })
            .Order(StringComparer.Ordinal)];
}
