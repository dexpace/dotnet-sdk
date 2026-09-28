// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Tools.Knowledge;

/// <summary>The diagnostic half of the renderer: coverage footers and zero-result explanations.</summary>
internal sealed partial class Renderer
{
    private const int ListedUncovered = 12;

    /// <summary>
    /// A zero-result query must never look like "the corpus has nothing to say" when it is really a typo,
    /// the wrong topic name, or two filters that cannot both hold. Each dimension is first tested alone,
    /// and only a dimension that matches nothing on its own gets to explain itself — an unconditional
    /// per-filter hint would state falsehoods ("PAGE-11 is canonical but no entry cites it yet" under
    /// <c>--prefix HTTP --req PAGE-11</c>).
    /// </summary>
    public string NoMatches(Query query)
    {
        var output = new List<string> { "no matching entries." };
        var used = query.UsedDimensions();
        var barren = used.Where(name => !_corpus.Entries.Any(query.Solo(name).Matches)).ToList();

        if (barren.Count == 0)
        {
            var alone = used.Select(name =>
                $"{name.ToString().ToLowerInvariant()} {_corpus.Entries.Count(query.Solo(name).Matches)}");
            output.Add("  every filter matches something on its own; no entry satisfies all of them at once " +
                       $"(matching alone: {string.Join(", ", alone)}). Filters AND together — drop one.");
            return string.Join('\n', output);
        }

        foreach (var name in barren)
        {
            output.AddRange(HintFor(name, query));
        }

        return string.Join('\n', output);
    }

    // `--prefix P --section rules` and `--prefix-info P` answer different questions and print two numbers
    // a reader naturally reads as one. An ID's section is a per-ID filing decision, so a narrowed prefix
    // query can silently cover fewer IDs than the prefix has. So a prefix query states its own coverage
    // and separates the two reasons an ID is missing: filed in a section this query filtered out, or
    // absent from the corpus entirely. Null when there is no prefix filter, or nothing is uncovered.
    private string? PrefixCoverage(List<Entry> results, Query query)
    {
        if (query.Prefixes.Count == 0)
        {
            return null;
        }

        var covered = results.SelectMany(entry => entry.Reqs).ToHashSet(StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var prefix in query.Prefixes)
        {
            var ids = _appendix.IdsFor(prefix);
            var uncovered = ids.Where(id => !covered.Contains(id)).ToList();
            if (uncovered.Count == 0)
            {
                continue;
            }

            lines.Add($"NOTE: these filters cover {ids.Count - uncovered.Count} of {prefix}'s " +
                      $"{Text.Plural(ids.Count, "canonical ID")}. Not covered:");
            lines.AddRange(uncovered.Take(ListedUncovered).Select(id => $"  {UncoveredLine(id)}"));
            if (uncovered.Count > ListedUncovered)
            {
                lines.Add($"  ... and {uncovered.Count - ListedUncovered} more");
            }

            lines.Add($"  --gaps {prefix} separates the roll-up-only from the uncited.");
        }

        return lines.Count == 0 ? null : string.Join('\n', lines);
    }

