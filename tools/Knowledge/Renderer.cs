// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Tools.Knowledge;

/// <summary>Everything that turns entries into text.</summary>
internal sealed partial class Renderer
{
    private readonly Corpus _corpus;
    private readonly AppendixC _appendix;
    private readonly KnowledgePaths _paths;
    private HashSet<string>? _specProseIds;

    public Renderer(Corpus corpus, AppendixC appendix, KnowledgePaths paths)
    {
        _corpus = corpus;
        _appendix = appendix;
        _paths = paths;
    }

    public string Entries(List<Entry> results, bool brief, Query query)
    {
        var output = new List<string>();
        foreach (var entry in results)
        {
            output.Add($"{entry.Location} ({entry.Section}){(entry.Key.Length > 0 ? " " + entry.Key : "")}{TagsOf(entry)}");
            output.Add($"- {entry.Text}");
            if (!brief && entry.SubLineText is not null)
            {
                output.Add($"  {entry.SubLineText}");
            }

            output.Add("");
        }

        var files = results.Select(entry => entry.Location.Split(':')[0]).Distinct().Count();
        var notes = results.Count(entry => entry.IsNote);
        var summary = $"{Text.Plural(results.Count, "entry", "entries")} across {Text.Plural(files, "topic file")}";
        if (notes > 0)
        {
            summary += $", {notes} of them notes — a note states what the implementation found and overrides " +
                       "the harvested entry where it names one";
        }

        output.Add(summary);

        var coverage = PrefixCoverage(results, query);
        if (coverage is not null)
        {
            output.Add("");
            output.Add(coverage);
        }

        // The silent wrong answer this tool can give: a --req that "hits" but whose every hit merely
        // names the ID in a conformance-checklist sentence. Exit 0 makes it look answered, so say so.
        if (results.All(entry => entry.IsRollup) && query.Reqs.Count > 0)
        {
            output.Add("");
            output.Add($"WARNING: every result is an appendix-B conformance roll-up — it names " +
                       $"{string.Join(", ", query.Reqs)} without stating the requirement. The corpus has no " +
                       "substantive entry. Read the canonical text in appendix C and the owning " +
                       "docs/product-spec/NN chapter instead (--prefix-info names it).");
        }

        return string.Join('\n', output);
    }

    public string ListTopics()
    {
        var stats = new SortedDictionary<string, (int Entries, int Notes, HashSet<string> Ids)>(StringComparer.Ordinal);
        foreach (var entry in _corpus.Entries)
        {
            (int Entries, int Notes, HashSet<string> Ids) row = stats.TryGetValue(entry.Topic, out var existing)
                ? existing
                : (0, 0, new HashSet<string>(StringComparer.Ordinal));
            row = entry.IsNote ? row with { Notes = row.Notes + 1 } : row with { Entries = row.Entries + 1 };
            row.Ids.UnionWith(entry.Reqs);
            stats[entry.Topic] = row;
        }

        var output = new List<string> { "topic\tentries\tdistinct IDs\tnotes" };
        output.AddRange(stats.Select(pair => $"{pair.Key}\t{pair.Value.Entries}\t{pair.Value.Ids.Count}\t{pair.Value.Notes}"));

        // Count only harvested topics as styleguide-derived: a note-only topic has no requirement ID
        // either, and calling it styleguide-derived is wrong.
        var idless = stats.Values.Count(row => row.Ids.Count == 0 && row.Entries > 0);
        var noted = stats.Values.Count(row => row.Notes > 0);
        var harvested = stats.Values.Count(row => row.Entries > 0);
        output.Add("");
        output.Add($"{stats.Count} topics, {harvested} of them harvested. {idless} harvested topics carry no " +
                   "requirement ID at all — those are styleguide-derived and are only reachable topic-first. " +
                   $"{noted} topics carry a hand-written note (`--origin note`), which states what the " +
                   "implementation found and overrides the harvested entry.");
        return string.Join('\n', output);
    }

