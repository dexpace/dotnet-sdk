// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Housekeeping;

/// <summary>The outcome of one <c>git</c> invocation.</summary>
internal readonly record struct GitResult(int ExitCode, string Output, string Error)
{
    /// <summary><c>true</c> when git exited 0.</summary>
    public bool Success => ExitCode == 0;
}

/// <summary>
/// The one way this tool talks to git: an argument vector, never a shell string, so a filename that
/// looks like a flag or carries a space is still one argument.
/// </summary>
internal static class Git
{
    /// <summary>Runs <c>git -c core.quotePath=false &lt;args&gt;</c> in <paramref name="workingDirectory"/>.</summary>
    /// <remarks>
    /// <c>core.quotePath=false</c> because <c>git ls-files</c> C-quotes any path with a non-ASCII byte
    /// by default (<c>"docs/caf\303\251.md"</c>), and a quoted path fed back to a file read is a
    /// <see cref="FileNotFoundException"/> that takes the whole run down instead of a finding.
    /// </remarks>
    public static GitResult Run(
        string workingDirectory,
        IEnumerable<string> args,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("core.quotePath=false");
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                info.Environment[key] = value;
            }
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("could not start git");
        // Both pipes are drained concurrently: a child that fills stderr while this reads stdout
        // to the end would otherwise deadlock.
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return new GitResult(process.ExitCode, output, error.GetAwaiter().GetResult());
    }

    /// <summary>
    /// The top of the git work tree containing this tool, so it can be run from anywhere; the
    /// current directory when that fails.
    /// </summary>
    public static string RepositoryRoot()
    {
        var result = Run(AppContext.BaseDirectory, ["rev-parse", "--show-toplevel"]);
        return result.Success ? result.Output.Trim() : Directory.GetCurrentDirectory();
    }
}

/// <summary>One thing a check found. <see cref="Line"/> is 1 for a finding about a file as a whole.</summary>
internal sealed record Finding(string Check, string Severity, string Path, int Line, string Message)
{
    /// <inheritdoc/>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Path}:{Line}: [{Severity}] {Message}");
}

/// <summary>
/// Everything a check is allowed to do to a repository: read it and list it.
/// </summary>
/// <remarks>
/// The root is a constructor argument rather than a constant so a fixture tree can be probed; that
/// is the only reason this indirection exists.
/// </remarks>
internal sealed class Repo
{
    /// <summary>Opens the tree at <paramref name="root"/>, which must exist.</summary>
    public Repo(string root)
    {
        Root = Path.GetFullPath(root);
        if (!Directory.Exists(Root))
        {
            throw new ArgumentException($"no such directory: {Root}", nameof(root));
        }
    }

    /// <summary>The absolute root of the tree.</summary>
    public string Root { get; }

    /// <summary>A repository-relative path made absolute.</summary>
    public string Abs(string rel) => Path.Combine(Root, rel);

    /// <summary>Whether a file or directory exists at <paramref name="rel"/>.</summary>
    public bool Exists(string rel) => File.Exists(Abs(rel)) || Directory.Exists(Abs(rel));

    /// <summary>Whether <paramref name="rel"/> is a directory.</summary>
    public bool IsDirectory(string rel) => Directory.Exists(Abs(rel));

    /// <summary>The UTF-8 text of <paramref name="rel"/>.</summary>
    public string Read(string rel) => File.ReadAllText(Abs(rel), Encoding.UTF8);

