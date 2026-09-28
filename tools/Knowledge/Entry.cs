// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;

namespace Dexpace.Tools.Knowledge;

/// <summary>The vocabularies the corpus uses, and the names that are never topic files.</summary>
internal static class Vocabulary
{
    /// <summary>
    /// <c>INDEX.md</c> is a generated topic table, <c>SOURCES.md</c> a provenance manifest and
    /// <c>README.md</c> the two-tree contract; none holds entries, and all would parse as noise.
    /// </summary>
    public static readonly IReadOnlyList<string> NonTopicFiles = ["INDEX.md", "SOURCES.md", "README.md"];

    /// <summary>
    /// Appendix B is the conformance-test checklist. Its entries roll several requirement IDs into one
    /// "the suite verifies X, Y, Z" sentence, so they make an ID look cited while carrying none of its
    /// content. A large share of cited IDs resolve ONLY to a roll-up — a silent wrong answer unless
    /// called out.
    /// </summary>
    public const string RollupSource = "appendix-b-conformance-test-checklist";

    /// <summary>Provenance roles, most common first. <c>review</c> is the notes tree's role only.</summary>
    public static readonly IReadOnlyList<string> Roles = ["spec", "design", "styleguide", "review"];

    public static readonly IReadOnlyList<string> HarvestedRoles = ["spec", "design", "styleguide"];

    /// <summary>The two trees, and the value of an entry's <c>origin</c> field for each.</summary>
    public static readonly IReadOnlyList<string> Origins = ["harvested", "note"];

    /// <summary>
    /// The six section names the parser recognises, in emission order. <c>Superseded</c> exists only
    /// under <c>notes/</c>; the structure gate rejects one under <c>harvested/</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> Sections =
        ["Rules", "Constraints", "Conclusions", "Reference", "Conflicts", "Superseded"];
}

/// <summary>
/// One <c>&lt;sub&gt;</c> provenance line, parsed. Two shapes are in the corpus:
/// <c>role · `path:lines` · confidence · sha:xxxx</c> (the common one) and
/// <c>roleA `pathA` · roleB `pathB` · resolution-status</c> (Conflicts entries), so role and path are
/// sometimes separate <c> · </c> fields and sometimes one. Walk the fields and classify each rather than
/// reading them positionally.
/// </summary>
internal sealed partial record SubLine(List<string> Roles, List<string> Sources, string? Confidence, string? Sha)
{
    private const string FieldSeparator = " · ";

    [GeneratedRegex(@"\A(\S+)\s+`(.+)`\z", RegexOptions.ECMAScript)]
    private static partial Regex RoleAndSource();

    [GeneratedRegex(@"\A`(.+)`\z", RegexOptions.ECMAScript)]
    private static partial Regex BareSource();

    [GeneratedRegex(@"\A\S+\z", RegexOptions.ECMAScript)]
    private static partial Regex LoneWord();

    public static SubLine Parse(string inner)
    {
        var roles = new List<string>();
        var sources = new List<string>();
        string? confidence = null;
        string? sha = null;
        string? pendingRole = null;

        foreach (var raw in inner.Split(FieldSeparator))
        {
            var field = Text.Strip(raw);
            var pair = RoleAndSource().Match(field);
            if (pair.Success)
            {
                roles.Add(pair.Groups[1].Value);
                sources.Add(pair.Groups[2].Value);
                pendingRole = null;
                continue;
            }

            var bare = BareSource().Match(field);
            if (bare.Success)
            {
                roles.Add(pendingRole ?? "unknown");
                sources.Add(bare.Groups[1].Value);
                pendingRole = null;
                continue;
            }

            if (field.StartsWith("sha:", StringComparison.Ordinal))
            {
                sha = field["sha:".Length..];
                continue;
            }

            // A lone word that is not followed by a source is the confidence / status; anything with
            // spaces is the confidence / status outright.
            if (pendingRole is not null)
            {
                confidence = pendingRole;
            }

            pendingRole = LoneWord().IsMatch(field) ? field : null;
            if (pendingRole is null)
            {
                confidence = field;
            }
        }

        if (pendingRole is not null)
        {
            confidence = pendingRole;
        }

        return new SubLine(roles, sources, confidence, sha);
    }
}

