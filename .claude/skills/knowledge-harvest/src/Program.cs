// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KnowledgeHarvest;

/// <summary>
/// The entry point: <c>stats</c>, <c>pack</c>, <c>collect</c> (the measuring, routing and assembling
/// steps) and <c>merge</c> (the one writer of the corpus).
/// </summary>
/// <remarks>
/// <code>
/// dotnet run --project .claude/skills/knowledge-harvest/src -- stats --path docs/product-spec --as spec [--exclude glob] -o stats.json
/// dotnet run --project .claude/skills/knowledge-harvest/src -- pack --stats stats.json --routing routing.json -o manifest.json
/// dotnet run --project .claude/skills/knowledge-harvest/src -- collect --manifest manifest.json --outputs DIR [--topics map.json] [--extra decisions.json] -o entries.json
/// dotnet run --project .claude/skills/knowledge-harvest/src -- merge entries.json --corpus docs/knowledge/harvested [--dry-run] [--force]
/// </code>
/// </remarks>
internal static class Program
{
    private const string Usage =
        "usage: dotnet run --project .claude/skills/knowledge-harvest/src -- <stats|pack|collect|merge> [options]\n" +
        "  stats    measure roots: sizes, hashes, heading offsets\n" +
        "  pack     apply a routing to the stats and emit extractor chunks\n" +
        "  collect  turn the extractors' output into entries.json, validating every citation\n" +
        "  merge    apply entries.json to a corpus (the only writer of topic files)";

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>Process entry point.</summary>
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>Dispatches to a command; 2 on an unknown command or a usage error.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        if (args.Count == 0)
        {
            error.WriteLine(Usage);
            return 2;
        }