    /// <summary>Immediate child names of a directory, sorted ordinally; empty when it is absent.</summary>
    public IReadOnlyList<string> Children(string rel)
    {
        if (!IsDirectory(rel))
        {
            return [];
        }

        return [.. Directory.EnumerateFileSystemEntries(Abs(rel)).Select(Path.GetFileName).OfType<string>()
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>Tracked paths matching <paramref name="pathspecs"/>.</summary>
    /// <remarks>
    /// <c>ls-files</c> lists the INDEX, so a file deleted in the working tree and not yet staged is
    /// still listed; every caller goes on to read what it gets back, so the result is filtered to
    /// what is actually on disk.
    /// </remarks>
    public IReadOnlyList<string> Tracked(params string[] pathspecs) => List(["ls-files", "--", .. pathspecs]);

    /// <summary>Tracked paths PLUS untracked, non-ignored ones.</summary>
    /// <remarks>
    /// The inbox's NORMAL state is a file a global skill has just written and nobody has staged, so
    /// a tracked-only sweep reports the empty tree this skill exists to notice.
    /// </remarks>
    public IReadOnlyList<string> Present(params string[] pathspecs) =>
        List(["ls-files", "--cached", "--others", "--exclude-standard", "--", .. pathspecs]);

    /// <summary>The standard output of a git command run in the tree; empty when it fails.</summary>
    public string GitOutput(params string[] args)
    {
        var result = Git.Run(Root, args);
        return result.Success ? result.Output : string.Empty;
    }

    private List<string> List(string[] args) =>
    [
        .. GitOutput(args).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Where(Exists),
    ];
}

/// <summary>Spelled-out numerals.</summary>
/// <remarks>
/// Count claims in prose are written as English words at least as often as digits — "three shipped
/// projects", "nine phase directories". A digits-only matcher protects roughly one sentence per
/// repository, which is the same as protecting none.
/// </remarks>
internal static class Numeral
{
    private static readonly string[] s_ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven",
        "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];

    private static readonly Dictionary<string, int> s_tens = new(StringComparer.Ordinal)
    {
        ["twenty"] = 20,
        ["thirty"] = 30,
        ["forty"] = 40,
        ["fifty"] = 50,
        ["sixty"] = 60,
        ["seventy"] = 70,
        ["eighty"] = 80,
        ["ninety"] = 90,
    };

    /// <summary>A digit run, or a word that might be a numeral; <see cref="Parse"/> decides which.</summary>
    public const string Token = "([0-9]+|[A-Za-z]+(?:-[A-Za-z]+)?)";

    /// <summary>The number <paramref name="token"/> denotes, or <c>null</c> when it is not a numeral.</summary>
    public static int? Parse(string token)
    {
        var word = token.ToLowerInvariant();
        if (word.Length > 0 && word.All(char.IsAsciiDigit))
        {
            return int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        }

        var ones = Array.IndexOf(s_ones, word);
        if (ones >= 0)
        {
            return ones;
        }

        if (s_tens.TryGetValue(word, out var tens))
        {
            return tens;
        }

        var dash = word.IndexOf('-', StringComparison.Ordinal);
        if (dash < 0 || !s_tens.TryGetValue(word[..dash], out var head))
        {
            return null;
        }

        var unit = Array.IndexOf(s_ones, word[(dash + 1)..]);
        return unit is > 0 and < 10 ? head + unit : null;
    }
}

/// <summary>Text helpers shared by the checks that read prose.</summary>
internal static partial class Prose
{
    /// <summary>
    /// <paramref name="text"/> split into lines, each KEEPING its terminator, so joining them gives
    /// the text back and a trailing newline does not add an empty last line.
    /// </summary>
    public static List<string> Lines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                lines.Add(text[start..]);
                break;
            }

            lines.Add(text[start..(end + 1)]);
            start = end + 1;
        }

        return lines;
    }

    /// <summary>The 1-based line number of character <paramref name="index"/>.</summary>
    public static int LineAt(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>
    /// <paramref name="text"/> with fenced code blocks blanked and double-quoted spans blanked,
    /// preserving every line break so a match's line number is still the file's line number.
    /// </summary>
    /// <remarks>
    /// Fenced code is not prose, and a double-quoted span is reported speech: a document that quotes
    /// the historical drift it fixed — <c>"two shipped projects" against three</c> — is describing a
    /// past claim, not making a present one. Matching inside either turns a document that explains
    /// its own history into a document that fails its own check.
    /// </remarks>
    public static string Asserted(string text) =>
        QuotedSpan().Replace(FencedBlanked(text), span => new string(' ', span.Length));

    /// <summary>
    /// <paramref name="text"/> with all the code a document can carry blanked — fenced blocks, inline
    /// code spans and 4-space/tab-indented blocks — so a link that appears only as an EXAMPLE is not
    /// read as an actual link. Quoted prose is left alone; line count is preserved throughout.
    /// </summary>
    /// <remarks>The <c>citations</c> check deliberately does NOT use this; see <see cref="Citations"/>.</remarks>
    public static string Unfenced(string text) => StripInlineCode(StripIndented(FencedBlanked(text)));

    /// <summary><paramref name="text"/> with <c>```</c> fences blanked, one line per line.</summary>
    public static string FencedBlanked(string text)
    {
        var inFence = false;
        var builder = new StringBuilder(text.Length);
        foreach (var line in Lines(text))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                builder.Append('\n');
            }
            else
            {
                builder.Append(inFence ? "\n" : line);
            }
        }

        return builder.ToString();
    }

    /// <summary>Every 4-space/tab-indented line blanked whole, so the line count survives.</summary>
    public static string StripIndented(string text) =>
        string.Concat(Lines(text).Select(line => IndentedLine().IsMatch(line) ? "\n" : line));

    /// <summary>Every inline code span replaced by same-length blanks.</summary>
    public static string StripInlineCode(string text) =>
        InlineCode().Replace(text, span => new string(' ', span.Length));

    [GeneratedRegex("\"[^\"\n]*\"")]
    private static partial Regex QuotedSpan();

    [GeneratedRegex(@"\A(?: {4,}|\t)")]
    private static partial Regex IndentedLine();

    [GeneratedRegex("`[^`\n]+`")]
    private static partial Regex InlineCode();
}