/// <summary>One entry: a bullet, its provenance line, and everything derived from them.</summary>
internal sealed partial class Entry
{
    public Entry(string file, string topic, string origin, int line, string? section, string text)
    {
        File = file;
        Topic = topic;
        Origin = origin;
        Line = line;
        Section = section;
        Text = text;
    }

    public string File { get; }

    public string Topic { get; }

    public string Origin { get; }

    public int Line { get; }

    public string? Section { get; }

    public string Text { get; set; }

    public string Key { get; set; } = "";

    public List<string> Reqs { get; set; } = [];

    public List<string> Roles { get; } = [];

    public List<string> Sources { get; } = [];

    public string? Confidence { get; private set; }

    public string? Sha { get; private set; }

    public string? SubLineText { get; private set; }

    public List<string> OverriddenBy { get; } = [];

    public List<string> Overrides { get; } = [];

    public List<string> CitedBy { get; } = [];

    public List<string> Cites { get; } = [];

    public string? Role => Roles.Count > 0 ? Roles[0] : null;

    public string? Source => Sources.Count > 0 ? Sources[0] : null;

    public bool IsNote => Origin == "note";

    /// <summary>
    /// <c>pagination.md</c> exists in both trees, so a bare basename is ambiguous. A harvested entry
    /// prints as <c>pagination.md:134</c>, a note as <c>notes/pagination.md:5</c>.
    /// </summary>
    public string Location => $"{(IsNote ? "notes/" : "")}{File}:{Line}";

    /// <summary>
    /// True when every source this entry cites is the conformance checklist, i.e. the entry names
    /// requirement IDs without saying anything about them.
    /// </summary>
    public bool IsRollup =>
        Sources.Count > 0 && Sources.All(source => source.Contains(Vocabulary.RollupSource, StringComparison.Ordinal));

    // Styleguide `<sub>` paths carry a numbered chapter file
    // (`docs/styleguide/csharp/06-types-and-data-modeling.md:168-183`), so "styleguide 6.7" is answerable
    // by matching the chapter number. Only styleguide-role sources count: `docs/product-spec/04-….md` is a
    // numbered chapter too, and conflating the two would answer "styleguide 4" with spec chapter 4.
    [GeneratedRegex(@"/([0-9]{2})-[^/]*\.md(?::|\z)", RegexOptions.ECMAScript)]
    private static partial Regex StyleguideChapter();

    [GeneratedRegex(@":[0-9,\-]+\z", RegexOptions.ECMAScript)]
    public static partial Regex LineSuffix();

    public List<string> Chapters()
    {
        var found = new List<string>();
        for (var i = 0; i < Sources.Count; i++)
        {
            if (i >= Roles.Count || Roles[i] != "styleguide")
            {
                continue;
            }

            var match = StyleguideChapter().Match(Sources[i]);
            if (match.Success)
            {
                found.Add(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return found;
    }

    /// <summary>Source paths with any <c>:12-18</c> line suffix removed — what drift and the gate hash.</summary>
    public IEnumerable<string> SourcePaths() => Sources.Select(source => LineSuffix().Replace(source, "", 1));

    /// <summary>
    /// One bullet carrying two <c>&lt;sub&gt;</c> lines is malformed, but overwriting the first with the
    /// second drops its sources — and a source is what the structural gate checks, so the malformed half
    /// would escape the check. Accumulate instead, and let the gate see everything the entry cites.
    /// </summary>
    public void MergeSub(SubLine parsed, string rawLine)
    {
        Roles.AddRange(parsed.Roles);
        Sources.AddRange(parsed.Sources);
        if (SubLineText is null)
        {
            Confidence = parsed.Confidence;
            Sha = parsed.Sha;
            SubLineText = rawLine;
        }
        else
        {
            SubLineText = $"{SubLineText} {rawLine}";
        }
    }
}

/// <summary>String helpers with Ruby's whitespace set, so keys digest exactly what the Ruby port did.</summary>
internal static class Text
{
    private static readonly char[] s_rubyWhitespace = [' ', '\t', '\n', '\v', '\f', '\r', '\0'];

    public static string Strip(string value) => value.Trim(s_rubyWhitespace);

    public static string StripEnd(string value) => value.TrimEnd(s_rubyWhitespace);

    public static bool IsBlank(string value) => Strip(value).Length == 0;

    public static string Plural(int count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";
}
