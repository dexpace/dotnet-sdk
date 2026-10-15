// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Xml;
using System.Xml.Linq;

namespace Dexpace.Sdk.Conformance;

/// <summary>Reads the metadata and dependency groups of a nuspec with <see cref="System.Xml.Linq"/>, with no DTD and no resolver.</summary>
internal static class NuspecReader
{
    /// <summary>Parses <paramref name="nuspec"/>.</summary>
    /// <param name="nuspec">The nuspec's XML text.</param>
    /// <param name="path">The package file, for the facts.</param>
    /// <param name="libFolders">The <c>lib/</c> folders found in the package.</param>
    /// <param name="assembly">The package's main assembly facts, or <see langword="null"/>.</param>
    /// <exception cref="ConformanceException">The nuspec is not well-formed or has no id or version.</exception>
    internal static PackageFacts Read(string nuspec, string path, IReadOnlyList<string> libFolders, AssemblyFacts? assembly)
    {
        XDocument document;
        try
        {
            using var reader = XmlReader.Create(new StringReader(nuspec), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            document = XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new ConformanceException($"the nuspec of '{Path.GetFileName(path)}' is not well-formed XML", ex);
        }

        var metadata = document.Root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "metadata")
            ?? throw new ConformanceException($"the nuspec of '{Path.GetFileName(path)}' has no metadata element");
        string? Text(string name) => metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();

        var id = Text("id");
        var version = Text("version");
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(version))
        {
            throw new ConformanceException($"the nuspec of '{Path.GetFileName(path)}' has no id or version");
        }

        var license = metadata.Elements().FirstOrDefault(static e => e.Name.LocalName == "license" && (string?)e.Attribute("type") == "expression")?.Value.Trim();
        var groups = metadata.Elements().FirstOrDefault(static e => e.Name.LocalName == "dependencies")?.Elements().Where(static e => e.Name.LocalName == "group").Select(static group =>
            new DependencyGroup(
                (string?)group.Attribute("targetFramework") ?? string.Empty,
                [.. group.Elements().Where(static e => e.Name.LocalName == "dependency").Select(static d => new PackageDependency((string?)d.Attribute("id") ?? string.Empty, (string?)d.Attribute("version") ?? string.Empty))]))
            .ToArray() ?? [];

        return new PackageFacts(path, id, version, license, Text("copyright"), groups, libFolders, assembly);
    }
}
