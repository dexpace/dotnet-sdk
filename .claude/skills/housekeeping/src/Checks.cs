// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.RegularExpressions;

namespace Housekeeping;

// The nine read-only checks. Each is a class with a `Name` and `Run(repo)`, and each derives a
// repository fact ONCE, from the repository, then compares every document that states it against
// that one derivation. Nothing here reads a number out of one document and compares it to another.
// The ninth, `chapters`, lives in Chapters.cs: one concern per file, as Guard.cs.

/// <summary>Base class: a name and a finding factory.</summary>
internal abstract class Check
{
    /// <summary>The name <c>--only</c> selects this check by.</summary>
    public abstract string Name { get; }

    /// <summary>Everything this check finds in <paramref name="repo"/>. Never writes.</summary>
    public abstract IReadOnlyList<Finding> Run(Repo repo);

    /// <summary>Drift to fix.</summary>
    protected Finding Act(string path, int line, string message) => new(Name, "act", path, line, message);

    /// <summary>A judgement call.</summary>
    protected Finding Note(string path, int line, string message) => new(Name, "note", path, line, message);
}

/// <summary>1. <c>docs/superpowers/{specs,plans}/</c> is an inbox. Anything in it is unfiled.</summary>
internal sealed class Inbox : Check
{
    /// <summary>The two inbox directories.</summary>
    public static readonly string[] Directories = ["docs/superpowers/specs", "docs/superpowers/plans"];

    /// <summary>A directory's placeholder and its explainer are furniture, not documents.</summary>
    public static readonly string[] Furniture = ["README.md", ".gitkeep"];

    /// <inheritdoc/>
    public override string Name => "inbox";

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo) =>
    [
        .. repo.Present(Directories)
            .Where(file => !Furniture.Contains(Path.GetFileName(file)))
            .Select(file => Act(
                file,
                1,
                "is still in the inbox. It belongs under docs/work/<delivery>/phaseN[/phaseNx]/ -- see " +
                "'Phase file conventions' in .claude/skills/housekeeping/SKILL.md, then run the apply stage.")),
    ];
}

/// <summary>2. Markdown at the repository root that belongs under <c>docs/</c>.</summary>
internal sealed class Root : Check
{
    /// <summary>The whole allowed list: the index files, the NuGet-conventional changelog, and the community-health files.</summary>
    public static readonly string[] Allowed =
    [
        "README.md", "CLAUDE.md", "CHANGELOG.md", "CONTRIBUTING.md", "CODE_OF_CONDUCT.md", "SECURITY.md", "LICENSE.md",
    ];

    /// <inheritdoc/>
    public override string Name => "root";

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo) =>
    [
        // A pathspec glob crosses `/`, so `*.md` matches at any depth; the root is what this
        // check is about, so files with a directory component are filtered out.
        .. repo.Present("*.md")
            .Where(file => !file.Contains('/', StringComparison.Ordinal) && !Allowed.Contains(file))
            .Select(file => Act(
                file,
                1,
                "sits at the repository root. A register belongs in docs/, a phase record under docs/work/. " +
                "The root carries README.md, CLAUDE.md, CHANGELOG.md and the community-health files only.")),
    ];
}

/// <summary>3. Counts stated in the three index documents, against the repository.</summary>
/// <remarks>
/// The table in <see cref="Table"/> is the whole check. A new claim is one row: the file that states
/// it, the pattern whose first capture is the number, a label, and a function that derives the real
/// value from the repository. A file that does not exist, or does not carry the pattern at all, is a
/// no-op — this reports a WRONG count, not a missing sentence.
/// </remarks>
internal sealed partial class Claims : Check
{
    /// <summary>Topic-directory furniture, not topics.</summary>
    private static readonly string[] NonTopicFiles = ["INDEX.md", "SOURCES.md", "README.md"];

