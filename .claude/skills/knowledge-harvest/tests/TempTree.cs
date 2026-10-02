// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace KnowledgeHarvest.Tests;

/// <summary>A throwaway directory tree, deleted on dispose.</summary>
internal sealed class TempTree : IDisposable
{
    public TempTree() => Root = Directory.CreateTempSubdirectory("knowledge-harvest-").FullName;

    public string Root { get; }

    public string Write(string relative, string text)
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public string Combine(string relative) => Path.Combine(Root, relative);

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