        try
        {
            var rest = args.Skip(1).ToList();
            switch (args[0])
            {
                case "stats":
                    return StatsCommand(rest, output, error);
                case "pack":
                    return PackCommand(rest, output, error);
                case "collect":
                    return CollectCommand(rest, output, error);
                case "merge":
                    return MergeCommand(rest, output, error);
                case "-h" or "--help":
                    output.WriteLine(Usage);
                    return 0;
                default:
                    error.WriteLine($"unknown command: {args[0]}\n{Usage}");
                    return 2;
            }
        }
        catch (Scan.ScanException e)
        {
            error.WriteLine($"{args[0]}: {e.Message}");
            return 2;
        }
        catch (Exception e) when (e is ArgumentException or FileNotFoundException or JsonException or DirectoryNotFoundException)
        {
            error.WriteLine($"{args[0]}: {e.Message}");
            return 2;
        }
    }

    private static Dictionary<string, List<string>> Parse(IReadOnlyList<string> args, HashSet<string> flags)
    {
        var values = new Dictionary<string, List<string>>();
        string? lastPath = null;
        for (var index = 0; index < args.Count; index++)
        {
            var name = args[index];
            if (!name.StartsWith('-'))
            {
                values.GetOrAdd("positional").Add(name);
                continue;
            }

            name = name == "-o" ? "--out" : name;
            if (flags.Contains(name))
            {
                values.GetOrAdd(name).Add("true");
                continue;
            }

            if (index + 1 >= args.Count)
            {
                throw new ArgumentException($"{name} needs a value");
            }

            var value = args[++index];
            values.GetOrAdd(name).Add(name == "--path" ? lastPath = value : value);
            if (name == "--as" && lastPath is null)
            {
                throw new ArgumentException("--as must follow a --path");
            }
        }

        return values;
    }

    private static List<string> GetOrAdd(this Dictionary<string, List<string>> map, string key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            map[key] = list = [];
        }

        return list;
    }

    private static T ReadJson<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), s_json)
        ?? throw new JsonException($"{path} is empty");

    private static void WriteJson(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, s_json) + "\n", new UTF8Encoding(false));

    private static string Required(Dictionary<string, List<string>> values, string name) =>
        values.TryGetValue(name, out var list) ? list[^1] : throw new ArgumentException($"{name} is required");

    private static int StatsCommand(List<string> args, TextWriter output, TextWriter error)
    {
        // --as must follow its --path, so the pairs are read positionally rather than through Parse.
        var roots = new List<(string Path, string Role)>();
        var patterns = new List<string>();
        string? outPath = null;
        for (var index = 0; index < args.Count; index++)
        {
            var next = () => index + 1 < args.Count ? args[++index] : throw new ArgumentException($"{args[index]} needs a value");
            switch (args[index])
            {
                case "--path":
                    roots.Add((next(), "unspecified"));
                    break;
                case "--as" when roots.Count > 0:
                    roots[^1] = (roots[^1].Path, next());
                    break;
                case "--as":
                    throw new ArgumentException("--as must follow a --path");
                case "--exclude":
                    patterns.Add(next());
                    break;
                case "-o" or "--out":
                    outPath = next();
                    break;
                default:
                    throw new ArgumentException($"unknown option {args[index]}");
            }
        }

        if (roots.Count == 0 || outPath is null)
        {
            error.WriteLine("stats: at least one --path and an -o are required");
            return 2;
        }

        var stats = Scan.BuildStats(roots, patterns);
        if (stats.Files.Count == 0)
        {
            error.WriteLine("stats: no eligible files found (all excluded, all binary, or the paths are empty)");
            return 2;
        }

        WriteJson(outPath, stats);
        output.WriteLine($"scanned {stats.Totals.Files} files, {stats.Totals.Bytes} bytes -> {outPath}");
        return 0;
    }

    private static int PackCommand(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var values = Parse(args, new HashSet<string>());
        var manifest = Scan.Pack(ReadJson<Stats>(Required(values, "--stats")), ReadJson<Routing>(Required(values, "--routing")));
        var outPath = Required(values, "--out");
        WriteJson(outPath, manifest);
        output.WriteLine($"packed {manifest.Totals.Chunks} chunks ({manifest.Totals.Haiku} haiku, {manifest.Totals.Sonnet} sonnet) -> {outPath}");
        _ = error;
        return 0;
    }

    private static int CollectCommand(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var values = Parse(args, new HashSet<string>());
        var manifest = ReadJson<Manifest>(Required(values, "--manifest"));
        var topics = values.TryGetValue("--topics", out var map)
            ? ReadJson<Dictionary<string, string>>(map[^1])
            : [];
        var extras = values.TryGetValue("--extra", out var extra)
            ? JsonNode.Parse(File.ReadAllText(extra[^1], Encoding.UTF8))!.AsArray().Select(n => n!).ToList()
            : [];
        var harvested = values.TryGetValue("--harvested", out var stamp)
            ? stamp[^1]
            : DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        var (entries, report) = Collect.Run(manifest, Required(values, "--outputs"), topics, extras, harvested);
        var outPath = Required(values, "--out");
        File.WriteAllText(outPath, entries.ToJsonString(s_json) + "\n", new UTF8Encoding(false));

        output.WriteLine($"collected {report.Kept} entries from {report.Parsed} proposed ({report.Duplicates} duplicates, {report.Dropped.Count} dropped) -> {outPath}");
        foreach (var (topic, count) in report.Topics)
        {
            output.WriteLine($"  {count,5}  {topic}");
        }

        foreach (var line in report.Dropped)
        {
            error.WriteLine($"dropped: {line}");
        }

        return 0;
    }

    private static int MergeCommand(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        var values = Parse(args, new HashSet<string> { "--dry-run", "--force" });
        var entries = values.TryGetValue("positional", out var positional) ? positional[0] : throw new ArgumentException("entries.json is required");
        if (!File.Exists(entries))
        {
            error.WriteLine($"merge: no such file: {entries}");
            return 2;
        }

        return Merge.Run(entries, Required(values, "--corpus"), values.ContainsKey("--dry-run"), values.ContainsKey("--force"), output, error);
    }
}