    /// <inheritdoc/>
    public override string Name => "claims";

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo) => [.. Table(repo).SelectMany(claim => Evaluate(repo, claim))];

    /// <summary>
    /// The number of shipped projects: directories under <c>src/</c> carrying a <c>.csproj</c> that
    /// does not opt out of packing. Every such project is a NuGet package. Absent <c>src/</c> is 0.
    /// </summary>
    public static int ProjectCount(Repo repo) => ShippedProjects(repo).Count();

    /// <summary>The <c>(directory, csproj)</c> pairs under <c>src/</c> that ship as a NuGet package.</summary>
    public static IEnumerable<(string Directory, string Csproj)> ShippedProjects(Repo repo) =>
        repo.Children("src")
            .Where(dir => repo.IsDirectory($"src/{dir}"))
            .Select(dir => (dir, csproj: Csproj.Find(repo, $"src/{dir}")))
            .Where(pair => pair.csproj is not null && Csproj.IsPackable(repo.Read(pair.csproj)))
            .Select(pair => (pair.dir, pair.csproj!));

    /// <summary>
    /// <c>phaseN</c> directories directly under a delivery. Sub-phases nest INSIDE one of these and
    /// are deliberately not counted twice.
    /// </summary>
    public static int PhaseCount(Repo repo) =>
        repo.Children("docs/work").Sum(delivery => repo.Children($"docs/work/{delivery}")
            .Count(entry => PhaseDirectory().IsMatch(entry) && repo.IsDirectory($"docs/work/{delivery}/{entry}")));

    /// <summary>Topic files in the harvested corpus.</summary>
    public static int TopicCount(Repo repo) =>
        repo.Children("docs/knowledge/harvested")
            .Count(file => file.EndsWith(".md", StringComparison.Ordinal) && !NonTopicFiles.Contains(file));

    private static Claim[] Table(Repo repo)
    {
        Func<int> projects = () => ProjectCount(repo);
        Func<int> phases = () => PhaseCount(repo);
        Func<int> topics = () => TopicCount(repo);
        const string ProjectsTail = @"(?:\*\*)?(?:(?:shipped|published|library|NuGet)\s+)?(?:projects|packages)\b";
        const string PhasesTail = @"phase\s+director(?:y|ies)\b";
        const string TopicsTail = @"harvested\s+topics?\b";
        const string ProjectsLabel = "shipped projects (NuGet packages) under src/";
        const string PhasesLabel = "phase directories under docs/work/";
        const string TopicsLabel = "topics under docs/knowledge/harvested/";
        return
        [
            new("CLAUDE.md", Numbered(ProjectsTail), ProjectsLabel, projects),
            new("README.md", Numbered(ProjectsTail), ProjectsLabel, projects),
            new("docs/README.md", Numbered(ProjectsTail), ProjectsLabel, projects),
            new("CLAUDE.md", Numbered(PhasesTail), PhasesLabel, phases),
            new("docs/README.md", Numbered(PhasesTail), PhasesLabel, phases),
            new("CLAUDE.md", Numbered(TopicsTail), TopicsLabel, topics),
            new("docs/README.md", Numbered(TopicsTail), TopicsLabel, topics),
        ];
    }

    /// <summary><c>&lt;numeral&gt; &lt;tail&gt;</c>, case-insensitive, first capture holding the numeral.</summary>
    private static Regex Numbered(string tail) =>
        new($@"{Numeral.Token}\s+{tail}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private List<Finding> Evaluate(Repo repo, Claim claim)
    {
        if (!repo.Exists(claim.File))
        {
            return [];
        }

        var prose = Prose.Asserted(repo.Read(claim.File));
        var actual = claim.Actual();
        var findings = new List<Finding>();
        foreach (Match match in claim.Pattern.Matches(prose))
        {
            var stated = Numeral.Parse(match.Groups[1].Value);
            if (stated is null || stated == actual)
            {
                continue; // an adjective, not a numeral -- or a true count
            }

            findings.Add(Act(
                claim.File,
                Prose.LineAt(prose, match.Index),
                $"states \"{match.Value.Trim()}\" but the repository has {actual} {claim.Label}."));
        }

        return findings;
    }

    [GeneratedRegex(@"\Aphase[0-9]+\z")]
    private static partial Regex PhaseDirectory();

    private sealed record Claim(string File, Regex Pattern, string Label, Func<int> Actual);
}

