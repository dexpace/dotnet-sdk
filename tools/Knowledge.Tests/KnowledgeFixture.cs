// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Tools.Knowledge.Tests;

/// <summary>
/// A private copy of <c>Fixtures/knowledge/</c> — a hand-written miniature repository root: nine appendix-C
/// rows, three harvested topics, one note, two phase documents. xUnit builds one test-class instance per
/// test, so every test gets its own copy and may write into it (a second note, a stray file, a broken
/// topic) without disturbing a test running in parallel. The Ruby suite edited its shared fixture in
/// place and restored it in an ensure; a copy makes that restore unnecessary.
/// </summary>
public abstract class KnowledgeFixture : IDisposable
{
    // Entry keys the fixture pins. A key digests the entry's text, so editing a fixture bullet moves its
    // key — and the note that cites one has to move with it, the discipline the real corpus enforces.
    protected const string RuleHttp1 = "http-domain-model/03726362";
    protected const string RuleHttp70 = "http-domain-model/a81d135f";
    protected const string RollupHttp2 = "http-domain-model/2fa7d6f2";
    protected const string RulePage1 = "pagination/b895b9ab";
    protected const string ReferencePage2 = "pagination/199b2444";

    private AppendixC? _appendix;
    private Corpus? _corpus;

    protected KnowledgeFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "knowledge-fixture-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(Path.Combine(AppContext.BaseDirectory, "Fixtures", "knowledge"), Root);
        Paths = new KnowledgePaths(Root);
    }

    protected string Root { get; }

    internal KnowledgePaths Paths { get; }

    internal AppendixC Appendix => _appendix ??= AppendixC.Load(Paths);

    internal Corpus Corpus => _corpus ??= Corpus.Load(Paths, Appendix.Prefixes);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A temp directory left behind is harmless; a failing teardown would hide the real result.
        }

        GC.SuppressFinalize(this);
    }

    protected string FixturePath(string relative) => Path.Combine(Root, relative);

    protected void WriteFixture(string relative, string content)
    {
        var path = FixturePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        _corpus = null;
    }

    internal Entry EntryWithKey(string key) =>
        Corpus.Entries.FirstOrDefault(candidate => candidate.Key == key)
        ?? throw new InvalidOperationException($"no fixture entry with key {key}");

    internal Query BuildQuery(Action<QueryOptions> configure, params string[] words) =>
        BuildQuery(configure, TextWriter.Null, words);

    internal Query BuildQuery(Action<QueryOptions> configure, TextWriter warn, params string[] words)
    {
        var options = new QueryOptions();
        configure(options);
        return Query.Build(options, words, Appendix, null, warn);
    }

    internal List<Entry> Select(Action<QueryOptions> configure, params string[] words)
    {
        var query = BuildQuery(configure, words);
        return [.. Corpus.Entries.Where(query.Matches)];
    }

    /// <summary>The whole executable in process, against this test's fixture root.</summary>
    protected (string Stdout, string Stderr, int Status) Run(params string[] argv)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var args = argv.Length > 0 && argv[0] is "verify-structure" or "drift"
            ? [argv[0], "--root", Root, .. argv.Skip(1)]
            : new[] { "--root", Root }.Concat(argv).ToArray();
        var status = Program.Run(args, stdout, stderr);
        return (stdout.ToString(), stderr.ToString(), status);
    }

    protected static (string Stdout, string Stderr, int Status) RunRaw(params string[] argv)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var status = Program.Run(argv, stdout, stderr);
        return (stdout.ToString(), stderr.ToString(), status);
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
