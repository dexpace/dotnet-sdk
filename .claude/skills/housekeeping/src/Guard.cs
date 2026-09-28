// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Housekeeping;

/// <summary>
/// The frozen-path guard. Three trees, one corpus and two files in <c>docs/</c> are read-only to
/// this skill, and that has to be a check rather than a paragraph of good intent: the apply stage
/// moves files, and a glob that widens by one segment is exactly how a maintenance tool eats a
/// normative document.
/// </summary>
/// <remarks>
/// Every write the skill performs goes through <see cref="AssertWritable"/> or
/// <see cref="AssertAllWritable"/> first. <c>tests/GuardTests.cs</c> is what proves it, including
/// the four ways a naive prefix test gets it wrong: a sibling whose name merely starts with a
/// frozen one, a <c>..</c> segment that lands inside after normalization, an absolute path, and a
/// symlink whose target is inside a frozen tree while its own path is not.
/// </remarks>
internal static class Guard
{
    /// <summary>
    /// The entries this skill must never write to. ONE list; widening it is a reviewed diff (a test
    /// pins it) rather than a silent change scattered across call sites.
    /// </summary>
    /// <remarks>
    /// <c>docs/knowledge</c> covers both <c>harvested/</c> and <c>notes/</c>. <c>harvested/</c>
    /// cannot absorb a hand edit at all — a <c>&lt;sub&gt;</c> sha digests the whole source file
    /// rather than the entry, so an edit inside one changes no sha and the next harvest regenerates
    /// or duplicates it silently. <c>docs/styleguide</c> is vendored from the styleguide repository
    /// and is re-vendored, never edited in place.
    ///
    /// Both SHAPES are represented on purpose, and the matcher handles each: a directory prefix
    /// (<c>docs/product-spec</c>) and an exact file (<c>docs/product-spec.md</c>).
    /// <c>docs/product-spec</c> does NOT cover <c>docs/product-spec.md</c>, because the comparison
    /// is segment-wise; the table of contents is frozen only because it is listed separately.
    /// </remarks>
    public static readonly IReadOnlyList<string> Frozen =
    [
        "docs/knowledge",
        "docs/product-spec",
        "docs/sdk-design-dotnet",
        "docs/styleguide",
        "docs/product-spec.md",
        "docs/sdk-design-dotnet.md",
    ];

    private const int MaxSymlinkHops = 40;