/// <summary>What the checks need to know about a <c>.csproj</c>, read as text.</summary>
internal static partial class Csproj
{
    /// <summary>The first <c>*.csproj</c> (ordinal order) directly in <paramref name="dir"/>, repository-relative.</summary>
    public static string? Find(Repo repo, string dir) =>
        repo.Children(dir).FirstOrDefault(name => name.EndsWith(".csproj", StringComparison.Ordinal)) is { } name
            ? $"{dir}/{name}"
            : null;

    /// <summary><c>false</c> only when the project says <c>&lt;IsPackable&gt;false&lt;/IsPackable&gt;</c>.</summary>
    public static bool IsPackable(string text) => !NotPackable().IsMatch(text);

    /// <summary>The declared <c>&lt;PackageId&gt;</c>, or <c>null</c>.</summary>
    public static string? PackageId(string text) => PackageIdElement().Match(text) is { Success: true } m ? m.Groups[1].Value : null;

    /// <summary>Whether the project wires a README into its package with <c>&lt;PackageReadmeFile&gt;</c>.</summary>
    public static bool HasPackageReadme(string text) => PackageReadmeFileElement().IsMatch(text);

    [GeneratedRegex(@"<IsPackable>\s*false\s*</IsPackable>", RegexOptions.IgnoreCase)]
    private static partial Regex NotPackable();

    [GeneratedRegex(@"<PackageId>\s*([^<\s]+)\s*</PackageId>")]
    private static partial Regex PackageIdElement();

    [GeneratedRegex(@"<PackageReadmeFile>\s*[^<\s]+\s*</PackageReadmeFile>")]
    private static partial Regex PackageReadmeFileElement();
}

/// <summary>4. A README on every shipped project, naming the package its <c>.csproj</c> declares.</summary>
internal sealed partial class Readmes : Check
{
    /// <summary>
    /// The bar is zero to one working call in about 30 seconds without reading source. Lines rather
    /// than bytes, because a README's first working example is a fenced block and a line count is
    /// what a writer can see.
    /// </summary>
    public const int MinLines = 20;

    /// <inheritdoc/>
    public override string Name => "readmes";

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo)
    {
        if (!repo.IsDirectory("src"))
        {
            return [];
        }

        var findings = new List<Finding>();
        foreach (var dir in repo.Children("src").Where(dir => repo.IsDirectory($"src/{dir}")))
        {
            findings.AddRange(CheckProject(repo, dir));
        }

        return findings;
    }

    private List<Finding> CheckProject(Repo repo, string dir)
    {
        var csproj = Csproj.Find(repo, $"src/{dir}");
        if (csproj is null)
        {
            return [Act($"src/{dir}", 1, "has no .csproj. Every directory under src/ is a shipped project.")];
        }

        var project = repo.Read(csproj);
        if (!Csproj.IsPackable(project))
        {
            return []; // not a NuGet package, so not a package README
        }

        var name = Csproj.PackageId(project) ?? Path.GetFileNameWithoutExtension(csproj);
        var readme = $"src/{dir}/README.md";
        if (!repo.Exists(readme))
        {
            return [Act(readme, 1, $"is missing. Every shipped project carries a NuGet package README ({name}).")];
        }

        var text = repo.Read(readme);
        var findings = new List<Finding>();
        var lineCount = Prose.Lines(text).Count;
        if (lineCount < MinLines)
        {
            findings.Add(Note(
                readme,
                1,
                $"is {lineCount} lines. The bar is {MinLines}: zero to one working call in about 30 seconds, " +
                "without reading source."));
        }

        var heading = FirstHeading().Match(text);
        if (!heading.Success)
        {
            findings.Add(Act(readme, 1, "has no top-level `# ` heading."));
        }
        else if (!heading.Groups[1].Value.Contains(name, StringComparison.Ordinal))
        {
            findings.Add(Act(
                readme,
                Prose.LineAt(text, heading.Index),
                $"opens with \"{heading.Groups[1].Value.Trim()}\" but its .csproj declares {name}."));
        }

        if (!Csproj.HasPackageReadme(project))
        {
            findings.Add(Note(
                csproj,
                1,
                "sets no <PackageReadmeFile>, so README.md never reaches the package page on nuget.org."));
        }

        return findings;
    }

    [GeneratedRegex(@"^#\s+(.+?)\r?$", RegexOptions.Multiline)]
    private static partial Regex FirstHeading();
}

