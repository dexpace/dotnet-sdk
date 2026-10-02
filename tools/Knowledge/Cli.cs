// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Encodings.Web;
using System.Text.Json;

namespace Dexpace.Tools.Knowledge;

/// <summary>
/// The query command: argument parsing and dispatch. Takes its streams so the tests can drive it in
/// process and read what it wrote.
/// </summary>
internal sealed class Cli
{
    public static readonly string Usage = $"""
        Usage: scripts/knowledge [options] [words...]
               dotnet run --project tools/Knowledge -- [options] [words...]
               scripts/knowledge verify-structure [--root DIR]   (the blocking gate)
               scripts/knowledge drift [--root DIR]              (the hand-run report)

        Query docs/knowledge/ — both harvested/ (what the documents say) and notes/
        (what the implementation found; it overrides). Different filters AND together;
        values within one filter OR.

          --req <ID>          entries citing that requirement ID (repeatable, comma-ok).
                              Comma form is the whole-task query: --req HTTP-13,HTTP-14
          --key <topic/hex>   the one entry with that key — how a note's citation is
                              resolved. An unknown key is reported, not an error: it
                              means the entry was reworded and the citation is stale.
          --prefix <name>     a whole ID family: --prefix HTTP. The audit-scale filter.
          --origin <names>    {string.Join(" | ", Vocabulary.Origins)}
          --topic <names>     topic files whose name contains any of these (substring)
          --section <names>   {string.Join(" | ", Vocabulary.Sections.Select(s => s.ToLowerInvariant()))}
          --role <names>      {string.Join(" | ", Vocabulary.Roles)}
          --chapter <n>       styleguide chapter, e.g. 6 (a "6.7" drops the .7)
          --grep <regex>      case-insensitive regex over entry text (repeatable)
          <words...>          bare words: case-insensitive substrings, all must match
                              (under --gaps, wherever they stand, they are prefixes instead)
          --phase <N[x]>      every requirement ID cited by docs/work/*/phaseN[/phaseNx]/,
                              queried as one --req set, with the per-document breakdown.
                              A range (HTTP-1–HTTP-35, HTTP-1-HTTP-35, HTTP-1..HTTP-35, or the
                              short HTTP-1–35, each endpoint also in `code` or **bold**) is
                              credited with every appendix-C ID between its ends
          --brief             drop <sub> provenance lines (~30% less output)
          --json              machine-readable records
          --list-topics       every topic with entry, distinct-ID and note counts
          --list-reqs         requirement-ID -> location map (large; prefer --coverage)
          --coverage          substantive vs roll-up-only vs uncited, per prefix
          --gaps <NAMES|all>  the IDs with no substantive entry, roll-up-only and
                              uncited listed apart. Space- or comma-separated, and
                              repeatable: --gaps HTTP PAGE, --gaps HTTP,PAGE. `all`
                              anywhere in the list means every prefix. What a phase
                              must read out of the spec rather than the corpus.
          --prefix-info <P>   subsystem, owning chapter and ID count for one prefix, all
                              derived from appendix C — no routing table to go stale.
                              A second prefix, a repeat, or --gaps beside it exits 2.
          --no-drift-check    skip the stale-source warning (it hashes touched sources)
          --root <dir>        repository root (default: the checkout this tool was built
                              from; {KnowledgePaths.EnvRoot} overrides that)
          --help

        Every result carries a stable key, <topic>/<8 hex>, digested from the entry
        text. Name a rule by that key in a note: it survives a re-order, and it changes
        exactly when the rule's text does — including on a re-harvest that rewords it,
        which is when the note needs revisiting. Resolve one with --key. A harvested
        entry a note corrects prints [overridden by notes/...]; one a note only leans
        on prints [cited by notes/...]. The relation is the note's own verb before the
        key -- Supersedes, Resolves, Answers, Narrows, Corrects -- and every other key
        in the entry is a citation in support.

        An unknown --role, --section, --chapter, --origin, --prefix or --gaps exits 2:
        a typo there is a silent empty result. So does an argument a flag would
        otherwise drop: a second word after --prefix-info, --prefix-info given twice,
        and --prefix-info together with --gaps. An unknown --req only warns, because a
        not-yet-canonical ID is a legitimate thing to ask about.

        Exits 1 when a query matches nothing, and when the corpus has not been
        harvested in this checkout at all. A first word of `verify-structure` or
        `drift` selects that subcommand; to search for either word, use --grep.

        Examples:
          scripts/knowledge --req HTTP-13,HTTP-14,HTTP-15   # one task's whole ID set
          scripts/knowledge --origin note --brief           # start of a phase: what we found
          scripts/knowledge --prefix HTTP --section rules   # an audit group, by ID family
          scripts/knowledge --chapter 9 lock                # "styleguide 9.x"
          scripts/knowledge --prefix-info RETRY             # subsystem, chapter, counts
          scripts/knowledge --gaps RETRY RECOV              # what the corpus does NOT know
          scripts/knowledge --phase 5a --brief              # a past phase's whole ID set

        """;

    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly HashSet<string> s_valueFlags = new(StringComparer.Ordinal)
    {
        "--req", "--key", "--prefix", "--origin", "--topic", "--section", "--role", "--chapter", "--grep",
        "--phase", "--gaps", "--prefix-info", "--root",
    };

    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    public Cli(TextWriter stdout, TextWriter stderr)
    {
        _stdout = stdout;
        _stderr = stderr;
    }