    /// <summary>Which frozen entry <paramref name="candidate"/> falls under, or <c>null</c>.</summary>
    /// <remarks>
    /// Resolved against <paramref name="repoRoot"/> and compared SEGMENT-WISE, never as a raw string
    /// prefix: <c>docs/product-spec-draft/x.md</c> starts with <c>docs/product-spec</c> as characters
    /// and is not under it as a path. <c>..</c> is normalized away first, so a path that spells its
    /// way in cannot spell its way past the check — and symlinks are resolved, so a path that
    /// <em>links</em> its way in cannot either.
    /// </remarks>
    public static string? FrozenEntryFor(string candidate, string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var target = Segments(ResolveNearest(Path.GetFullPath(candidate, root)));
        foreach (var entry in Frozen)
        {
            if (Under(target, Segments(ResolveNearest(Path.GetFullPath(entry, root)))))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary><c>true</c> when <paramref name="candidate"/> is a frozen entry or lives under one.</summary>
    public static bool IsFrozen(string candidate, string repoRoot) => FrozenEntryFor(candidate, repoRoot) is not null;

    /// <summary>
    /// Throws <see cref="FrozenPathException"/> when <paramref name="candidate"/> is frozen; returns it
    /// otherwise, so a call site reads <c>File.WriteAllText(Guard.AssertWritable(p, root), text)</c>
    /// and cannot forget the check.
    /// </summary>
    public static string AssertWritable(string candidate, string repoRoot)
    {
        var entry = FrozenEntryFor(candidate, repoRoot);
        return entry is null ? candidate : throw new FrozenPathException(candidate, entry);
    }

    /// <summary>
    /// Guards a whole batch before any of it is performed, so a run cannot half-apply and leave the
    /// tree between two states.
    /// </summary>
    public static IReadOnlyList<string> AssertAllWritable(IReadOnlyList<string> candidates, string repoRoot)
    {
        foreach (var candidate in candidates)
        {
            AssertWritable(candidate, repoRoot);
        }

        return candidates;
    }

    /// <summary>Frozen entries that exist and are symlinks.</summary>
    /// <remarks>
    /// A frozen tree replaced by a link is the one way the guard can be correct and useless at the
    /// same time: it would keep refusing the path while the bytes it protects live somewhere the
    /// skill happily writes. The probe reports it; nothing here repairs it.
    /// </remarks>
    public static IReadOnlyList<string> FrozenSymlinks(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        return [.. Frozen.Where(entry => new FileInfo(Path.Combine(root, entry)).LinkTarget is not null)];
    }

    /// <summary>
    /// <paramref name="path"/> (absolute) with every symlink in it resolved, as far as the
    /// filesystem actually goes.
    /// </summary>
    /// <remarks>
    /// <see cref="Path.GetFullPath(string)"/> is purely lexical, so on its own it answers the wrong
    /// question: with <c>docs/work</c> a symlink to <c>docs/product-spec</c>,
    /// <c>docs/work/mvp/x.md</c> lexically escapes the frozen tree and physically lands inside it —
    /// and <see cref="Directory.CreateDirectory(string)"/> follows the link, so a <c>git mv</c> would
    /// write there while the guard said yes. A target that does not exist yet is the normal case
    /// for a move, so this walks up to the nearest ancestor that does, resolves that, and
    /// re-attaches the tail.
    /// </remarks>
    internal static string ResolveNearest(string path)
    {
        var current = Path.TrimEndingDirectorySeparator(path);
        var tail = new List<string>();
        while (true)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                return Path.Combine([RealPath(current), .. tail]);
            }

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent == current)
            {
                // Root reached without anything existing: answer lexically.
                return path;
            }

            tail.Insert(0, Path.GetFileName(current));
            current = parent;
        }
    }

    /// <summary>
    /// The physical path of an existing absolute <paramref name="path"/>, resolved component by
    /// component the way POSIX <c>realpath(3)</c> does. The BCL resolves only the final component of
    /// a link (<see cref="FileSystemInfo.ResolveLinkTarget(bool)"/>), and a link in the MIDDLE of a
    /// path is exactly the case this guard exists for.
    /// </summary>
    internal static string RealPath(string path)
    {
        var current = Path.GetPathRoot(path) ?? string.Empty;
        var pending = new Stack<string>(Segments(path).AsEnumerable().Reverse());
        var hops = 0;
        while (pending.Count > 0)
        {
            var segment = pending.Pop();
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                current = Path.GetDirectoryName(current) ?? current;
                continue;
            }

            var next = Path.Combine(current, segment);
            var link = new FileInfo(next).LinkTarget;
            if (link is null)
            {
                current = next;
                continue;
            }

            if (++hops > MaxSymlinkHops)
            {
                throw new IOException($"too many levels of symbolic links resolving {path}");
            }

            if (Path.IsPathRooted(link))
            {
                current = Path.GetPathRoot(link) ?? current;
            }

            foreach (var part in Segments(link).AsEnumerable().Reverse())
            {
                pending.Push(part);
            }
        }

        return current;
    }

    /// <summary>A path to its segments below its root. <c>/repo/docs/x.md</c> → <c>repo, docs, x.md</c>.</summary>
    private static string[] Segments(string path)
    {
        var root = Path.GetPathRoot(path) ?? string.Empty;
        return path[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Containment, segment-wise. Equal paths count as contained, which is what makes a frozen entry
    /// refuse itself.
    /// </summary>
    private static bool Under(string[] target, string[] entry) =>
        entry.Length > 0 && entry.Length <= target.Length && target.AsSpan(0, entry.Length).SequenceEqual(entry);
}

/// <summary>Thrown instead of writing. Carries the offending path so a caller can report it.</summary>
internal sealed class FrozenPathException : Exception
{
    /// <summary>Creates the exception for <paramref name="path"/> under <paramref name="entry"/>.</summary>
    public FrozenPathException(string path, string entry)
        : base(
            $"refusing to write {path}: it is under the frozen entry '{entry}'. The housekeeping skill " +
            "reads the normative, harvested and vendored trees; it never writes to them. " +
            "See .claude/skills/housekeeping/SKILL.md.")
    {
        PathName = path;
        Entry = entry;
    }

    /// <summary>The path that was refused, as the caller spelled it.</summary>
    public string PathName { get; }

    /// <summary>The frozen entry it falls under.</summary>
    public string Entry { get; }
}