/// <summary>5. Broken relative links, and backticked chapters of a normative tree that do not resolve.</summary>
internal sealed partial class Links : Check
{
    /// <inheritdoc/>
    public override string Name => "links";

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo) => [.. Files(repo).SelectMany(file => CheckFile(repo, file))];

    /// <summary>The repository-relative path <paramref name="raw"/> points at, or <c>null</c> when it is not a path.</summary>
    public static string? Resolve(string file, string raw)
    {
        if (External().IsMatch(raw))
        {
            return null;
        }

        var hash = raw.IndexOf('#', StringComparison.Ordinal);
        var target = hash < 0 ? raw : raw[..hash];
        if (target.Length == 0)
        {
            return null;
        }

        var directory = Path.GetDirectoryName(file)?.Replace('\\', '/') ?? string.Empty;
        return CleanRelative($"{directory}/{Uri.UnescapeDataString(target)}");
    }

    /// <summary>Lexical normalization of a relative path: <c>.</c> dropped, <c>..</c> folded where it can be.</summary>
    public static string CleanRelative(string path)
    {
        var parts = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == ".." && parts.Count > 0 && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
            }
            else
            {
                parts.Add(segment);
            }
        }

        return parts.Count == 0 ? "." : string.Join('/', parts);
    }

    private static IEnumerable<string> Files(Repo repo) =>
        repo.Present("docs")
            .Concat(repo.Present("*.md").Where(file => !file.Contains('/', StringComparison.Ordinal)))
            .Concat(repo.Present("src/*/README.md"))
            .Distinct(StringComparer.Ordinal)
            .Where(file => file.EndsWith(".md", StringComparison.Ordinal));

    private List<Finding> CheckFile(Repo repo, string file)
    {
        var raw = repo.Read(file);
        var text = Prose.Unfenced(raw);
        var definitions = ReferenceDefinitions(text);
        var findings = new List<Finding>();
        var number = 0;
        foreach (var line in Prose.Lines(text))
        {
            number++;
            findings.AddRange(InlineFindings(repo, file, line, number));
            findings.AddRange(ReferenceFindings(repo, file, line, number, definitions));
        }

        findings.AddRange(SpanFindings(repo, file, raw));
        return findings;
    }

    /// <summary>
    /// A claim of the form "X is stated in <c>docs/sdk-design-dotnet/NN-….md</c>". Markdown link syntax
    /// is not how this repository writes a chapter reference — backticks are — so
    /// <see cref="Prose.Unfenced"/>, which blanks inline code, resolves none of them. This pass reads
    /// the spans instead, over <see cref="Prose.FencedBlanked"/>: a fence is still an example.
    /// </summary>
    private List<Finding> SpanFindings(Repo repo, string file, string raw)
    {
        var findings = new List<Finding>();
        var number = 0;
        foreach (var line in Prose.Lines(Prose.FencedBlanked(raw)))
        {
            number++;
            foreach (Match span in Span().Matches(line))
            {
                var value = span.Groups[1].Value;
                var hash = value.IndexOf('#', StringComparison.Ordinal);
                var target = (hash < 0 ? value : value[..hash]).Trim();
                if (Placeholder().IsMatch(target) || !NormativePath().IsMatch(target) || repo.Exists(target))
                {
                    continue;
                }

                findings.Add(Act(
                    file,
                    number,
                    $"names `{target}`, a path that does not exist. A chapter of a normative tree is a claim about " +
                    "the repository; check the filename against docs/product-spec/ or docs/sdk-design-dotnet/."));
            }
        }

        return findings;
    }

    private IEnumerable<Finding> InlineFindings(Repo repo, string file, string line, int number)
    {
        foreach (Match link in Link().Matches(line))
        {
            var raw = link.Groups[1].Value;
            var resolved = Resolve(file, raw);
            if (resolved is not null && !repo.Exists(resolved))
            {
                yield return Act(file, number, $"links {raw}, which resolves to {resolved} -- a path that does not exist.");
            }
        }
    }

    private IEnumerable<Finding> ReferenceFindings(
        Repo repo, string file, string line, int number, Dictionary<string, string> definitions)
    {
        foreach (Match reference in Reference().Matches(line))
        {
            var rawRef = reference.Groups[2].Value;
            var key = (rawRef.Length == 0 ? reference.Groups[1].Value : rawRef).Trim().ToLowerInvariant();
            if (!definitions.TryGetValue(key, out var target))
            {
                yield return Act(file, number, $"references [{key}], which has no [{key}]: definition in this file.");
                continue;
            }

            var resolved = Resolve(file, target);
            if (resolved is not null && !repo.Exists(resolved))
            {
                yield return Act(file, number, $"references [{key}], which resolves to {resolved} -- a path that does not exist.");
            }
        }
    }

    /// <summary>
    /// Every <c>[ref]: target</c> definition in the file, keyed the way Markdown resolves a reference
    /// — case-insensitively, trimmed.
    /// </summary>
    private static Dictionary<string, string> ReferenceDefinitions(string text)
    {
        var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Prose.Lines(text))
        {
            var match = Definition().Match(line);
            if (match.Success)
            {
                definitions[match.Groups[1].Value.Trim().ToLowerInvariant()] = match.Groups[2].Value;
            }
        }

        return definitions;
    }

    [GeneratedRegex(@"\[[^\]]*\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex Link();

    /// <summary><c>[text][ref]</c>, or the collapsed <c>[text][]</c> that reuses <c>text</c> as the ref.</summary>
    [GeneratedRegex(@"\[([^\]]+)\]\[([^\]]*)\]")]
    private static partial Regex Reference();

    /// <summary><c>[ref]: target</c>, optionally titled — a reference-style link's definition.</summary>
    [GeneratedRegex(@"^\[([^\]]+)\]:\s*(\S+)(?:\s+""[^""]*"")?\s*$")]
    private static partial Regex Definition();

    /// <summary>Anything the filesystem cannot answer for.</summary>
    [GeneratedRegex(@"\A(?:https?:|mailto:|#)")]
    private static partial Regex External();

    /// <summary>An inline code span, read for the paths this repository writes in prose rather than in link syntax.</summary>
    [GeneratedRegex("`([^`\n]+)`")]
    private static partial Regex Span();

    /// <summary>
    /// A chapter of one of the two NORMATIVE trees. Scoped to those two on purpose: a chapter that
    /// does not resolve is wrong rather than not-yet-written — while a <c>src/…</c> path in a plan
    /// names something a later phase creates, and claiming it does not exist would be true and useless.
    /// </summary>
    [GeneratedRegex(@"\Adocs/(?:product-spec|sdk-design-dotnet)/\S+\.md\z")]
    private static partial Regex NormativePath();

    /// <summary>A pattern standing for a family of chapters, or a path elided to fit a line.</summary>
    [GeneratedRegex(@"[*…]|\bNN\b")]
    private static partial Regex Placeholder();
}

/// <summary>6. An aggregate register living inside a specification, design or plan document.</summary>
/// <remarks>
/// This repository keeps no find-list register: a finding is routed to its owner when it is found —
/// the numbered plan task whose scope it falls in, or the roadmap — or it is simply fixed. So the
/// message names the owners rather than a file.
/// </remarks>
internal sealed partial class Registers : Check
{
    private static readonly string[] Trees =
    [
        "docs/work", "docs/superpowers", "docs/sdk-documentation", "docs/product-spec", "docs/sdk-design-dotnet",
    ];

    /// <inheritdoc/>
    public override string Name => "registers";

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo) =>
    [
        .. repo.Present(Trees)
            .Where(file => file.EndsWith(".md", StringComparison.Ordinal))
            .SelectMany(file => CheckFile(repo, file)),
    ];

    private IEnumerable<Finding> CheckFile(Repo repo, string file)
    {
        var text = repo.Read(file);
        foreach (var heading in (Regex[])[OpenFindings(), DeferredItems(), OpenItems()])
        {
            var match = heading.Match(text);
            if (!match.Success)
            {
                continue;
            }

            var window = text.Substring(match.Index, Math.Min(600, text.Length - match.Index));
            if (Stub().IsMatch(window))
            {
                continue;
            }

            yield return Act(
                file,
                Prose.LineAt(text, match.Index),
                $"carries \"{match.Value.Trim()}\". No find-list register exists: an aggregate belongs with its " +
                "owner -- the numbered plan task whose scope it falls in, or the roadmap entry that owns it. " +
                "A phase's own dated section stays with the phase; the aggregate does not.");
        }
    }

    [GeneratedRegex(@"^##\s+Open Findings\b.*$", RegexOptions.Multiline)]
    private static partial Regex OpenFindings();

    [GeneratedRegex(@"^##\s+Deferred Items\b.*$", RegexOptions.Multiline)]
    private static partial Regex DeferredItems();

    [GeneratedRegex(@"^##\s+Open Items\s*$", RegexOptions.Multiline)]
    private static partial Regex OpenItems();

    /// <summary>A pointer stub is a paragraph saying where the register went, not a register.</summary>
    [GeneratedRegex(@"^\*\*Moved out on ", RegexOptions.Multiline)]
    private static partial Regex Stub();
}

