// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// NFR-17's first half (phase 10, P10-16, plan reading R8): every gate of the gate table is a step of <c>.github/workflows/ci.yml</c>, and no step or job is
/// advisory. The second half, that a merge requires the checks, is a repository setting outside the file, recorded in the phase checklist. The workflow is read
/// as text (no YAML package is added): the rules are line-based and documented here. A gate added to the workflow without a row below is not checked, and a row
/// whose step is renamed or loses its command fails: this is the "every blocking gate stays blocking" pin. Two more pins keep the gates reachable:
/// the workflow must trigger on <c>push</c> and <c>pull_request</c> to <c>main</c> with no filter that skips a pull request, and no job may carry a
/// job-level <c>if:</c>, <c>continue-on-error</c> or matrix <c>exclude:</c> that would stop a job, or one operating-system leg, from running.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class CiWorkflowTests
{
    /// <summary>
    /// The gates: the step's name, and the command that must appear in its <c>run:</c>. The requirement-coverage gates joined with the commit that added
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
        ("Dependency audit - self-test", "scripts/ci/dependency-audit-selftest.sh artifacts/packages"),
        ("Packaging conformance", "dotnet run scripts/ci/packaging-conformance.cs -- artifacts/packages"),
        ("Tools - restore (locked)", "dotnet restore tools/Dexpace.Tools.sln --locked-mode"),
        ("Tools - build", "dotnet build tools/Dexpace.Tools.sln --configuration Release"),
        ("Tools - format", "dotnet format tools/Dexpace.Tools.sln --verify-no-changes"),
        ("Tools - test", "dotnet test --solution tools/Dexpace.Tools.sln --configuration Release"),
        ("Knowledge corpus structure", "scripts/knowledge verify-structure"),
        ("Requirement coverage self-test", "scripts/ci/requirement-coverage-selftest.sh"),
        ("Requirement coverage", "dotnet run scripts/ci/requirement-coverage.cs -- --check"),
        ("Section 12", "dotnet run scripts/ci/requirement-coverage.cs -- --check-section12"),
        ("Pack twice and compare", "scripts/ci/reproducible-pack.sh"),
        ("Pack twice and compare - self-test", "scripts/ci/reproducible-pack.sh --self-test"),
        ("Publish", "dotnet publish tests/Dexpace.Sdk.AotSmoke/Dexpace.Sdk.AotSmoke.csproj"),
        ("Run", "./artifacts/aot-smoke/Dexpace.Sdk.AotSmoke"),
    ];

    /// <summary>The steps allowed an <c>if:</c>, and the condition: the Linux-only structural checks, and the upload that must run after a failure (it is not a gate).</summary>
    internal static IReadOnlyDictionary<string, string> Conditionals { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Knowledge corpus structure"] = "runner.os == 'Linux'",
        ["Requirement coverage self-test"] = "runner.os == 'Linux'",
        ["Requirement coverage"] = "runner.os == 'Linux'",
        ["Section 12"] = "runner.os == 'Linux'",
        ["Upload test results"] = "always()",
    };

    [GeneratedRegex(@"^\s{6}- (?:name|uses):", RegexOptions.CultureInvariant)]
    private static partial Regex StepStart();

    [GeneratedRegex(@"^\s{6}- name:\s*(?<name>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex StepName();

    [GeneratedRegex(@"^\s+if:\s*(?<condition>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex IfLine();

    [GeneratedRegex(@"^on:\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex OnBlockStart();

    [GeneratedRegex(@"^ {2}(?<event>[a-z_]+):\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TriggerEvent();

    [GeneratedRegex(@"^ {4}(?<key>[a-z-]+):\s*(?<value>.*?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TriggerFilter();

    [GeneratedRegex(@"^ {2}(?<id>[A-Za-z0-9_-]+):\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JobHeader();

    [GeneratedRegex(@"^ {4}steps:\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex StepsKey();

    [GeneratedRegex(@"^\s+(?:if|continue-on-error|exclude):", RegexOptions.CultureInvariant)]
    private static partial Regex JobGuard();

    private static string Workflow => File.ReadAllText(RepositoryRoot.Of(".github/workflows/ci.yml")).ReplaceLineEndings("\n");

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
    public void The_workflow_runs_on_every_pull_request_and_every_push_to_main()
    {
        Assert.Empty(TriggerProblems(Workflow));
    }

    [Fact]
    public void No_job_can_be_skipped_whole_or_dropped_from_the_matrix()
    {
        var workflow = Workflow;

        Assert.Empty(JobProblems(workflow));
        // The jobs the other pins name exist, so the scan is not vacuous.
        Assert.Equal(["build", "reproducible", "aot-smoke"], Jobs(workflow).Select(static job => job.Id));
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

        // A workflow that no longer runs on pull requests.
        var withoutPullRequests = workflow.Replace("  pull_request:\n    branches: [main]\n", string.Empty, StringComparison.Ordinal);
        Assert.NotEqual(workflow, withoutPullRequests);
        Assert.Contains(TriggerProblems(withoutPullRequests), static p => p.Contains("pull_request", StringComparison.Ordinal));

        // A trigger that skips some pull requests, or runs for another branch only.
        var filtered = workflow.Replace("  pull_request:\n    branches: [main]\n", "  pull_request:\n    branches: [main]\n    paths-ignore: ['docs/**']\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, filtered);
        Assert.Contains(TriggerProblems(filtered), static p => p.Contains("paths-ignore", StringComparison.Ordinal));
        var elsewhere = workflow.Replace("  push:\n    branches: [main]\n", "  push:\n    branches: [release]\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, elsewhere);
        Assert.Contains(TriggerProblems(elsewhere), static p => p.Contains("push", StringComparison.Ordinal));

        // A job that is skipped on pull requests, whichever job it is, and a job that cannot fail the run.
        var skippedBuild = workflow.Replace("    runs-on: ${{ matrix.os }}\n", "    runs-on: ${{ matrix.os }}\n    if: github.event_name == 'push'\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, skippedBuild);
        Assert.Contains(JobProblems(skippedBuild), static p => p.Contains("build", StringComparison.Ordinal));
        var skippedReproducible = workflow.Replace("    name: reproducible packages\n", "    name: reproducible packages\n    if: github.event_name == 'push'\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, skippedReproducible);
        Assert.Contains(JobProblems(skippedReproducible), static p => p.Contains("reproducible", StringComparison.Ordinal));
        var tolerant = workflow.Replace("    name: NativeAOT smoke\n", "    name: NativeAOT smoke\n    continue-on-error: true\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, tolerant);
        Assert.Contains(JobProblems(tolerant), static p => p.Contains("aot-smoke", StringComparison.Ordinal));

        // A matrix leg removed.
        var excluded = workflow.Replace("        os: [ubuntu-latest, windows-latest, macos-latest]\n", "        os: [ubuntu-latest, windows-latest, macos-latest]\n        exclude:\n          - os: windows-latest\n", StringComparison.Ordinal);
        Assert.NotEqual(workflow, excluded);
        Assert.Contains(JobProblems(excluded), static p => p.Contains("exclude", StringComparison.Ordinal));
    }

    private sealed record Job(string Id, string Settings, List<(string Name, string Text)> Steps);

    // The jobs, each as its own settings block (everything before its steps: name, strategy, runs-on, ...) and its own steps, so a job-level
    // line is never read as part of the previous job's last step.
    private static List<Job> Jobs(string workflow)
    {
        var lines = workflow.Split('\n');
        var first = Array.FindIndex(lines, static line => line.TrimEnd() == "jobs:");
        Assert.True(first >= 0, "ci.yml has no jobs: block");
        var headers = Enumerable.Range(first + 1, lines.Length - first - 1).Where(i => JobHeader().IsMatch(lines[i])).Append(lines.Length).ToArray();
        var jobs = new List<Job>();
        for (var j = 0; j < headers.Length - 1; j++)
        {
            var body = lines[headers[j]..headers[j + 1]];
            var steps = Array.FindIndex(body, static line => StepsKey().IsMatch(line));
            var settings = steps < 0 ? body : body[..steps];
            var stepLines = steps < 0 ? [] : body[steps..];
            jobs.Add(new Job(JobHeader().Match(lines[headers[j]]).Groups["id"].Value, string.Join('\n', settings), StepsOf(stepLines)));
        }

        return jobs;
    }

    // The triggers: each event under on:, with the filters it carries. Only push and pull_request to main, with no other filter, are accepted.
    private static List<string> TriggerProblems(string workflow)
    {
        var lines = workflow.Split('\n');
        var start = Array.FindIndex(lines, static line => OnBlockStart().IsMatch(line));
        if (start < 0)
        {
            return ["there is no on: block"];
        }

        var events = new Dictionary<string, List<(string Key, string Value)>>(StringComparer.Ordinal);
        List<(string Key, string Value)>? current = null;
        foreach (var line in lines.Skip(start + 1).TakeWhile(static line => line.Length == 0 || line.StartsWith(' ') || line.StartsWith('#')))
        {
            if (TriggerEvent().Match(line) is { Success: true } trigger)
            {
                current = [];
                events[trigger.Groups["event"].Value] = current;
            }
            else if (TriggerFilter().Match(line) is { Success: true } filter && current is not null)
            {
                current.Add((filter.Groups["key"].Value, filter.Groups["value"].Value));
            }
        }

        var problems = new List<string>();
        foreach (var name in new[] { "push", "pull_request" })
        {
            if (!events.TryGetValue(name, out var filters))
            {
                problems.Add($"the workflow does not run on {name}");
                continue;
            }

            problems.AddRange(filters.Where(static f => f.Key != "branches").Select(f => $"{name} carries the filter {f.Key}: a run can be skipped"));
            if (!filters.Contains(("branches", "[main]")))
            {
                problems.Add($"{name} does not run for exactly the branch main (branches: [main])");
            }
        }

        return problems;
    }

    // A job-level if:, continue-on-error or matrix exclude: stops a job, or one operating-system leg of it, from running or from failing the run.
    private static List<string> JobProblems(string workflow) =>
        [.. Jobs(workflow).SelectMany(job => job.Settings.Split('\n').Where(static line => JobGuard().IsMatch(line)).Select(line => $"job {job.Id}: {line.Trim()}"))];

    private static List<(string Name, string Text)> Steps(string workflow) => [.. Jobs(workflow).SelectMany(static job => job.Steps)];

    private static List<(string Name, string Text)> StepsOf(string[] lines)
    {
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