    public int Run(IReadOnlyList<string> argv)
    {
        try
        {
            var (options, words) = Parse(argv);
            if (options.Help)
            {
                _stdout.Write(Usage);
                return 0;
            }

            return Dispatch(options, words);
        }
        catch (UsageException e)
        {
            _stderr.Write(e.Message + "\n");
            return 2;
        }
        catch (NotHarvestedException e)
        {
            _stderr.Write(e.Message + "\n");
            return 1;
        }
    }

    /// <summary>
    /// <c>--flag value</c> and <c>--flag=value</c>, options anywhere among the words, <c>--</c> ends
    /// option parsing. An unknown option or a missing value is a usage error (exit 2).
    /// </summary>
    public static (QueryOptions Options, List<string> Words) Parse(IReadOnlyList<string> argv)
    {
        var options = new QueryOptions();
        var words = new List<string>();
        for (var i = 0; i < argv.Count; i++)
        {
            var arg = argv[i];
            if (arg == "--")
            {
                words.AddRange(argv.Skip(i + 1));
                break;
            }

            if (!arg.StartsWith('-') || arg == "-")
            {
                words.Add(arg);
                continue;
            }

            var equals = arg.IndexOf('=', StringComparison.Ordinal);
            var flag = equals > 0 && arg.StartsWith("--", StringComparison.Ordinal) ? arg[..equals] : arg;
            string? inline = equals > 0 && flag != arg ? arg[(equals + 1)..] : null;
            if (s_valueFlags.Contains(flag))
            {
                var value = inline ?? (i + 1 < argv.Count ? argv[++i] : throw new UsageException($"missing argument: {flag}"));
                Assign(options, flag, value);
            }
            else if (inline is null && SetSwitch(options, flag))
            {
                continue;
            }
            else
            {
                throw new UsageException($"invalid option: {arg}");
            }
        }

        return (options, words);
    }

    private static void Assign(QueryOptions options, string flag, string value)
    {
        switch (flag)
        {
            case "--req": options.Req.Add(value); break;
            case "--key": options.Key.Add(value); break;
            case "--prefix": options.Prefix.Add(value); break;
            case "--origin": options.Origin.Add(value); break;
            case "--topic": options.Topic.Add(value); break;
            case "--section": options.Section.Add(value); break;
            case "--role": options.Role.Add(value); break;
            case "--chapter": options.Chapter.Add(value); break;
            case "--grep": options.Grep.Add(value); break;
            case "--phase": options.Phase = value; break;
            case "--gaps": options.Gaps.Add(value); break;
            case "--prefix-info": options.PrefixInfo = SetOnce(options.PrefixInfo, flag, value); break;
            default: options.Root = value; break;
        }
    }

    // A flag that takes one value cannot take two: the second would silently replace the first.
    private static string SetOnce(string? current, string flag, string value) =>
        current is null ? value : throw new UsageException($"{flag} was given more than once; it takes one value");

    private static bool SetSwitch(QueryOptions options, string flag)
    {
        switch (flag)
        {
            case "--brief": options.Brief = true; return true;
            case "--json": options.Json = true; return true;
            case "--list-topics": options.ListTopics = true; return true;
            case "--list-reqs": options.ListReqs = true; return true;
            case "--coverage": options.Coverage = true; return true;
            case "--no-drift-check": options.DriftCheck = false; return true;
            case "-h" or "--help": options.Help = true; return true;
            default: return false;
        }
    }