/// <summary>
/// 7. No ID from a RETIRED register is cited anywhere, and every ID from a LIVE one resolves to an
/// item in ITS OWN register.
/// </summary>
/// <remarks>
/// This repository has never had a register, so both tables are empty and the check reports nothing
/// today. The mechanism is kept whole, because a register is one entry away: adding a prefix is one
/// entry in <see cref="DefaultRegisters"/>; retiring one is moving it to <see cref="DefaultRetired"/>
/// with the sentence that says where its items went.
///
/// A live prefix resolves only against its own file, so a heading or table row with the right shape
/// sitting in any other file resolves nothing, and a live prefix whose register file does not exist
/// is a no-op. A retired prefix deliberately does NOT no-op when its file is missing: that is the one
/// behaviour that would hide the leftovers the check exists to find; the file itself, if it lingers,
/// is one finding rather than one per row.
///
/// The ID namespace is a declared list of prefixes rather than <c>[A-Z]+-\d+</c>, which would swallow
/// every requirement ID in the spec (<c>HTTP-7</c>, <c>SEAM-1</c>, <c>NFR-5</c>) and report the entire
/// corpus as dangling.
/// </remarks>
internal sealed partial class Citations : Check
{
    /// <summary>The live registers: prefix → the file whose own rows define it. Empty.</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultRegisters = new Dictionary<string, string>();

