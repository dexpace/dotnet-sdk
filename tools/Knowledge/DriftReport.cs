// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Tools.Knowledge;

/// <summary>
/// Reports what has gone stale in <c>docs/knowledge/</c>, in two dimensions: every sha256 recorded in
/// <c>harvested/SOURCES.md</c> against the file on disk, and every <c>&lt;topic&gt;/&lt;8 hex&gt;</c> a note
/// cites against the corpus. A report, not a gate, and deliberately not in CI: no drift state fails it
/// (a missing or malformed manifest still exits 2 — that is the report being unable to run). Drift is
/// normal: a design chapter that a phase edits to record an outcome SHOULD drift, and the fix is a
/// re-harvest, which is a user-invoked skill rather than something CI can do.
/// </summary>
internal sealed class DriftReport
{
    public const string Help = """
        Usage: scripts/knowledge drift [--root DIR]

        Reports what has gone stale in docs/knowledge/: harvested sources whose
        sha256 no longer matches the file on disk, and notes whose cited key no
        longer resolves to an entry. A report, not a gate -- no drift state fails it.

          --root DIR   repository root (default: the checkout this tool was built from)
          -h, --help   print this message

        """;

    private readonly KnowledgePaths _paths;
    private readonly TextWriter _stdout;

    public DriftReport(KnowledgePaths paths, TextWriter stdout)
    {
        _paths = paths;
        _stdout = stdout;
    }

    private enum State
    {
        Ok,
        Drift,
        NotVerifiable,
        Unreadable,
    }

    public int Run()
    {
        if (!Directory.Exists(_paths.KnowledgeDir))
        {
            _stdout.Write($"{_paths.Relative(_paths.KnowledgeDir)} does not exist under {_paths.Root}; nothing has " +
                          "been harvested here yet, so nothing can have drifted.\n");
            return 0;
        }

        ReportSources(SourceManifest.Load(_paths).Rows);
        ReportKeys();
        return 0;
    }

    private static string Label(State state) => state switch
    {
        State.Ok => "OK",
        State.Drift => "DRIFT",
        State.NotVerifiable => "NOT VERIFIABLE",
        _ => "UNREADABLE",
    };

    private void ReportSources(List<SourceRow> rows)
    {
        var counts = Enum.GetValues<State>().ToDictionary(state => state, _ => 0);
        foreach (var row in rows)
        {
            var (state, actual, detail) = StateOf(row);
            counts[state]++;
            if (state == State.Ok)
            {
                continue;
            }

            var text = state switch
            {
                State.Drift => $"recorded {row.Sha}, actual {actual}",
                State.Unreadable => $"read failed: {detail}",
                _ => "file not present in this checkout",
            };
            _stdout.Write($"{Label(state)}\t{row.Path}\t{text}\n");
        }

        _stdout.Write($"\n{rows.Count} harvested sources: {counts[State.Ok]} OK, {counts[State.Drift]} DRIFT, " +
                      $"{counts[State.NotVerifiable]} NOT VERIFIABLE, {counts[State.Unreadable]} UNREADABLE.\n");
        if (counts[State.Drift] > 0)
        {
            _stdout.Write("A drifted source means the harvested entries derived from it describe an older revision. " +
                          "Re-harvest that source, or record what changed as a note under docs/knowledge/notes/. " +
                          "This check never fails the build.\n");
        }

        if (counts[State.NotVerifiable] > 0)
        {
            _stdout.Write("NOT VERIFIABLE means the source is not present in this checkout — expected for a source " +
                          "harvested by absolute path from a sibling repository. Every source this repository " +
                          "harvests is repo-relative, so here it more likely means a file was moved or deleted: " +
                          "re-harvest. It is not a failure.\n");
        }
    }

    // The manifest records a truncated digest, so compare at the recorded width. Only a missing file is NOT
    // VERIFIABLE: an unreadable or wrong-typed path reported as "not present" would hide inside the one
    // state this report teaches the reader to shrug at.
    private (State State, string? Actual, string? Detail) StateOf(SourceRow row)
    {
        var path = _paths.Resolve(row.Path);
        try
        {
            var digest = KnowledgePaths.DigestOf(path, row.Sha.Length);
            return (digest == row.Sha ? State.Ok : State.Drift, digest, null);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return (State.NotVerifiable, null, null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (State.Unreadable, null, e.GetType().Name);
        }
    }

    // A note names a harvested rule by key, whether it overrides that rule or only leans on it. Report every
    // citation that no longer resolves — the rule was reworded, so the note describes something that is not
    // there any more.
    private void ReportKeys()
    {
        var corpus = Corpus.Load(_paths, AppendixC.Load(_paths).Prefixes);
        var dangling = corpus.DanglingKeys();
        foreach (var (note, cited) in dangling)
        {
            _stdout.Write($"STALE KEY\t{note}\tcites {cited}, which no entry carries\n");
        }

        var resolved = corpus.Entries.Where(entry => entry.IsNote).Sum(note => note.Overrides.Count + note.Cites.Count);
        _stdout.Write($"\n{resolved} note citation(s) resolve, {dangling.Count} do not.\n");
        if (dangling.Count > 0)
        {
            _stdout.Write("A stale key means the harvested rule was reworded or re-harvested. Re-read the rule, then " +
                          "update the note to the key it prints now.\n");
        }
    }
}
