// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Housekeeping;

/// <summary>One <c>git mv</c>, unperformed.</summary>
internal sealed record Move(string From, string To)
{
    /// <summary>
    /// The exact command. <c>--</c> before the paths, so a filename beginning with <c>-</c> is not
    /// read as a flag.
    /// </summary>
    public string Command => $"git mv -- {From} {To}";
}

/// <summary>
/// The only stage that writes, and it does exactly one mechanical thing: drain the
/// <c>docs/superpowers/{specs,plans}/</c> inbox into <c>docs/work/&lt;delivery&gt;/phaseN[/phaseNx]/</c>
/// with <c>git mv</c>, so <c>git log --follow</c> resolves each file across the move. Never rewrites
/// prose; never repoints a link.
/// </summary>
/// <remarks>
/// It shells out to <c>git mv</c> and never calls <see cref="File.Move(string, string)"/>: a plain
/// move is a delete plus an add in the index, and the history stops there.
/// </remarks>
internal sealed partial class Apply
{
    /// <summary>Plans an inbox drain in <paramref name="root"/>.</summary>
    /// <exception cref="ArgumentException">When <paramref name="phase"/> is not <c>5</c> or <c>5a</c>-shaped.</exception>
    public Apply(string root, string delivery = "mvp", string? phase = null, IReadOnlyDictionary<string, string>? renames = null)
    {
        Root = Path.GetFullPath(root);
        Delivery = delivery;
        if (phase is not null && !PhaseFlag().IsMatch(phase))
        {
            throw new ArgumentException($"--phase must look like 5 or 5a, got \"{phase}\"");
        }

        Phase = phase;
        Renames = renames ?? new Dictionary<string, string>();
    }

    /// <summary>The repository root.</summary>
    public string Root { get; }

    /// <summary>The unit of delivery under <c>docs/work/</c>.</summary>
    public string Delivery { get; }

    /// <summary>The <c>--phase</c> override, or <c>null</c> to read the phase from the filename.</summary>
    public string? Phase { get; }

    /// <summary>Renames applied as files move, keyed by inbox path or basename.</summary>
    public IReadOnlyDictionary<string, string> Renames { get; }

    /// <summary>The moves this run would perform.</summary>
    public IReadOnlyList<Move> Plan() =>
        [.. InboxFiles().Select(from => new Move(from, $"{TargetDirectory(from)}/{Renamed(from)}"))];

    /// <summary>Where a document belongs. <c>--phase</c> wins; otherwise the filename is read.</summary>
    /// <remarks>
    /// A file naming a whole phase with no sub-phase letter — a segmentation design, a shared
    /// checklist — sits at the <c>phaseN/</c> level. A file naming no phase at all sits directly
    /// under the delivery.
    /// </remarks>
    public string TargetDirectory(string from)
    {
        var name = FileName(from);
        if (Phase is not null && PhaseFlag().Match(Phase) is { Success: true } flag)
        {
            return PhaseDirectory(flag.Groups[1].Value, flag.Groups[2].Value);
        }

        if (SubPhaseInName().Match(name) is { Success: true } sub)
        {
            return PhaseDirectory(sub.Groups[1].Value, sub.Groups[2].Value);
        }

        if (WholePhaseInName().Match(name) is { Success: true } whole)
        {
            return PhaseDirectory(whole.Groups[1].Value, string.Empty);
        }

        return $"docs/work/{Delivery}";
    }

    /// <summary>Every reason this batch cannot be performed, as messages.</summary>
    /// <remarks>
    /// All of them, not the first: an operator who fixes one refusal and is handed the next one runs
    /// the tool four times to learn what it knew on the first run.
    /// </remarks>
    public IReadOnlyList<string> Refusals(IReadOnlyList<Move> moves) =>
        [.. FrozenPaths(moves), .. Collisions(moves), .. Occupied(moves), .. Untracked(moves)];

