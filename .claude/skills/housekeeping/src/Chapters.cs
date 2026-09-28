// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;

namespace Housekeeping;

/// <summary>
/// 9. A requirement ID a document attributes to a <c>docs/product-spec/NN-….md</c> chapter that does
/// not carry it.
/// </summary>
/// <remarks>
/// The unit is a CLAUSE, not a line: "A.md for X, Y; B.md for Z" pairs every ID with every chapter
/// under a same-line rule, which is why a naive rule fires several times per real defect. So a line is
/// split at <c>;</c>, each ID is associated with the NEAREST PRECEDING chapter reference in its clause
/// — or, for an ID run followed by "appears in", with the chapter that phrase introduces — a range is
/// expanded whether or not its endpoints are backticked, and a line whose two-line window carries a
/// negation (prose whose own subject is that the ID is NOT in that chapter) is skipped. Appendix C
/// carries every ID and can never be wrong about one, so it is exempt as a target.
///
/// Two stated blind spots, printed in <see cref="Gaps"/> so a reader is never told the check saw
/// something it did not: <c>continued_clause</c> (a chapter reference on the PRECEDING line is
/// invisible to a line-oriented scanner) and <c>dynamic_chapter_path</c> (a chapter named by an
/// ellipsis such as <c>13-…md</c>, or by a variable, resolves to no file and is skipped).
/// </remarks>
internal sealed partial class Chapters : Check
{
    /// <summary>The requirement-ID prefixes of <c>docs/product-spec/</c> (appendix C's nineteen).</summary>
    public static readonly string[] Prefixes =
    [
        "SEAM", "HTTP", "IO", "BODY", "CTX", "PIPE", "RECOV", "RETRY", "REDIR", "AUTH", "PAGE", "SSE", "SERDE",
        "OBS", "CFG", "TRANSPORT", "ASYNC", "XCUT", "NFR",
    ];

    /// <summary>The check's stated blind spots.</summary>
    public static readonly string[] Gaps = ["continued_clause", "dynamic_chapter_path"];

    /// <summary>
    /// Documents that quote wrong attributions ON PURPOSE, as the record of a defect this check found:
    /// repository-relative path prefixes. Empty — the Ruby port's entry here was its own phase-10
    /// record, which this repository does not have. A future record of that kind is one entry.
    /// </summary>
    public static readonly string[] DefaultExemptDocuments = [];

    private const string ExemptTarget = "appendix-c-consolidated-normative-requirement-index.md";
    private const char Boundary = ';';

    private static readonly string s_alternation = string.Join('|', Prefixes);

    private static readonly Regex s_id = new($@"\b({s_alternation})-([0-9]+)\b");

    /// <summary>
    /// <c>`SEAM-11`–`SEAM-15`</c>, <c>SEAM-11–SEAM-15</c>, <c>SEAM-11–15</c>: backticks tolerated on
    /// either end, which is what hides a backticked range from a pattern written for bare text.
    /// </summary>
    private static readonly Regex s_range = new($@"`?\b({s_alternation})-([0-9]+)`?\s*[–—]\s*`?(?:\1-)?([0-9]+)\b`?");

    /// <summary>
    /// Prose whose subject is that the ID is NOT in the named chapter, over the line and the next one,
    /// because such a sentence routinely crosses a line break. A union of escaped plain phrases, never
    /// an IgnorePatternWhitespace pattern: under that option the spaces inside a phrase are stripped
    /// and "appears nowhere" silently becomes "appearsnowhere".
    /// </summary>
    private static readonly Regex s_negation = new(
        string.Join('|', new[]
        {
            "appear nowhere", "appears nowhere", "harvested nowhere", "appear in no", "appears in no",
            "appendix C is their only", "appendix C is its only", "appendix C alone",
            "does not carry", "do not carry", "carries neither", "carries none", "carries no ",
            "carry none", "no prose chapter", "appendix-C row", "unfollowable", "from appendix C",
            "read out of appendix C", "read out of **appendix C", "neither ID appears", "is not in",
            "are not in", "not stated in", "only normative statement", "only prose home",
            "not appear in", "does not appear", "do not appear", "never appear", "n't appear",
        }.Select(Regex.Escape)),
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] s_scanned = ["docs"];

    private readonly string[] _exemptDocuments;

    /// <summary>The exempt-document list is a constructor argument so a test can exercise it.</summary>
    public Chapters(IReadOnlyList<string>? exemptDocuments = null) =>
        _exemptDocuments = [.. exemptDocuments ?? DefaultExemptDocuments];

    private enum Kind
    {
        Chapter,
        Forward,
        Id,
    }

    /// <inheritdoc/>
    public override string Name => "chapters";