    /// <summary>Retired prefixes: prefix → (the sentence saying where its items went, the file it lived in). Empty.</summary>
    public static readonly IReadOnlyDictionary<string, RetiredRegister> DefaultRetired = new Dictionary<string, RetiredRegister>();

    private static readonly string[] Trees = ["docs", "*.md", "src", "tests", ".claude"];

    private readonly IReadOnlyDictionary<string, string> _registers;
    private readonly IReadOnlyDictionary<string, RetiredRegister> _retired;
    private readonly Regex? _citation;

    /// <summary>
    /// Both tables are constructor arguments, so the mechanism can be exercised while they are empty;
    /// the probe builds every check with the defaults, and that is the only reason this exists.
    /// </summary>
    public Citations(
        IReadOnlyDictionary<string, string>? registers = null,
        IReadOnlyDictionary<string, RetiredRegister>? retired = null)
    {
        _registers = registers ?? DefaultRegisters;
        _retired = retired ?? DefaultRetired;
        var prefixes = _registers.Keys.Concat(_retired.Keys).Select(Regex.Escape).ToList();
        _citation = prefixes.Count == 0 ? null : new Regex($@"\b((?:{string.Join('|', prefixes)})-[0-9]+)\b");
    }

    /// <inheritdoc/>
    public override string Name => "citations";

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo)
    {
        if (_citation is null)
        {
            return []; // no register, live or retired: nothing to cite
        }

        var active = _registers.Where(pair => repo.Exists(pair.Value)).ToDictionary(StringComparer.Ordinal);
        var known = KnownIds(repo, active);
        return [.. Lingering(repo), .. CitedFiles(repo).SelectMany(file => CheckFile(repo, file, known, active))];
    }

    /// <summary>A retired register's file still on disk: one finding for the file, not one per row.</summary>
    private IEnumerable<Finding> Lingering(Repo repo) =>
        _retired.Where(pair => repo.Exists(pair.Value.File)).Select(pair => Act(
            pair.Value.File,
            1,
            $"still exists, but it was {pair.Value.Reason}. Delete it once no {pair.Key}-<n> citation remains."));

    /// <summary>
    /// <c>### XX-&lt;n&gt;</c>, or a table row whose first cell is the ID, backticked or not: a register
    /// can be a list of sections or a table of rows without this check caring which.
    /// </summary>
    private static Regex DefinitionPattern(string prefix) =>
        new($@"^(?:#{{2,6}}\s+|\|\s*)`?({Regex.Escape(prefix)}-[0-9]+)`?\b", RegexOptions.Multiline);

    /// <summary>Every defined ID, scanned from its OWN register file only.</summary>
    private static HashSet<string> KnownIds(Repo repo, Dictionary<string, string> active)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (prefix, file) in active)
        {
            foreach (Match match in DefinitionPattern(prefix).Matches(repo.Read(file)))
            {
                ids.Add(match.Groups[1].Value);
            }
        }

        return ids;
    }

    private IEnumerable<string> CitedFiles(Repo repo)
    {
        var registerFiles = _registers.Values.Concat(_retired.Values.Select(r => r.File)).ToHashSet(StringComparer.Ordinal);
        return repo.Present(Trees)
            .Where(file => Readable().IsMatch(file) && !Excluded().IsMatch(file) && !registerFiles.Contains(file));
    }

    /// <summary>
    /// <see cref="Prose.FencedBlanked"/> rather than <see cref="Prose.Unfenced"/>, which is what the
    /// <c>links</c> check reads: <c>Unfenced</c> also blanks inline code spans and every
    /// 4-space-indented line, and both are prose where register IDs are concerned — the ID convention
    /// backticks every ID, and a 4-space indent is nearly always list-item continuation rather than a
    /// code block. A fence is still an example, and is still blanked.
    /// </summary>
    private IEnumerable<Finding> CheckFile(Repo repo, string file, HashSet<string> known, Dictionary<string, string> active)
    {
        var number = 0;
        foreach (var line in Prose.Lines(Prose.FencedBlanked(repo.Read(file))))
        {
            number++;
            foreach (Match match in _citation!.Matches(line))
            {
                var message = FindingFor(match.Groups[1].Value, known, active);
                if (message is not null)
                {
                    yield return Act(file, number, message);
                }
            }
        }
    }

    private string? FindingFor(string id, HashSet<string> known, Dictionary<string, string> active)
    {
        var prefix = id[..id.IndexOf('-', StringComparison.Ordinal)];
        if (_retired.TryGetValue(prefix, out var retired))
        {
            return $"cites {id}, an ID from {retired.Reason}.";
        }

        if (!active.TryGetValue(prefix, out var register) || known.Contains(id))
        {
            return null; // that prefix's register does not exist yet, or the ID resolves
        }

        return $"cites {id}, which has no entry in {register}. Item IDs are permanent; a dangling one means " +
               "the citation, not the register, is wrong.";
    }

    [GeneratedRegex(@"\.(?:md|cs)\z")]
    private static partial Regex Readable();

    /// <summary>
    /// A skill's own tests invent a register and cite it. Those IDs are literals in a throwaway tree,
    /// not citations of this repository's register.
    /// </summary>
    [GeneratedRegex(@"\A\.claude/skills/[^/]+/tests?/")]
    private static partial Regex Excluded();
}

