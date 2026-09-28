// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Tools.Knowledge.Tests;

/// <summary>
/// The repository's own corpus, checked for shape rather than for counts — a test that pins live counts
/// fails on every harvest. It proves the parser and the gate agree with what is actually committed.
/// </summary>
public sealed class RealCorpusTests
{
    private static readonly string s_root = FindRoot();

    [Fact]
    public void TheCommittedCorpus_PassesTheStructureGate()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var status = Program.Run(["verify-structure", "--root", s_root], stdout, stderr);
        Assert.True(status == 0, stderr.ToString());
    }

    [Fact]
    public void EveryHarvestedBullet_HasExactlyOneSubLineAndAKey()
    {
        var paths = new KnowledgePaths(s_root);
        if (!Directory.Exists(paths.HarvestedDir))
        {
            return;
        }

        var corpus = Corpus.Load(paths, AppendixC.Load(paths).Prefixes);
        foreach (var topic in Corpus.TopicFiles(paths))
        {
            var bullets = File.ReadLines(topic.Path).Count(line => line.StartsWith("- ", StringComparison.Ordinal));
            var subs = File.ReadLines(topic.Path).Count(line => line.StartsWith("  <sub>", StringComparison.Ordinal));
            Assert.True(bullets == subs, $"{topic.File}: {bullets} bullets, {subs} <sub> lines");
        }

        Assert.All(corpus.Entries, entry => Assert.Matches("^[a-z0-9-]+/[0-9a-f]{8}$", entry.Key));
    }

    [Fact]
    public void AppendixC_ParsesIntoTheFullCanonicalSet()
    {
        var appendix = AppendixC.Load(new KnowledgePaths(s_root));
        Assert.True(appendix.Count > 600, $"only {appendix.Count} IDs parsed");
        Assert.Contains("RETRY", appendix.Prefixes);
    }

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "tools", "Knowledge", "Knowledge.csproj")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("cannot locate the repository root above " + AppContext.BaseDirectory);
    }
}
