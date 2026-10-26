// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Gates;

/// <summary>
/// NFR-16 and NFR-12's release half (phase 12, task 3.2, P12-2 to P12-4; design V16): <c>.github/workflows/release.yml</c> is inert until a registry exists, and this
/// pins the facts that keep it so and keep it a release path. <see cref="CiWorkflowTests"/> reads <c>ci.yml</c> only, so the release workflow needs its own test, in
/// the same style: the file is read as text (no YAML package is added), the rules are line-based and documented here, and
/// the four <c>*_checks_can_fail</c> facts edit copies to prove each rule can fail. The pins: exactly two triggers (a <c>v*</c> tag push, and a manual run whose boolean
/// <c>dry_run</c> defaults to <c>true</c>) with no <c>branches</c> and no <c>pull_request</c>; <c>contents: read</c> at the top and <c>id-token: write</c> and
/// <c>attestations: write</c> in the publish job only; no repository credential and no tolerated failure; the publish job's condition, its needs and its
/// environment; the build job's locked restore, pinned epoch, reproducible pack, audit, conformance, preflight and checksums; the publish job's guard as its first step
/// and the verify, attest, login and push after it, in order; and the header that says the workflow is inert. Comment lines are not code: the permission, credential
/// and tolerance rules read the lines that are not comments.
/// </summary>
[Trait("Category", "Unit")]
public sealed partial class ReleaseWorkflowTests
{
    [GeneratedRegex(@"^ {2}(?<id>[A-Za-z0-9_-]+):\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JobHeader();

    [GeneratedRegex(@"^ {2}(?<event>[a-z_]+):\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TriggerEvent();

    [GeneratedRegex(@"^ {4}(?<key>[A-Za-z_-]+):\s*(?<value>.*?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TriggerKey();

    [GeneratedRegex(@"^ {4}(?<key>[A-Za-z-]+):\s*(?<value>.*?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex JobKey();

    [GeneratedRegex(@"^ {6}- (?:name|uses):", RegexOptions.CultureInvariant)]
    private static partial Regex StepStart();

    [GeneratedRegex(@"^ {6}- name:\s*(?<name>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex StepName();

    private static string Workflow => File.ReadAllText(RepositoryRoot.Of(".github/workflows/release.yml")).ReplaceLineEndings("\n");

    [Fact]
    public void The_header_says_the_workflow_is_inert_and_names_the_three_unmet_conditions()
    {
        Assert.Empty(HeaderProblems(Workflow));
    }

    [Fact]
    public void The_workflow_has_exactly_two_triggers_a_tag_and_a_dry_run_by_default_dispatch()
    {
        Assert.Empty(TriggerProblems(Workflow));
    }

    [Fact]
    public void Permissions_are_read_only_at_the_top_and_only_the_publish_job_may_mint_a_token_or_attest()
    {
        Assert.Empty(PermissionProblems(Workflow));
    }

    [Fact]
    public void No_registry_credential_is_read_and_no_failure_is_tolerated()
    {
        Assert.Empty(CredentialProblems(Workflow));
    }

    [Fact]
    public void The_publish_job_runs_only_for_a_tag_after_the_build_in_the_protected_environment()
    {
        Assert.Empty(PublishJobProblems(Workflow));
    }

    [Fact]
    public void The_build_job_is_the_release_candidate()
    {
        Assert.Empty(BuildJobProblems(Workflow));
    }

    [Fact]
    public void The_publish_job_guards_first_then_verifies_attests_logs_in_and_pushes_in_that_order()
    {
        Assert.Empty(PublishStepProblems(Workflow));
    }

    [Fact]
    public void The_two_jobs_are_present_and_the_scan_is_not_vacuous()
    {
        var jobs = Jobs(Workflow);

        Assert.Equal(["build", "publish"], jobs.Select(static job => job.Id));
        Assert.True(Step(jobs[0], "Pack twice and compare") is not null, "the build job lost its pack step");
        Assert.True(jobs[1].Steps.Count >= 6, "the publish job holds fewer than six steps");
    }

    [Fact]
    public void The_credential_and_guard_checks_can_fail()
    {
        var workflow = Workflow;

        // The guard removed.
        var withoutGuard = Regex.Replace(workflow, @"      - name: Guard[^\n]*\n(?:        [^\n]*\n|\n)+?(?=      - name: Download packages)", string.Empty, RegexOptions.CultureInvariant);
        Assert.NotEqual(workflow, withoutGuard);
        Assert.Contains(PublishStepProblems(withoutGuard), static p => p.Contains("guard", StringComparison.OrdinalIgnoreCase));

        // A guard that no longer fails.
        var mute = Mutate(workflow, "            exit 1\n", "            exit 0\n");
        Assert.Contains(PublishStepProblems(mute), static p => p.Contains("exit 1", StringComparison.Ordinal));

        // A long-lived registry key, and a tolerated failure.
        var withKey = Mutate(workflow, "NUGET_API_KEY: ${{ steps.login.outputs.NUGET_API_KEY }}", "NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}");
        Assert.Contains(CredentialProblems(withKey), static p => p.Contains("secrets.", StringComparison.Ordinal));
        var tolerant = Mutate(workflow, "      - name: Verify checksums\n", "      - name: Verify checksums\n        continue-on-error: true\n");
        Assert.Contains(CredentialProblems(tolerant), static p => p.Contains("continue-on-error", StringComparison.Ordinal));

        // id-token: write at the top level, and in the build job.
        var topLevel = Mutate(workflow, "permissions:\n  contents: read\n\nconcurrency:", "permissions:\n  contents: read\n  id-token: write\n\nconcurrency:");
        Assert.Contains(PermissionProblems(topLevel), static p => p.Contains("top", StringComparison.OrdinalIgnoreCase));
        var inBuild = Mutate(workflow, "    timeout-minutes: 30\n", "    timeout-minutes: 30\n    permissions:\n      id-token: write\n");
        Assert.Contains(PermissionProblems(inBuild), static p => p.Contains("id-token", StringComparison.Ordinal));
    }

    [Fact]
    public void The_trigger_and_publish_job_checks_can_fail()
    {
        var workflow = Workflow;

        // A branch filter on the tag trigger, a pull request trigger, and a dry run that publishes by default.
        var branches = Mutate(workflow, "    tags: [\"v*\"]\n", "    tags: [\"v*\"]\n    branches: [main]\n");
        Assert.Contains(TriggerProblems(branches), static p => p.Contains("branches", StringComparison.Ordinal));
        var pullRequest = Mutate(workflow, "  workflow_dispatch:\n", "  pull_request:\n    branches: [main]\n  workflow_dispatch:\n");
        Assert.Contains(TriggerProblems(pullRequest), static p => p.Contains("pull_request", StringComparison.Ordinal));
        var live = Mutate(workflow, "        default: true\n", "        default: false\n");
        Assert.Contains(TriggerProblems(live), static p => p.Contains("dry_run", StringComparison.Ordinal));

        // A publish job that no longer needs a tag, the build or the environment.
        var anyRef = Mutate(workflow, "github.ref_type == 'tag' && ", string.Empty);
        Assert.Contains(PublishJobProblems(anyRef), static p => p.Contains("if:", StringComparison.Ordinal));
        var unprotected = Mutate(workflow, "    environment: nuget\n", string.Empty);
        Assert.Contains(PublishJobProblems(unprotected), static p => p.Contains("environment", StringComparison.Ordinal));
        var unordered = Mutate(workflow, "    needs: build\n", string.Empty);
        Assert.Contains(PublishJobProblems(unordered), static p => p.Contains("needs", StringComparison.Ordinal));
    }

    [Fact]
    public void The_build_job_and_step_order_checks_can_fail()
    {
        var workflow = Workflow;

        // A build job that lost the epoch, the locked restore or the preflight.
        var withoutEpoch = Mutate(workflow, "SOURCE_DATE_EPOCH", "EPOCH");
        Assert.Contains(BuildJobProblems(withoutEpoch), static p => p.Contains("SOURCE_DATE_EPOCH", StringComparison.Ordinal));
        var unlocked = Mutate(workflow, "--locked-mode", string.Empty);
        Assert.Contains(BuildJobProblems(unlocked), static p => p.Contains("--locked-mode", StringComparison.Ordinal));
        var withoutPreflight = Mutate(workflow, "scripts/ci/release-preflight.cs", "scripts/ci/other.cs");
        Assert.Contains(BuildJobProblems(withoutPreflight), static p => p.Contains("release-preflight", StringComparison.Ordinal));

        // The attestation after the login, and a publish job that checks out the repository.
        var swapped = SwapSteps(workflow, "Attest build provenance", "NuGet login (trusted publishing)");
        Assert.NotEqual(workflow, swapped);
        Assert.Contains(PublishStepProblems(swapped), static p => p.Contains("order", StringComparison.Ordinal));
        var checkedOut = Mutate(workflow, "      - name: Download packages\n", "      - name: Checkout\n        uses: actions/checkout@v7\n\n      - name: Download packages\n");
        Assert.Contains(PublishStepProblems(checkedOut), static p => p.Contains("checks out", StringComparison.Ordinal));
    }

    [Fact]
    public void The_header_check_can_fail()
    {
        var loud = Mutate(Workflow, "THIS WORKFLOW IS INERT", "THIS WORKFLOW IS LIVE");

        Assert.Contains(HeaderProblems(loud), static p => p.Contains("INERT", StringComparison.Ordinal));
    }

    // The workflow with one edit, which must change it (so a renamed anchor fails here rather than passing vacuously).
    private static string Mutate(string workflow, string find, string replacement)
    {
        var edited = workflow.Replace(find, replacement, StringComparison.Ordinal);
        Assert.NotEqual(workflow, edited);
        return edited;
    }

    private sealed record Job(string Id, List<string> Lines, List<(string Name, string Text)> Steps);

    // The lines that are code: comment-only lines are dropped.
    private static IEnumerable<string> Code(string text) => text.Split('\n').Where(static line => !line.TrimStart().StartsWith('#'));

    private static List<Job> Jobs(string workflow)
    {
        var lines = workflow.Split('\n');
        var first = Array.FindIndex(lines, static line => line.TrimEnd() == "jobs:");
        Assert.True(first >= 0, "release.yml has no jobs: block");
        var headers = Enumerable.Range(first + 1, lines.Length - first - 1).Where(i => JobHeader().IsMatch(lines[i])).Append(lines.Length).ToArray();
        var jobs = new List<Job>();
        for (var j = 0; j < headers.Length - 1; j++)
        {
            var body = lines[headers[j]..headers[j + 1]];
            jobs.Add(new Job(JobHeader().Match(lines[headers[j]]).Groups["id"].Value, [.. body], StepsOf(body)));
        }

        return jobs;
    }

    private static List<(string Name, string Text)> StepsOf(string[] body)
    {
        var steps = Array.FindIndex(body, static line => line.TrimEnd() == "    steps:");
        if (steps < 0)
        {
            return [];
        }

        var lines = body[steps..];
        var starts = Enumerable.Range(0, lines.Length).Where(i => StepStart().IsMatch(lines[i])).Append(lines.Length).ToArray();
        var result = new List<(string, string)>();
        for (var s = 0; s < starts.Length - 1; s++)
        {
            var name = StepName().Match(lines[starts[s]]);
            result.Add((name.Success ? name.Groups["name"].Value : "(unnamed)", string.Join('\n', lines[starts[s]..starts[s + 1]])));
        }

        return result;
    }

    private static string? Step(Job job, string name) => job.Steps.FirstOrDefault(step => step.Name == name).Text;

    // The value of a job-level key (indent four), or null.
    private static string? JobKeyValue(Job job, string key) =>
        job.Lines.Select(static line => JobKey().Match(line)).Where(match => match.Success && match.Groups["key"].Value == key).Select(static match => match.Groups["value"].Value).FirstOrDefault();

    private static string Flatten(string text) => Regex.Replace(text, @"\s*\n\s*", " ", RegexOptions.CultureInvariant);

    private static string SwapSteps(string workflow, string first, string second)
    {
        var job = Jobs(workflow).Single(static job => job.Id == "publish");
        var a = Step(job, first)!;
        var b = Step(job, second)!;
        return workflow.Replace(a, "@@A@@", StringComparison.Ordinal).Replace(b, a, StringComparison.Ordinal).Replace("@@A@@", b, StringComparison.Ordinal);
    }

    private static List<string> HeaderProblems(string workflow)
    {
        var header = string.Join('\n', workflow.Split('\n').TakeWhile(static line => line.StartsWith('#') || line.Length == 0));
        var problems = new List<string>();
        foreach (var required in new[] { "THIS WORKFLOW IS INERT", "1. a nuget.org organisation owning the Dexpace.* ID prefix", "2. a nuget.org trusted-publishing policy", "3. the repository environment `nuget`", "UNVERIFIED" })
        {
            if (!header.Contains(required, StringComparison.Ordinal))
            {
                problems.Add($"the header does not carry \"{required}\" (INERT paragraph, P12-3)");
            }
        }

        return problems;
    }

    // The on: block: exactly push (tags: ["v*"] and nothing else) and workflow_dispatch (one input, dry_run, boolean, default true).
    private static List<string> TriggerProblems(string workflow)
    {
        var lines = workflow.Split('\n');
        var start = Array.FindIndex(lines, static line => line.TrimEnd() == "on:");
        if (start < 0)
        {
            return ["there is no on: block"];
        }

        var block = lines.Skip(start + 1).TakeWhile(static line => line.Length == 0 || line.StartsWith(' ') || line.StartsWith('#')).ToList();
        var events = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? current = null;
        foreach (var line in block)
        {
            if (TriggerEvent().Match(line) is { Success: true } trigger)
            {
                current = [];
                events[trigger.Groups["event"].Value] = current;
            }
            else if (current is not null && !line.TrimStart().StartsWith('#') && line.Trim().Length > 0)
            {
                current.Add(line);
            }
        }

        var problems = new List<string>();
        problems.AddRange(events.Keys.Where(static name => name is not ("push" or "workflow_dispatch")).Select(static name => $"the workflow also triggers on {name}: exactly a tag push and workflow_dispatch are allowed"));
        if (!events.TryGetValue("push", out var push))
        {
            problems.Add("the workflow does not trigger on push");
        }
        else
        {
            problems.AddRange(push.Select(static line => TriggerKey().Match(line)).Where(static m => m.Success && m.Groups["key"].Value != "tags").Select(static m => $"push carries the filter {m.Groups["key"].Value}: only tags: [\"v*\"] is allowed (no branches)"));
            if (!push.Any(static line => line.Trim() == "tags: [\"v*\"]"))
            {
                problems.Add("push does not run for exactly the tags [\"v*\"]");
            }
        }

        if (!events.TryGetValue("workflow_dispatch", out var dispatch))
        {
            problems.Add("the workflow does not trigger on workflow_dispatch");
        }
        else
        {
            var text = string.Join('\n', dispatch);
            if (!Regex.IsMatch(text, @"^ {6}dry_run:\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)
                || !Regex.IsMatch(text, @"^ {8}type: boolean\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)
                || !Regex.IsMatch(text, @"^ {8}default: true\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant))
            {
                problems.Add("workflow_dispatch has no boolean dry_run input whose default is true");
            }

            if (Regex.Count(text, @"^ {6}[a-z_]+:\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant) != 1)
            {
                problems.Add("workflow_dispatch takes inputs other than dry_run");
            }
        }

        return problems;
    }

    private static List<string> PermissionProblems(string workflow)
    {
        var problems = new List<string>();
        var code = Code(workflow).ToList();
        var top = code.FindIndex(static line => line.TrimEnd() == "permissions:");
        var topBlock = top < 0 ? [] : code.Skip(top + 1).TakeWhile(static line => line.StartsWith("  ", StringComparison.Ordinal)).Select(static line => line.Trim()).ToList();
        if (top < 0 || !topBlock.SequenceEqual(["contents: read"]))
        {
            problems.Add($"the top-level permissions are not exactly contents: read (found: {string.Join(", ", topBlock)}); a token-minting permission belongs to the publish job only");
        }

        foreach (var job in Jobs(workflow))
        {
            foreach (var permission in new[] { "id-token: write", "attestations: write" })
            {
                var held = Code(string.Join('\n', job.Lines)).Any(line => line.Trim() == permission);
                if (job.Id != "publish" && held)
                {
                    problems.Add($"job {job.Id} holds {permission}: only the publish job may");
                }

                if (job.Id == "publish" && !held)
                {
                    problems.Add($"the publish job does not hold {permission}");
                }
            }
        }

        return problems;
    }

    private static List<string> CredentialProblems(string workflow)
    {
        var problems = new List<string>();
        foreach (var line in Code(workflow))
        {
            if (line.Contains("secrets.", StringComparison.Ordinal))
            {
                problems.Add($"a repository credential is read: {line.Trim()} (the key comes from NuGet/login)");
            }

            if (line.Contains("continue-on-error", StringComparison.Ordinal))
            {
                problems.Add($"a failure is tolerated: {line.Trim()} (continue-on-error)");
            }
        }

        return problems;
    }

    private static List<string> PublishJobProblems(string workflow)
    {
        var problems = new List<string>();
        var publish = Jobs(workflow).SingleOrDefault(static job => job.Id == "publish");
        if (publish is null)
        {
            return ["there is no publish job"];
        }

        if (JobKeyValue(publish, "needs") != "build")
        {
            problems.Add("the publish job does not have needs: build");
        }

        if (JobKeyValue(publish, "environment") != "nuget")
        {
            problems.Add("the publish job does not have environment: nuget");
        }

        if (JobKeyValue(publish, "if") != "github.ref_type == 'tag' && (github.event_name == 'push' || inputs.dry_run == false)")
        {
            problems.Add("the publish job's if: is not github.ref_type == 'tag' && (github.event_name == 'push' || inputs.dry_run == false)");
        }

        if (Jobs(workflow).Where(static job => job.Id != "publish").Any(static job => JobKeyValue(job, "environment") is not null))
        {
            problems.Add("a job other than publish names an environment");
        }

        return problems;
    }

    private static List<string> BuildJobProblems(string workflow)
    {
        var problems = new List<string>();
        var build = Jobs(workflow).SingleOrDefault(static job => job.Id == "build");
        if (build is null)
        {
            return ["there is no build job"];
        }

        var text = Flatten(string.Join('\n', Code(string.Join('\n', build.Lines))));
        foreach (var (what, needle) in new[]
        {
            ("a full-history checkout", "fetch-depth: 0"),
            ("the locked restore (--locked-mode)", "--locked-mode"),
            ("SOURCE_DATE_EPOCH", "SOURCE_DATE_EPOCH"),
            ("the reproducible pack", "scripts/ci/reproducible-pack.sh artifacts/release"),
            ("the dependency audit", "dotnet run scripts/ci/dependency-audit.cs -- Release artifacts/release/1"),
            ("the packaging conformance", "dotnet run scripts/ci/packaging-conformance.cs -- artifacts/release/1"),
            ("the release preflight (release-preflight.cs)", "dotnet run scripts/ci/release-preflight.cs -- artifacts/release/1"),
            ("the release preflight with --tag on a tag ref", "--tag \"$GITHUB_REF_NAME\""),
            ("the SHA256SUMS file", "sha256sum *.nupkg *.snupkg > SHA256SUMS"),
            ("the upload of SHA256SUMS", "artifacts/release/1/SHA256SUMS"),
        })
        {
            if (!text.Contains(needle, StringComparison.Ordinal))
            {
                problems.Add($"the build job lost {what}: \"{needle}\"");
            }
        }

        return problems;
    }

    // The publish steps: the guard first (fails naming docs/first-release.md when vars.NUGET_USER is empty), then, in order, the checksum verification, the attestation, the login and
    // the push with --skip-duplicate.
    private static List<string> PublishStepProblems(string workflow)
    {
        var publish = Jobs(workflow).SingleOrDefault(static job => job.Id == "publish");
        if (publish is null)
        {
            return ["there is no publish job"];
        }

        var problems = new List<string>();
        if (publish.Steps.Count == 0 || !publish.Steps[0].Name.StartsWith("Guard", StringComparison.Ordinal))
        {
            problems.Add("the publish job's first step is not the guard");
        }
        else
        {
            var guard = publish.Steps[0].Text;
            foreach (var needle in new[] { "vars.NUGET_USER", "docs/first-release.md", "exit 1" })
            {
                if (!guard.Contains(needle, StringComparison.Ordinal))
                {
                    problems.Add($"the guard does not contain \"{needle}\"");
                }
            }
        }

        var ordered = new (string Needle, string What)[]
        {
            ("sha256sum --check SHA256SUMS", "the checksum verification"),
            ("actions/attest-build-provenance@", "the attestation"),
            ("NuGet/login@", "NuGet/login"),
            ("dotnet nuget push", "dotnet nuget push"),
        };
        var previous = 0;
        foreach (var (needle, what) in ordered)
        {
            var index = publish.Steps.FindIndex(step => step.Text.Contains(needle, StringComparison.Ordinal));
            if (index < 0)
            {
                problems.Add($"the publish job has no step for {what} (\"{needle}\")");
            }
            else if (index < previous)
            {
                problems.Add($"{what} is out of order: the steps must be guard, verify, attest, login, push");
            }
            else
            {
                previous = index;
            }
        }

        var push = publish.Steps.FirstOrDefault(static step => step.Text.Contains("dotnet nuget push", StringComparison.Ordinal)).Text;
        if (push is not null && (!push.Contains("--skip-duplicate", StringComparison.Ordinal) || !push.Contains("*.nupkg", StringComparison.Ordinal)))
        {
            problems.Add("the push does not name *.nupkg with --skip-duplicate");
        }

        if (publish.Steps.Any(static step => step.Text.Contains("actions/checkout", StringComparison.Ordinal) || step.Text.Contains("setup-dotnet", StringComparison.Ordinal)))
        {
            problems.Add("the publish job checks out or sets up .NET: it must need nothing from the repository but the artifact");
        }

        return problems;
    }
}