/// <summary>A retired register: where its items went, and the file it used to live in.</summary>
internal sealed record RetiredRegister(string Reason, string File);

/// <summary>
/// 8. The guard itself: the frozen list and the writable surface must not overlap, the guard must
/// still refuse what it must, and a frozen entry must not have become a symlink out of its own tree.
/// </summary>
internal sealed class GuardCheck : Check
{
    /// <summary>Every surface this skill, or a routine edit, is allowed to write.</summary>
    public static readonly string[] WritableSurface =
    [
        "docs/README.md", "docs/architecture.md", "docs/sdk-documentation", "docs/work", "docs/superpowers",
        "docs/assets", "CLAUDE.md", "README.md", "CHANGELOG.md",
    ];

    /// <summary>
    /// Paths the guard must refuse, whether or not they exist yet. A guard that has quietly stopped
    /// refusing is worse than no guard: the apply stage would still say it checked.
    /// </summary>
    public static readonly string[] MustRefuse =
    [
        "docs/product-spec/04.md", "docs/product-spec.md", "docs/knowledge/notes/x.md",
        "docs/knowledge/harvested/documentation.md", "docs/sdk-design-dotnet.md",
        "docs/sdk-design-dotnet/10-deliberate-deviations-from-the-reference-contract.md",
        "docs/styleguide/csharp/01-x.md",
    ];