    // Why one canonical ID is not in this result: elsewhere in the corpus, or nowhere in it.
    private string UncoveredLine(string id)
    {
        var elsewhere = _corpus.CitationIndex().TryGetValue(id, out var hits)
            ? hits.Where(entry => !entry.IsRollup).ToList()
            : [];
        if (elsewhere.Count == 0)
        {
            return $"{id} — no substantive entry anywhere in the corpus";
        }

        var sections = string.Join(", ", elsewhere.Select(entry => entry.Section ?? "")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        return $"{id} — in the corpus under {sections}: read it with --req {id}";
    }

    private List<string> HintFor(Dimension dimension, Query query) => dimension switch
    {
        Dimension.Reqs => HintForReqs(query),
        Dimension.Keys => HintForKeys(query),
        Dimension.Prefixes => [.. query.Prefixes.Select(prefix => $"  no entry cites any {prefix} ID.")],
        Dimension.Origins => [.. query.Origins.Select(origin => origin == "note"
            ? "  the notes tree carries only what an implementation found that overrides a harvested rule; " +
              "it is meant to be small."
            : "  no harvested entry matches. harvested/ is the whole corpus bar the notes, so this is a filter " +
              "combination, not an empty tree.")],
        Dimension.Topics => [.. query.Topics.Select(topic =>
            $"  no topic file matches '{topic}'. available: {string.Join(' ', _corpus.Topics())}")],
        Dimension.Roles => [.. query.Roles.Select(role => role == "review"
            ? "  no review-role entry matches. review is the notes tree's role; under harvested/ it is a " +
              "structural violation, so there are none."
            : $"  no entry carries the role {role}.")],
        Dimension.Sections => [.. query.Sections.Select(section =>
            $"  the {section} section holds no entry in either tree" +
            (section == "Superseded" ? " — it exists only under notes/, where an override is recorded." : "."))],
        Dimension.Chapters => HintForChapters(query),
        _ => ["  text filters are applied to entry text only; try --grep with a looser pattern, or fewer bare words."],
    };

    private List<string> HintForReqs(Query query)
    {
        var index = _corpus.CitationIndex();
        var output = new List<string>();
        foreach (var id in query.Reqs)
        {
            var prefix = Ids.PrefixOf(id);
            var number = Ids.NumberOf(id);
            output.Add(_appendix.Contains(id)
                ? $"  {id} is canonical but no entry cites it yet."
                : $"  {id} is not a canonical requirement ID (not in appendix C).");

            // Never offer the queried ID back as its own nearest neighbour.
            var nearest = index.Keys
                .Where(other => other != id && Ids.PrefixOf(other) == prefix)
                .OrderBy(other => Math.Abs(Ids.NumberOf(other) - number))
                .ThenBy(Ids.NumberOf)
                .Take(5)
                .ToList();
            if (nearest.Count == 0)
            {
                var cited = index.Keys.Select(Ids.PrefixOf).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
                output.Add($"  no {prefix} ID is cited anywhere. cited prefixes: {string.Join(' ', cited)}");
            }
            else
            {
                output.Add($"  nearest cited {prefix} IDs: {string.Join(' ', nearest)}");
            }

            var topics = TopicsForPrefix(prefix);
            if (topics.Count > 0)
            {
                output.Add($"  topics carrying {prefix} knowledge: {string.Join(' ', topics)}");
            }
        }

        return output;
    }

    // A key that resolves to nothing is not a typo, it is news: the entry it named has been reworded, so
    // whatever cites it — a note, an audit — is describing a rule that no longer exists in that form.
    private List<string> HintForKeys(Query query) =>
        [.. query.Keys.Select(key =>
        {
            var topic = key.Split('/')[0];
            if (!_corpus.Entries.Any(entry => entry.Topic == topic))
            {
                return $"  no entry carries the key {key}, and no topic '{topic}' exists.";
            }

            var citedBy = _corpus.Entries
                .Where(entry => entry.Text.Contains($"`{key}`", StringComparison.Ordinal))
                .Select(entry => entry.Location)
                .ToList();
            var where = citedBy.Count == 0 ? "" : $" It is cited by {string.Join(' ', citedBy)}, which needs updating.";
            return $"  no entry carries the key {key}. A key digests the entry's text, so a reworded or " +
                   $"re-harvested rule gets a new one.{where}";
        })];

    private List<string> HintForChapters(Query query)
    {
        var known = _corpus.Entries.SelectMany(entry => entry.Chapters()).Distinct(StringComparer.Ordinal)
            .OrderBy(chapter => int.Parse(chapter, System.Globalization.CultureInfo.InvariantCulture));
        return [.. query.Chapters.Select(chapter =>
            $"  no entry cites styleguide chapter {chapter}. harvested chapters: {string.Join(' ', known)}")];
    }
}