    public string ListReqs()
    {
        var index = _corpus.CitationIndex();
        var output = Ids.Sort(index.Keys)
            .Select(id => $"{id}\t{string.Join(' ', index[id].Select(entry => entry.Location))}")
            .ToList();
        output.Add("");
        output.Add($"{index.Count} requirement IDs cited across the corpus");
        return string.Join('\n', output);
    }

    public string Coverage()
    {
        var index = _corpus.CitationIndex();
        var rows = new SortedDictionary<string, (int Total, List<string> Missing, List<string> Rollup)>(StringComparer.Ordinal);
        var rollupTotal = 0;
        foreach (var id in _appendix.AllIds)
        {
            var prefix = Ids.PrefixOf(id);
            if (!rows.TryGetValue(prefix, out var row))
            {
                row = (0, [], []);
            }

            row.Total++;
            if (!index.TryGetValue(id, out var hits))
            {
                row.Missing.Add(id);
            }
            else if (hits.All(entry => entry.IsRollup))
            {
                // Cited, but only by a conformance-checklist sentence that names it.
                row.Rollup.Add(id);
                rollupTotal++;
            }

            rows[prefix] = row;
        }

        var output = new List<string>
        {
            "requirement-ID coverage of docs/knowledge/",
            "",
            "prefix\tsubstantive\troll-up only\tuncited\ttotal\tuncited IDs",
        };
        foreach (var (prefix, row) in rows)
        {
            var substantive = row.Total - row.Missing.Count - row.Rollup.Count;
            var listing = row.Missing.Count == 0 ? "-" : string.Join(' ', Ids.Sort(row.Missing));
            output.Add($"{prefix}\t{substantive}\t{row.Rollup.Count}\t{row.Missing.Count}\t{row.Total}\t{listing}");
        }

        var uncited = _appendix.AllIds.Count(id => !index.ContainsKey(id));
        output.Add("");
        output.Add($"{_appendix.Count - uncited - rollupTotal}/{_appendix.Count} canonical IDs have a substantive " +
                   $"entry. {rollupTotal} more are named only by an appendix-B conformance roll-up (cited, but no " +
                   $"content). {uncited} are cited nowhere.");
        return string.Join('\n', output);
    }

    /// <summary>
    /// What the corpus does NOT know about a prefix. The question a roadmap phase asks before it plans
    /// anything: an ID with no substantive entry has to be read out of the specification itself.
    /// </summary>
    public string Gaps(IEnumerable<string> selectors)
    {
        // Every argument is a comma list, and the empty pieces of `HTTP, SEAM` (a trailing comma, then a
        // space) or `HTTP,,SEAM` are noise, not an unknown prefix. `all` anywhere means every prefix, which
        // contains whatever else was named; the others are still checked, so a typo does not hide behind it.
        var names = selectors.SelectMany(selector => selector.Split(',')).Select(Text.Strip).Where(name => name.Length > 0).ToList();
        var named = names.Where(name => !IsAll(name)).Select(ValidatePrefix).Distinct(StringComparer.Ordinal).ToList();
        List<string> prefixes = names.Any(IsAll) ? [.. _appendix.Prefixes.Order(StringComparer.Ordinal)] : named;
        if (prefixes.Count == 0)
        {
            throw new UsageException("--gaps was given no prefix; pass one like --gaps HTTP, or --gaps all");
        }

        var index = _corpus.CitationIndex();
        var output = new List<string> { "knowledge gaps: canonical IDs with no substantive corpus entry", "" };
        int totalIds = 0, totalRollup = 0, totalUncited = 0;
        foreach (var prefix in prefixes)
        {
            var ids = _appendix.IdsFor(prefix);
            var rollup = ids.Where(id => index.TryGetValue(id, out var hits) && hits.All(e => e.IsRollup)).ToList();
            var uncited = ids.Where(id => !index.ContainsKey(id)).ToList();
            totalIds += ids.Count;
            totalRollup += rollup.Count;
            totalUncited += uncited.Count;
            var substantive = ids.Count - rollup.Count - uncited.Count;

            output.Add($"{prefix} — {_appendix.SubsystemFor(prefix)}");
            output.Add($"  {ids.Count} canonical IDs: {substantive} substantive, {rollup.Count} roll-up only, {uncited.Count} uncited");
            if (rollup.Count > 0)
            {
                output.Add("  roll-up only (named by an appendix-B conformance sentence, which states nothing):");
                output.Add($"    {string.Join(' ', rollup)}");
            }

            if (uncited.Count > 0)
            {
                output.Add("  uncited (no entry in either tree names them):");
                output.Add($"    {string.Join(' ', uncited)}");
            }

            output.AddRange(GapPointer(prefix, [.. rollup, .. uncited]));
            output.Add("");
        }

        output.Add($"{totalRollup + totalUncited} of {totalIds} IDs in {Text.Plural(prefixes.Count, "prefix", "prefixes")} " +
                   $"have no substantive entry ({totalRollup} roll-up only, {totalUncited} uncited). Those are the " +
                   "ones a phase has to read out of the specification rather than out of the corpus.");
        return string.Join('\n', output);
    }