    /// <summary>Performs the batch and returns the moves completed.</summary>
    /// <exception cref="FrozenPathException">When any source or target is frozen; nothing is written.</exception>
    /// <exception cref="HalfAppliedException">When <c>git mv</c> fails part-way, carrying what was done.</exception>
    public IReadOnlyList<Move> Perform(IReadOnlyList<Move> moves)
    {
        // The whole batch is guarded again here, immediately before the first write, so deleting
        // the refusal-collecting call cannot leave this stage unguarded. `--delivery ../product-spec`
        // is what this stops.
        Guard.AssertAllWritable([.. moves.SelectMany(move => new[] { move.From, move.To })], Root);
        var done = new List<Move>();
        foreach (var move in moves)
        {
            Directory.CreateDirectory(Path.Combine(Root, Path.GetDirectoryName(move.To) ?? string.Empty));
            var result = Git.Run(Root, ["mv", "--", move.From, move.To]);
            if (!result.Success)
            {
                throw new HalfAppliedException($"{move.Command} failed: {(result.Error + result.Output).Trim()}", done);
            }

            done.Add(move);
        }

        return done;
    }

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    private string PhaseDirectory(string number, string letter)
    {
        var phase = $"docs/work/{Delivery}/phase{number}";
        return letter.Length == 0 ? phase : $"{phase}/phase{number}{letter}";
    }

    private string Renamed(string from)
    {
        var name = FileName(from);
        return Renames.TryGetValue(from, out var byPath) ? byPath
            : Renames.TryGetValue(name, out var byName) ? byName
            : name;
    }

    /// <summary>
    /// The inbox, tracked and untracked alike: its NORMAL state is a file a global skill has just
    /// written and nobody has staged.
    /// </summary>
    private List<string> InboxFiles() =>
    [
        .. Lines(Git.Run(Root, ["ls-files", "--cached", "--others", "--exclude-standard", "--", .. Inbox.Directories]).Output)
            .Where(file => !Inbox.Furniture.Contains(FileName(file)) && file.EndsWith(".md", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    private IEnumerable<string> FrozenPaths(IReadOnlyList<Move> moves)
    {
        foreach (var path in moves.SelectMany(move => new[] { move.From, move.To }))
        {
            if (Guard.FrozenEntryFor(path, Root) is { } entry)
            {
                yield return $"{path} is under the frozen entry '{entry}'; this stage never writes there.";
            }
        }
    }

    /// <summary>
    /// Two inbox files landing on ONE target. <c>specs/</c> and <c>plans/</c> are the two inbox
    /// directories, and a design and its plan can share a basename, so this is the likely collision
    /// rather than the exotic one.
    /// </summary>
    private static IEnumerable<string> Collisions(IReadOnlyList<Move> moves) =>
        moves.GroupBy(move => move.To, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{string.Join(" and ", group.Select(move => move.From))} both land on {group.Key}. " +
                "Rename one with --rename before collecting.");

    private IEnumerable<string> Occupied(IReadOnlyList<Move> moves) =>
        moves.Where(move => File.Exists(Path.Combine(Root, move.To)) || Directory.Exists(Path.Combine(Root, move.To)))
            .Select(move => $"{move.To} already exists (from {move.From}).");

    private IEnumerable<string> Untracked(IReadOnlyList<Move> moves)
    {
        var cached = Lines(Git.Run(Root, ["ls-files", "--cached", "--", .. Inbox.Directories]).Output)
            .ToHashSet(StringComparer.Ordinal);
        return moves.Select(move => move.From).Where(from => !cached.Contains(from)).Select(from =>
            $"{from} is not tracked; `git mv` cannot move it. Run `git add {from}` first -- a phase document is " +
            "worth a commit of its own before it moves, so history follows it.");
    }

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex(@"\A([0-9]+)([a-z]?)\z")]
    private static partial Regex PhaseFlag();

    /// <summary><c>2026-09-05-phase5a-transport-design.md</c> → phase 5, sub-phase a.</summary>
    [GeneratedRegex(@"-phase([0-9]+)([a-z])-")]
    private static partial Regex SubPhaseInName();

    [GeneratedRegex(@"-phase([0-9]+)[-.]")]
    private static partial Regex WholePhaseInName();
}

/// <summary>
/// Thrown when <c>git mv</c> fails mid-batch. Carries how far it got, because an operator left with a
/// stack trace and an index in an unknown state has to reconstruct it by hand.
/// </summary>
internal sealed class HalfAppliedException(string message, IReadOnlyList<Move> done) : Exception(message)
{
    /// <summary>The moves performed before the failure.</summary>
    public IReadOnlyList<Move> Done { get; } = done;
}

/// <summary>The apply stage's command line.</summary>
internal static class ApplyCli
{
    /// <summary>The usage text.</summary>
    public const string Usage =
        "usage: dotnet run --project .claude/skills/housekeeping/src -- apply [options]\n" +
        "  --delivery NAME    unit of delivery under docs/work/ (default: mvp)\n" +
        "  --phase N[x]       file everything under phaseN, or phaseN/phaseNx\n" +
        "  --rename FROM=TO   rename one file as it moves; repeatable\n" +
        "  --write            perform the moves (default is a dry run)\n" +
        "  --dry-run          print the git mv commands and stop (the default)\n" +
        "  --root PATH        operate on this tree instead of the enclosing repository\n" +
        "  -h, --help         this message";

    /// <summary>0 on success or an empty inbox, 1 on any refusal, failure or usage error.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        try
        {
            var reader = new ArgumentReader(args);
            string delivery = "mvp";
            string? phase = null;
            string? root = null;
            var write = false;
            var renames = new Dictionary<string, string>(StringComparer.Ordinal);
            while (reader.Next() is { } arg)
            {
                switch (arg.Name)
                {
                    case "--delivery":
                        delivery = reader.Value(arg);
                        break;
                    case "--phase":
                        phase = reader.Value(arg);
                        break;
                    case "--rename":
                        AddRename(renames, reader.Value(arg));
                        break;
                    case "--write":
                        write = true;
                        break;
                    case "--dry-run":
                        write = false;
                        break;
                    case "--root":
                        root = reader.Value(arg);
                        break;
                    case "-h" or "--help":
                        output.WriteLine(Usage);
                        return 0;
                    default:
                        throw new ArgumentException($"invalid option: {arg.Name}\n{Usage}");
                }
            }

            var apply = new Apply(root ?? Git.RepositoryRoot(), delivery, phase, renames);
            var moves = apply.Plan();
            if (moves.Count == 0)
            {
                output.WriteLine("the inbox is empty; nothing to collect.");
                return 0;
            }

            var stop = apply.Refusals(moves);
            if (stop.Count > 0)
            {
                foreach (var message in stop)
                {
                    error.WriteLine($"refusing: {message}");
                }

                error.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{stop.Count} refusal(s); the whole batch was declined, so the tree is untouched."));
                return 1;
            }

            return write ? Write(output, error, apply, moves) : DryRun(output, moves);
        }
        catch (Exception e) when (e is ArgumentException or FrozenPathException)
        {
            error.WriteLine($"refusing: {e.Message}");
            return 1;
        }
    }

    /// <summary>Records one <c>--rename FROM=TO</c> pair, refusing a value without both halves.</summary>
    private static void AddRename(Dictionary<string, string> renames, string value)
    {
        var equals = value.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0 || equals == value.Length - 1)
        {
            throw new ArgumentException($"--rename wants FROM=TO, got {value}");
        }

        renames[value[..equals]] = value[(equals + 1)..];
    }