    private int Dispatch(QueryOptions options, List<string> words)
    {
        // --gaps and --prefix-info each answer from appendix C alone and never look at the query words or at
        // each other, so anything handed to the one that it does not read is a dropped argument unless it is
        // refused here: --gaps reads the words as further prefixes (`--gaps HTTP SEAM`, the roadmap's
        // `--gaps <PREFIXES>`), and --prefix-info, which describes exactly one, refuses them.
        if (options.PrefixInfo is not null && options.Gaps.Count > 0)
        {
            throw new UsageException(
                "--prefix-info and --gaps cannot be combined: each answers a question of its own. " +
                "Run them one at a time.");
        }

        if (options.PrefixInfo is not null && words.Count > 0)
        {
            throw new UsageException(
                $"--prefix-info takes one prefix, but was also given: {string.Join(' ', words)}. " +
                "Run it once per prefix.");
        }

        var paths = options.Root is not null ? new KnowledgePaths(options.Root) : KnowledgePaths.Default();
        var appendix = AppendixC.Load(paths);

        // --gaps and --prefix-info answer from appendix C alone, so they are the two commands worth
        // running before anything has been harvested.
        var tolerant = options.Gaps.Count > 0 || options.PrefixInfo is not null;
        var corpus = LoadCorpus(paths, appendix.Prefixes, tolerant);
        var renderer = new Renderer(corpus, appendix, paths);

        if (options.PrefixInfo is not null)
        {
            return Emit(renderer.PrefixInfo(options.PrefixInfo));
        }

        if (options.Gaps.Count > 0)
        {
            return Emit(renderer.Gaps([.. options.Gaps, .. words]));
        }

        if (options.ListTopics)
        {
            return Emit(renderer.ListTopics());
        }

        if (options.ListReqs)
        {
            return Emit(renderer.ListReqs());
        }

        return options.Coverage ? Emit(renderer.Coverage()) : QueryCommand(options, words, paths, appendix, corpus, renderer);
    }

    private int Emit(string text)
    {
        Puts(text);
        return 0;
    }

    // Ruby's puts: a newline is added only when the text does not already end in one.
    private void Puts(string text) => _stdout.Write(text.EndsWith('\n') ? text : text + "\n");

    private Corpus LoadCorpus(KnowledgePaths paths, IReadOnlySet<string> prefixes, bool tolerant)
    {
        try
        {
            return Corpus.Load(paths, prefixes);
        }
        catch (NotHarvestedException e) when (tolerant)
        {
            _stderr.Write($"note: {e.Message.Split('\n')[0].Trim()} — reporting against appendix C alone.\n");
            return new Corpus([]);
        }
    }

    private int QueryCommand(
        QueryOptions options, List<string> words, KnowledgePaths paths, AppendixC appendix, Corpus corpus, Renderer renderer)
    {
        var extraReqs = new List<string>();
        if (options.Phase is not null)
        {
            var documents = new PhaseDocs(paths, appendix).Find(options.Phase);
            var phase = PhaseDocs.Normalize(options.Phase);
            if (documents.Count == 0)
            {
                return Emit(renderer.NoPhaseDocs(phase));
            }

            Puts(Renderer.PhaseHeader(phase, documents));
            extraReqs = [.. documents.SelectMany(document => document.Reqs).Distinct(StringComparer.Ordinal)];
            if (extraReqs.Count == 0)
            {
                return 0;
            }
        }

        var query = Query.Build(options, words, appendix, extraReqs, _stderr);
        if (query.IsEmpty)
        {
            _stdout.Write(Usage);
            return 0;
        }

        var results = corpus.Entries.Where(query.Matches).ToList();
        if (options.DriftCheck && results.Count > 0)
        {
            foreach (var warning in DriftCheck.For(paths)?.WarningsFor(results) ?? [])
            {
                _stderr.Write(warning + "\n");
            }
        }

        if (options.Json)
        {
            _stdout.Write(JsonSerializer.Serialize(results.Select(EntryRecord.From), s_json) + "\n");
            return results.Count == 0 ? 1 : 0;
        }

        if (results.Count == 0)
        {
            _stdout.Write(renderer.NoMatches(query) + "\n");
            return 1;
        }

        return Emit(renderer.Entries(results, options.Brief, query));
    }
}

/// <summary>The <c>--json</c> record: snake_case fields, in the order the Ruby port emitted them.</summary>
internal sealed record EntryRecord(
    string File,
    string Topic,
    string Origin,
    string Key,
    int Line,
    string? Section,
    string Text,
    string? Role,
    IReadOnlyList<string> Roles,
    string? Source,
    IReadOnlyList<string> Sources,
    string? Confidence,
    string? Sha,
    string? SubLine,
    IReadOnlyList<string> Reqs,
    bool Rollup,
    IReadOnlyList<string> OverriddenBy,
    IReadOnlyList<string> Overrides,
    IReadOnlyList<string> CitedBy,
    IReadOnlyList<string> Cites)
{
    public static EntryRecord From(Entry e) => new(
        e.File, e.Topic, e.Origin, e.Key, e.Line, e.Section, e.Text, e.Role, e.Roles, e.Source, e.Sources,
        e.Confidence, e.Sha, e.SubLineText, e.Reqs, e.IsRollup, e.OverriddenBy, e.Overrides, e.CitedBy, e.Cites);
}