    // Where to read the IDs this prefix cannot answer from the corpus. Appendix C's subsystem cell names
    // the owning chapter without asserting the chapter states the ID — appendix C is a superset of the
    // prose chapters — so check the chapters instead of assuming them, and say which source carries each.
    private List<string> GapPointer(string prefix, List<string> ids)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var chapter = _appendix.ChapterFor(prefix);
        var prose = SpecProseIds();
        var inProse = ids.Where(prose.Contains).ToList();
        var appendixOnly = ids.Where(id => !prose.Contains(id)).ToList();
        var lines = new List<string>();
        if (chapter is not null && inProse.Count > 0)
        {
            var listing = appendixOnly.Count == 0 ? "" : $": {string.Join(' ', inProse)}";
            lines.Add($"  read these out of docs/product-spec/{chapter}{listing}");
        }

        if (appendixOnly.Count == 0)
        {
            return lines;
        }

        var only = inProse.Count == 0 ? "" : $": {string.Join(' ', appendixOnly)}";
        lines.Add($"  appendix C is their only normative statement — no docs/product-spec/ chapter states them{only}");
        lines.Add($"    grep -n '^| {appendixOnly[0]} ' {_paths.Relative(_paths.AppendixC)}");
        return lines;
    }

    // Every canonical ID any specification chapter states in its prose, appendix C excluded — it is the
    // index, not a statement. Whole-token matching, because HTTP-7 and HTTP-70 are different.
    private HashSet<string> SpecProseIds()
    {
        if (_specProseIds is null)
        {
            _specProseIds = new HashSet<string>(StringComparer.Ordinal);
            if (Directory.Exists(_paths.ProductSpecDir))
            {
                foreach (var path in Directory.EnumerateFiles(_paths.ProductSpecDir, "*.md").Order(StringComparer.Ordinal))
                {
                    if (path != _paths.AppendixC)
                    {
                        _specProseIds.UnionWith(Ids.Extract(File.ReadAllText(path), _appendix.Prefixes));
                    }
                }
            }
        }

        return _specProseIds;
    }

    /// <summary>
    /// Replaces the hand-maintained prefix → chapter routing table: subsystem, owning chapter and counts,
    /// all derived from appendix C at runtime.
    /// </summary>
    public string PrefixInfo(string selector)
    {
        var prefix = ValidatePrefix(selector);
        var ids = _appendix.IdsFor(prefix);
        var levels = string.Join(", ", _appendix.LevelsFor(prefix).Select(pair => $"{pair.Value} {pair.Key}"));
        var chapter = _appendix.ChapterFor(prefix);
        var index = _corpus.CitationIndex();
        var cited = ids.Count(index.ContainsKey);
        var substantive = ids.Count(id => index.TryGetValue(id, out var hits) && hits.Any(e => !e.IsRollup));
        var topics = TopicsForPrefix(prefix);

        var output = new List<string>
        {
            $"{prefix} — {_appendix.SubsystemFor(prefix)}",
            $"  {ids.Count} canonical IDs ({ids[0]}..{ids[^1]}), {levels}",
            $"  owning chapter: {(chapter is not null ? $"docs/product-spec/{chapter}" : "not resolvable from the subsystem cell — grep docs/product-spec/")}",
            $"  canonical text:  grep -n '^| {ids[0]} ' {_paths.Relative(_paths.AppendixC)}",
            $"  corpus: {substantive} of {ids.Count} IDs have a substantive entry, {cited - substantive} are roll-up only, {ids.Count - cited} are uncited",
        };
        if (topics.Count > 0)
        {
            output.Add($"  topics carrying {prefix} knowledge: {string.Join(' ', topics)}");
        }

        output.Add($"  the whole family:  --prefix {prefix} --section rules --brief");
        return string.Join('\n', output);
    }

    public static string PhaseHeader(string phase, List<(string Relative, List<string> Reqs)> documents)
    {
        var output = new List<string> { $"phase {phase}: {Text.Plural(documents.Count, "document")}", "" };
        foreach (var (relative, reqs) in documents)
        {
            var listing = reqs.Count == 0 ? "no requirement ID" : $"{reqs.Count}: {Ids.Compress(reqs)}";
            output.Add($"{relative} — {listing}");
        }

        var ids = documents.SelectMany(document => document.Reqs).Distinct(StringComparer.Ordinal).Count();
        output.Add("");
        output.Add($"{ids} distinct requirement IDs cited by phase {phase}; querying the corpus for all of them.");
        output.Add("");
        output.Add("");
        return string.Join('\n', output);
    }

    public string NoPhaseDocs(string phase) =>
        $"no phase documents found for phase {phase}. Looked under {_paths.Relative(_paths.WorkDir)}/*/phase{phase}/. " +
        "A phase gets its documents when it is planned, so nothing here means the phase has not been written " +
        "yet — there is no requirement-ID set to query. Use --prefix-info and --gaps to scope it from " +
        "appendix C instead.";

    public string ValidatePrefix(string selector)
    {
        var prefix = Text.Strip(selector).ToUpperInvariant();
        return _appendix.Prefixes.Contains(prefix)
            ? prefix
            : throw new UsageException(
                $"'{prefix}' is not a requirement-ID prefix in appendix C. The prefixes are " +
                string.Join(' ', _appendix.Prefixes.Order(StringComparer.Ordinal)));
    }

    private static bool IsAll(string name) => string.Equals(name, "all", StringComparison.OrdinalIgnoreCase);

    private static string TagsOf(Entry entry)
    {
        // An override first, because it changes whether the entry is still true; then a citation in
        // support, which does not.
        var tags = entry.OverriddenBy.Select(at => $" [overridden by {at}]")
            .Concat(entry.CitedBy.Select(at => $" [cited by {at}]"));
        return string.Concat(tags) + SettledTag(entry) + (entry.IsRollup ? " [appendix-B roll-up]" : "");
    }

    // A Conflicts entry's source line ends in its status and date: `unresolved`, `kept` (the port keeps the
    // departure) or `conformed` (the port changed to match). Only the settled two are tagged; an untagged
    // conflict is still open.
    private static string SettledTag(Entry entry)
    {
        if (entry.Section != "Conflicts" || entry.IsNote || entry.Confidence is null)
        {
            return string.Empty;
        }

        var status = entry.Confidence.Split(' ')[0];
        return status is "kept" or "conformed" ? $" [{status}]" : string.Empty;
    }

    private List<string> TopicsForPrefix(string prefix)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in _corpus.Entries)
        {
            foreach (var id in entry.Reqs.Where(id => Ids.PrefixOf(id) == prefix))
            {
                counts[entry.Topic] = counts.GetValueOrDefault(entry.Topic) + 1;
            }
        }

        return [.. counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(3).Select(pair => pair.Key)];
    }
}
