// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Housekeeping;

/// <summary>Runs the checks and collects their findings. Read-only.</summary>
internal sealed class Probe
{
    private static readonly Func<Check>[] s_all =
    [
        () => new Inbox(), () => new Root(), () => new Claims(), () => new Readmes(), () => new Links(),
        () => new Registers(), () => new Citations(), () => new GuardCheck(), () => new Chapters(),
    ];

    private readonly Repo _repo;
    private readonly IReadOnlyList<string> _selected;

    /// <summary>
    /// A probe over <paramref name="repo"/>, running <paramref name="only"/> when given; throws
    /// <see cref="ArgumentException"/> naming any unknown check.
    /// </summary>
    public Probe(Repo repo, IReadOnlyCollection<string>? only = null)
    {
        _repo = repo;
        var unknown = (only ?? []).Except(Names, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"unknown check(s): {string.Join(", ", unknown)}. Known: {string.Join(", ", Names)}");
        }

        _selected = only is null || only.Count == 0 ? Names : [.. only];
    }

    /// <summary>Every check's name, in run order.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. s_all.Select(create => create().Name)];

    /// <summary>The checks this probe will run, in run order.</summary>
    public IReadOnlyList<Check> Checks() => [.. s_all.Select(create => create()).Where(check => _selected.Contains(check.Name))];

    /// <summary>Every finding of every selected check.</summary>
    public IReadOnlyList<Finding> Run() => [.. Checks().SelectMany(check => check.Run(_repo))];
}

/// <summary>The probe's command line: argument parsing, rendering and the exit code.</summary>
internal static class ProbeCli
{
    /// <summary>The usage text.</summary>
    public const string Usage =
        "usage: dotnet run --project .claude/skills/housekeeping/src -- probe [options]\n" +
        "  --only CHECKS   run only these checks (comma-separated)\n" +
        "  --json          emit findings as JSON\n" +
        "  --warn-only     report findings but exit 0\n" +
        "  --root PATH     probe this tree instead of the enclosing repository\n" +
        "  -h, --help      this message";

    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    /// <summary>0 when clean (or <c>--warn-only</c>), 1 on any finding, 2 on a usage error.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var options = Parse(args);
            if (options.Help)
            {
                output.WriteLine(Usage);
                output.WriteLine($"  checks: {string.Join(",", Probe.Names)}");
                return 0;
            }

            var repo = new Repo(options.Root ?? Git.RepositoryRoot());
            var findings = new Probe(repo, options.Only).Run();
            if (options.Json)
            {
                RenderJson(output, repo, options.Only, findings);
            }
            else
            {
                RenderText(output, repo, findings);
            }

            return findings.Count == 0 || options.WarnOnly ? 0 : 1;
        }
        catch (ArgumentException e)
        {
            error.WriteLine(e.Message);
            return 2;
        }
    }

    private static ProbeOptions Parse(IReadOnlyList<string> args)
    {
        var options = new ProbeOptions();
        var reader = new ArgumentReader(args);
        while (reader.Next() is { } arg)
        {
            switch (arg.Name)
            {
                case "--only":
                    options.Only = [.. reader.Value(arg).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0)];
                    break;
                case "--json":
                    options.Json = true;
                    break;
                case "--warn-only":
                    options.WarnOnly = true;
                    break;
                case "--root":
                    options.Root = reader.Value(arg);
                    break;
                case "-h" or "--help":
                    options.Help = true;
                    break;
                default:
                    throw new ArgumentException($"invalid option: {arg.Name}\n{Usage}");
            }
        }

        return options;
    }

    private static void RenderJson(TextWriter output, Repo repo, IReadOnlyList<string>? only, IReadOnlyList<Finding> findings)
    {
        var document = new JsonObject
        {
            ["root"] = repo.Root,
            ["checks"] = new JsonArray([.. (only ?? Probe.Names).Select(name => (JsonNode?)name)]),
            ["findings"] = new JsonArray([.. findings.Select(f => (JsonNode?)new JsonObject
            {
                ["check"] = f.Check,
                ["severity"] = f.Severity,
                ["path"] = f.Path,
                ["line"] = f.Line,
                ["message"] = f.Message,
            })]),
            ["summary"] = new JsonObject
            {
                ["findings"] = findings.Count,
                ["checks_with_findings"] = findings.Select(f => f.Check).Distinct(StringComparer.Ordinal).Count(),
            },
        };
        output.WriteLine(document.ToJsonString(s_indented));
    }

    private static void RenderText(TextWriter output, Repo repo, IReadOnlyList<Finding> findings)
    {
        output.WriteLine("housekeeping probe -- read-only");
        output.WriteLine($"repository: {repo.Root}");
        output.WriteLine();
        if (findings.Count == 0)
        {
            output.WriteLine("no drift found.");
            return;
        }

        foreach (var group in findings.GroupBy(f => f.Check, StringComparer.Ordinal))
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"## {group.Key} ({group.Count()})"));
            foreach (var finding in group)
            {
                output.WriteLine($"  {finding}");
            }

            output.WriteLine();
        }

        var checks = findings.Select(f => f.Check).Distinct(StringComparer.Ordinal).Count();
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{findings.Count} finding(s) across {checks} check(s). This stage writes nothing -- read them, then run the apply stage for the one mechanical repair."));
    }

    private sealed class ProbeOptions
    {
        public IReadOnlyList<string>? Only { get; set; }

        public bool Json { get; set; }

        public bool WarnOnly { get; set; }

        public string? Root { get; set; }

        public bool Help { get; set; }
    }
}

/// <summary>One parsed argument: its name, and a value when it was written <c>--name=value</c>.</summary>
internal readonly record struct Argument(string Name, string? InlineValue);

/// <summary>
/// A minimal GNU-style reader: <c>--name value</c> and <c>--name=value</c>. Hand-rolled because the
/// tool references no package, and <c>System.CommandLine</c> is one.
/// </summary>
internal sealed class ArgumentReader(IReadOnlyList<string> args)
{
    private int _index;

    /// <summary>The next argument, or <c>null</c> at the end.</summary>
    public Argument? Next()
    {
        if (_index >= args.Count)
        {
            return null;
        }

        var raw = args[_index++];
        var equals = raw.StartsWith("--", StringComparison.Ordinal) ? raw.IndexOf('=', StringComparison.Ordinal) : -1;
        return equals < 0 ? new Argument(raw, null) : new Argument(raw[..equals], raw[(equals + 1)..]);
    }

    /// <summary>The value of <paramref name="arg"/>, inline or the following argument.</summary>
    public string Value(Argument arg)
    {
        if (arg.InlineValue is not null)
        {
            return arg.InlineValue;
        }

        if (_index >= args.Count)
        {
            throw new ArgumentException($"missing argument: {arg.Name}");
        }

        return args[_index++];
    }
}
