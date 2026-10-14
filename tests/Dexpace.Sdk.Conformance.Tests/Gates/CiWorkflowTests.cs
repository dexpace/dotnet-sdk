// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// NFR-17's first half (phase 10, P10-16, plan reading R8): every gate of the gate table is a step of <c>.github/workflows/ci.yml</c>, and no step or job is
/// advisory. The second half, that a merge requires the checks, is a repository setting outside the file, recorded in the phase checklist. The workflow is read
/// as text (no YAML package is added): the rules are line-based and documented here. A gate added to the workflow without a row below is not checked, and a row
/// whose step is renamed or loses its command fails: this is the "every blocking gate stays blocking" pin.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class CiWorkflowTests
{
    /// <summary>
    /// The gates: the step's name, and the command that must appear in its <c>run:</c>. The requirement-coverage gates join with the commit that adds
    /// their steps (plan 4.9, R8).
    /// </summary>
    internal static IReadOnlyList<(string Step, string Command)> Gates { get; } =
    [
        ("Restore (locked)", "dotnet restore Dexpace.Sdk.sln --locked-mode"),
        ("Build", "dotnet build Dexpace.Sdk.sln --configuration Release"),
        ("Format", "dotnet format Dexpace.Sdk.sln --verify-no-changes"),
        ("Test", "--coverlet --coverlet-output-format cobertura"),
        ("Coverage floor", "dotnet run scripts/ci/coverage-gate.cs -- artifacts/test-results 80"),
        ("Coverage floor - self-test", "scripts/ci/coverage-gate-selftest.sh artifacts/test-results"),
        ("Pack", "dotnet pack Dexpace.Sdk.sln --configuration Release"),
        ("Dependency audit", "dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/packages"),
        ("Packaging conformance", "dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages"),
        ("Tools - restore (locked)", "dotnet restore tools/Dexpace.Tools.sln --locked-mode"),
        ("Tools - build", "dotnet build tools/Dexpace.Tools.sln --configuration Release"),
        ("Tools - format", "dotnet format tools/Dexpace.Tools.sln --verify-no-changes"),
        ("Tools - test", "dotnet test --solution tools/Dexpace.Tools.sln --configuration Release"),
        ("Knowledge corpus structure", "scripts/knowledge verify-structure"),
        ("Pack twice and compare", "scripts/ci/reproducible-pack.sh"),
        ("Publish", "dotnet publish tests/Dexpace.Sdk.AotSmoke/Dexpace.Sdk.AotSmoke.csproj"),
        ("Run", "./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke"),
    ];

    /// <summary>The steps allowed an <c>if:</c>, and the condition: the Linux-only structural checks, and the upload that must run after a failure (it is not a gate).</summary>
    internal static IReadOnlyDictionary<string, string> Conditionals { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Knowledge corpus structure"] = "runner.os == 'Linux'",
        ["Upload test results"] = "always()",
    };

    [GeneratedRegex(@"^\s{6}- (?:name|uses):", RegexOptions.CultureInvariant)]
    private static partial Regex StepStart();

    [GeneratedRegex(@"^\s{6}- name:\s*(?<name>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex StepName();

    [GeneratedRegex(@"^\s+if:\s*(?<condition>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex IfLine();

    private static string Workflow => File.ReadAllText(RepositoryRoot.Of(".github/workflows/ci.yml"));

    [Fact]
    public void Every_gate_of_the_gate_table_is_a_ci_step()
    {
        var missing = MissingGates(Workflow);

        Assert.Empty(missing);
    }

    [Fact]
    public void No_step_or_job_is_advisory()
    {
        Assert.Empty(Advisory(Workflow));
        Assert.DoesNotContain("continue-on-error", Workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_documented_gates_are_conditional()
    {
        var undocumented = UndocumentedConditions(Workflow);

        Assert.Empty(undocumented);
        // The documented ones exist, so the allow-list is not vacuous.
        Assert.Contains("runner.os == 'Linux'", Workflow, StringComparison.Ordinal);
        Assert.Contains("if: always()", Workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void The_three_jobs_and_the_three_operating_systems_are_present()
    {
        var workflow = Workflow;

        Assert.Contains("name: build & test (${{ matrix.os }})", workflow, StringComparison.Ordinal);
        Assert.Contains("os: [ubuntu-latest, windows-latest, macos-latest]", workflow, StringComparison.Ordinal);
        Assert.Contains("name: reproducible packages", workflow, StringComparison.Ordinal);
        Assert.Contains("name: NativeAOT smoke", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void The_checks_can_fail()
    {
        var workflow = Workflow;

        // A gate step removed.
        var withoutCoverage = Regex.Replace(workflow, @"      - name: Coverage floor\r?\n        run: [^\r\n]+", string.Empty, RegexOptions.CultureInvariant);
        Assert.NotEqual(workflow, withoutCoverage);
        Assert.Contains(MissingGates(withoutCoverage), static m => m.Contains("Coverage floor", StringComparison.Ordinal));

        // A gate whose command was edited away.
        var weakened = workflow.Replace("--verify-no-changes --no-restore\n", "--no-restore\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, weakened);
        Assert.Contains(MissingGates(weakened), static m => m.Contains("Format", StringComparison.Ordinal));

        // An advisory step.
        var advisory = workflow.Replace("      - name: Dependency audit\n", "      - name: Dependency audit\n        continue-on-error: true\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, advisory);
        Assert.NotEmpty(Advisory(advisory));

        // A conditional gate.
        var conditional = workflow.Replace("      - name: Pack\n", "      - name: Pack\n        if: github.event_name == 'push'\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, conditional);
        Assert.Contains(UndocumentedConditions(conditional), static c => c.Contains("Pack", StringComparison.Ordinal));
    }

    private static List<(string Name, string Text)> Steps(string workflow)
    {
        var lines = workflow.Split('\n');
        var starts = Enumerable.Range(0, lines.Length).Where(i => StepStart().IsMatch(lines[i])).Append(lines.Length).ToArray();
        var steps = new List<(string, string)>();
        for (var s = 0; s < starts.Length - 1; s++)
        {
            var text = string.Join('\n', lines[starts[s]..starts[s + 1]]);
            var name = StepName().Match(lines[starts[s]]);
            if (name.Success)
            {
                steps.Add((name.Groups["name"].Value, text));
            }
        }

        return steps;
    }

    private static List<string> MissingGates(string workflow)
    {
        var steps = Steps(workflow);
        return
        [
            .. Gates
                .Where(gate => !steps.Any(step => step.Name == gate.Step && step.Text.Contains("run:", StringComparison.Ordinal) && Flatten(step.Text).Contains(gate.Command, StringComparison.Ordinal)))
                .Select(static gate => $"{gate.Step}: {gate.Command}"),
        ];
    }

    // A folded scalar (">-") wraps one command over lines; the command is compared with its lines joined by single spaces.
    private static string Flatten(string text) => Regex.Replace(text, @"\s*\r?\n\s*", " ", RegexOptions.CultureInvariant);

    private static List<string> Advisory(string workflow) =>
        [.. workflow.Split('\n').Select((line, i) => (line, i)).Where(static entry => entry.line.Contains("continue-on-error", StringComparison.Ordinal)).Select(static entry => $"line {entry.i + 1}: {entry.line.Trim()}")];

    private static List<string> UndocumentedConditions(string workflow)
    {
        var problems = new List<string>();
        foreach (var (name, text) in Steps(workflow))
        {
            foreach (var line in text.Split('\n'))
            {
                var condition = IfLine().Match(line);
                if (!condition.Success)
                {
                    continue;
                }

                if (!Conditionals.TryGetValue(name, out var allowed) || allowed != condition.Groups["condition"].Value)
                {
                    problems.Add($"{name}: if: {condition.Groups["condition"].Value}");
                }
            }
        }

        return problems;
    }
}