    /// <inheritdoc/>
    public override string Name => "guard";

    /// <inheritdoc/>
    public override IReadOnlyList<Finding> Run(Repo repo) => [.. Overlaps(repo), .. Gaps(repo), .. Symlinks(repo)];

    private IEnumerable<Finding> Overlaps(Repo repo)
    {
        foreach (var path in WritableSurface)
        {
            if (Guard.FrozenEntryFor(path, repo.Root) is { } entry)
            {
                yield return Act(
                    path,
                    1,
                    $"is on the writable surface AND under the frozen entry '{entry}'. One of the two lists is " +
                    "wrong; until it is fixed the apply stage could eat a normative document.");
            }
        }
    }

    private IEnumerable<Finding> Gaps(Repo repo) =>
        MustRefuse.Where(path => !Guard.IsFrozen(path, repo.Root)).Select(path => Act(
            path,
            1,
            "is not refused by the guard. Run the skill's tests: dotnet test .claude/skills/housekeeping/tests"));

    private IEnumerable<Finding> Symlinks(Repo repo) =>
        Guard.FrozenSymlinks(repo.Root).Select(entry => Act(
            entry,
            1,
            "is a symlink. A frozen entry must be a real path, or the guard protects a name while the bytes " +
            "live somewhere writable."));
}