    private static int DryRun(TextWriter output, IReadOnlyList<Move> moves)
    {
        foreach (var move in moves)
        {
            output.WriteLine(move.Command);
        }

        output.WriteLine();
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{moves.Count} move(s) planned. Re-run with --write to perform them."));
        return 0;
    }

    private static int Write(TextWriter output, TextWriter error, Apply apply, IReadOnlyList<Move> moves)
    {
        try
        {
            var done = apply.Perform(moves);
            foreach (var move in done)
            {
                output.WriteLine(move.Command);
            }

            output.WriteLine();
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{done.Count} file(s) collected. Two things this stage did NOT do:"));
            output.WriteLine("  1. Repoint references to the old paths. Re-run the probe's links and citations");
            output.WriteLine("     checks and fix what they report, in the same commit as this move.");
            output.WriteLine("  2. Commit. A migration is its own commit, git mv only, so `git log --follow` works.");
            return 0;
        }
        catch (HalfAppliedException e)
        {
            error.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{e.Done.Count} of {moves.Count} move(s) were performed before this failed:"));
            foreach (var move in e.Done)
            {
                error.WriteLine($"  {move.Command}");
            }

            error.WriteLine(e.Message);
            error.WriteLine("The tree is half-collected. `git status` shows the completed moves; finish or revert them.");
            return 1;
        }
    }
}
