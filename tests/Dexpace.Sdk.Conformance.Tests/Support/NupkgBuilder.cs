// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;
using System.Xml.Linq;

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>
/// Builds synthetic <c>.nupkg</c> files for the packaging suite's negative controls (plan 3.1): a zip holding a nuspec and, for each <c>lib/</c> folder,
/// a real managed assembly emitted with <see cref="PersistedAssemblyBuilder"/> carrying exactly the attributes the spec states. Test code only; the
/// kit itself reads packages with no reflection.
/// </summary>
internal static class NupkgBuilder
{
    private const string Facade = "Microsoft.Extensions.Logging.Abstractions";

    /// <summary>The five libraries as a conforming set: one version, the facade, DI with its one third-party root and the transport it names.</summary>
    internal static PackageSpec[] ConformingSet() =>
    [
        new PackageSpec("Dexpace.Sdk.Core"),
        new PackageSpec("Dexpace.Sdk.Http.SystemNet").DependingOn(("Dexpace.Sdk.Core", "1.2.3"), (Facade, "10.0.12")),
        new PackageSpec("Dexpace.Sdk.Serialization.SystemTextJson").DependingOn(("Dexpace.Sdk.Core", "1.2.3"), (Facade, "10.0.12")),
        new PackageSpec("Dexpace.Sdk.Extensions.DependencyInjection").DependingOn(("Dexpace.Sdk.Core", "1.2.3"), ("Dexpace.Sdk.Http.SystemNet", "1.2.3"), ("Microsoft.Extensions.Http", "10.0.12"), (Facade, "10.0.12")),
        new PackageSpec("Dexpace.Sdk.Conformance").DependingOn(("Dexpace.Sdk.Core", "1.2.3"), (Facade, "10.0.12")),
    ];

    /// <summary>The subject over <paramref name="paths"/> with the policies of <c>scripts/ci/dependency-audit.cs</c>.</summary>
    /// <param name="paths">The package files.</param>
    internal static PackagingSubject Subject(IEnumerable<string> paths) =>
        new()
        {
            Name = "synthetic packages",
            PackagePaths = [.. paths],
            Policies = new Dictionary<string, PackagePolicy>
            {
                ["Dexpace.Sdk.Extensions.DependencyInjection"] = new("Microsoft.Extensions.Http", ["Dexpace.Sdk.Http.SystemNet"]),
            },
        };

    /// <summary>Writes <paramref name="spec"/> into <paramref name="directory"/> as <c>Id.Version.nupkg</c> and returns its path.</summary>
    /// <param name="directory">An existing directory.</param>
    /// <param name="spec">The package.</param>
    internal static string Write(string directory, PackageSpec spec)
    {
        var path = Path.Combine(directory, $"{spec.Id}.{spec.Version}.nupkg");
        using var file = File.Create(path);
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        Add(archive, spec.Id + ".nuspec", Encoding.UTF8.GetBytes(Nuspec(spec)));
        if (spec.WithAssembly)
        {
            var assembly = Assembly(spec);
            foreach (var folder in spec.LibFolders)
            {
                Add(archive, $"lib/{folder}/{spec.Id}.dll", assembly);
            }
        }

        return path;
    }

    /// <summary>Writes every spec and returns the paths in the same order.</summary>
    /// <param name="directory">An existing directory.</param>
    /// <param name="specs">The packages.</param>
    internal static string[] WriteAll(string directory, IEnumerable<PackageSpec> specs) => [.. specs.Select(spec => Write(directory, spec))];

    private static void Add(ZipArchive archive, string name, byte[] bytes)
    {
        using var stream = archive.CreateEntry(name).Open();
        stream.Write(bytes);
    }

    private static string Nuspec(PackageSpec spec)
    {
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var metadata = new XElement(ns + "metadata", new XElement(ns + "id", spec.Id), new XElement(ns + "version", spec.Version));
        if (spec.License is not null)
        {
            metadata.Add(new XElement(ns + "license", new XAttribute("type", "expression"), spec.License));
        }

        if (spec.Copyright is not null)
        {
            metadata.Add(new XElement(ns + "copyright", spec.Copyright));
        }

        metadata.Add(new XElement(
            ns + "dependencies",
            spec.Groups.Select(group => new XElement(
                ns + "group",
                new XAttribute("targetFramework", group.Tfm),
                group.Dependencies.Select(d => new XElement(ns + "dependency", new XAttribute("id", d.Id), new XAttribute("version", d.Version)))))));
        return new XDocument(new XElement(ns + "package", metadata)).ToString();
    }

    private static byte[] Assembly(PackageSpec spec)
    {
        var builder = new PersistedAssemblyBuilder(new AssemblyName(spec.Id) { Version = new Version(1, 0, 0, 0) }, typeof(object).Assembly);
        _ = builder.DefineDynamicModule(spec.Id);
        foreach (var (key, value) in spec.Metadata)
        {
            builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!, [key, value]));
        }

        if (spec.InformationalVersion is { } informational)
        {
            builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!, [informational]));
        }

        foreach (var granted in spec.Grants)
        {
            builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(InternalsVisibleToAttribute).GetConstructor([typeof(string)])!, [granted]));
        }

        if (spec.TargetFramework is { } framework)
        {
            builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(TargetFrameworkAttribute).GetConstructor([typeof(string)])!, [framework]));
        }

        using var stream = new MemoryStream();
        builder.Save(stream);
        return stream.ToArray();
    }
}