    /// <summary>
    /// The <c>(chapter, id)</c> pairs a single line asserts, in clause order. Public so a test can read
    /// the clause scoping directly.
    /// </summary>
    public static IReadOnlyList<(string Chapter, string Id)> Pairs(string line) =>
        [.. line.Split(Boundary).SelectMany(ClausePairs)];

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo)
    {
        var carried = ChapterIds(repo);
        var findings = new List<Finding>();
        foreach (var path in Documents(repo))
        {
            findings.AddRange(Scan(path, Prose.Lines(repo.Read(path)), carried));
        }

        return findings;
    }

    /// <summary>
    /// A run of consecutive IDs binds to the nearest preceding chapter, unless the run is followed by
    /// FORWARD, when it binds to the chapter immediately after the verb (or to nothing, when the
    /// chapter is on the next line: the continued-clause gap).
    /// </summary>
    private static List<(string Chapter, string Id)> ClausePairs(string clause)
    {
        var list = Tokens(clause);
        string? chapter = null;
        var pairs = new List<(string, string)>();
        var index = 0;
        while (index < list.Count)
        {
            var (kind, value) = list[index];
            if (kind == Kind.Chapter)
            {
                chapter = value;
                index++;
            }
            else if (kind == Kind.Forward)
            {
                index++;
            }
            else
            {
                var runEnd = index;
                while (runEnd < list.Count && list[runEnd].Kind == Kind.Id)
                {
                    runEnd++;
                }

                var target = RunTarget(list, runEnd, chapter);
                if (target is not null)
                {
                    for (var i = index; i < runEnd; i++)
                    {
                        pairs.Add((target, list[i].Value!));
                    }
                }

                index = runEnd;
            }
        }

        return pairs;
    }

    private static string? RunTarget(List<(Kind Kind, string? Value)> list, int after, string? chapter)
    {
        if (after >= list.Count || list[after].Kind != Kind.Forward)
        {
            return chapter;
        }

        return after + 1 < list.Count && list[after + 1].Kind == Kind.Chapter ? list[after + 1].Value : null;
    }

    /// <summary>Chapters and IDs in textual order, every range expanded in place.</summary>
    private static List<(Kind Kind, string? Value)> Tokens(string clause)
    {
        var found = new List<(int Position, Kind Kind, string? Value)>();
        foreach (Match match in ChapterReference().Matches(clause))
        {
            found.Add((match.Index, Kind.Chapter, match.Groups[1].Value));
        }

        foreach (Match match in Forward().Matches(clause))
        {
            found.Add((match.Index, Kind.Forward, null));
        }

        var covered = new List<(int Start, int End)>();
        foreach (Match match in s_range.Matches(clause))
        {
            covered.Add((match.Index, match.Index + match.Length));
            var from = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var to = int.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
            for (var n = from; n <= to; n++)
            {
                found.Add((match.Index, Kind.Id, $"{match.Groups[1].Value}-{n}"));
            }
        }

        foreach (Match match in s_id.Matches(clause))
        {
            if (covered.Any(span => match.Index >= span.Start && match.Index < span.End))
            {
                continue;
            }

            found.Add((match.Index, Kind.Id, $"{match.Groups[1].Value}-{match.Groups[2].Value}"));
        }

        // OrderBy is stable, so equal positions keep insertion order: a range's IDs stay ascending.
        return [.. found.OrderBy(token => token.Position).Select(token => (token.Kind, token.Value))];
    }

    /// <summary>
    /// Every spec chapter's IDs, keyed by file name. Read from tracked AND untracked files (the Ruby
    /// original reads tracked only): a spec that has landed but not been committed is still the spec.
    /// </summary>
    private static Dictionary<string, HashSet<string>> ChapterIds(Repo repo) =>
        repo.Present("docs/product-spec/*.md").ToDictionary(
            path => Path.GetFileName(path),
            path => s_id.Matches(repo.Read(path)).Select(m => $"{m.Groups[1].Value}-{m.Groups[2].Value}")
                .ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

    private IEnumerable<string> Documents(Repo repo) =>
        repo.Present(s_scanned)
            .Where(path => path.EndsWith(".md", StringComparison.Ordinal))
            .Where(path => !Skipped().IsMatch(path))
            .Where(path => !_exemptDocuments.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)));

    private List<Finding> Scan(string path, List<string> lines, Dictionary<string, HashSet<string>> carried)
    {
        var findings = new List<Finding>();
        for (var index = 0; index < lines.Count; index++)
        {
            var window = lines[index] + (index + 1 < lines.Count ? lines[index + 1] : string.Empty);
            if (s_negation.IsMatch(window))
            {
                continue;
            }

            foreach (var (chapter, id) in Pairs(lines[index]))
            {
                if (chapter == ExemptTarget || !carried.TryGetValue(chapter, out var ids) || ids.Contains(id))
                {
                    continue;
                }

                findings.Add(Act(
                    path,
                    index + 1,
                    $"attributes {id} to docs/product-spec/{chapter}, which does not carry it " +
                    $"(gaps: {string.Join(", ", Gaps)})"));
            }
        }

        return findings;
    }

    [GeneratedRegex(@"docs/product-spec/([0-9]{2}-[a-z0-9-]+\.md)")]
    private static partial Regex ChapterReference();

    /// <summary>
    /// "X appears in &lt;chapter&gt;": the one verb that puts the chapter AFTER the IDs it is about. An
    /// ID run followed by it binds FORWARD, to the chapter the verb introduces.
    /// </summary>
    [GeneratedRegex(@"\bappears?\s+in\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Forward();

    [GeneratedRegex(@"\Adocs/(?:product-spec|knowledge/harvested)/")]
    private static partial Regex Skipped();
}
