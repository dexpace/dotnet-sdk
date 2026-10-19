// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.IO.Compression;
using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Packaging;

/// <summary>Plan 3.1 (V6): the BCL readers of the packaging suite, over synthetic packages.</summary>
[Trait("Category", "Unit")]
public sealed class NupkgReaderTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("nupkg-reader");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void It_reads_the_nuspec_metadata_dependency_groups_and_lib_folders()
    {
        var spec = new PackageSpec("Dexpace.Sdk.Probe").DependingOn(("Dexpace.Sdk.Core", "1.2.3"), ("Microsoft.Extensions.Logging.Abstractions", "10.0.12")) with { License = "MIT", Copyright = "Copyright test" };

        var facts = NupkgReader.Read(NupkgBuilder.Write(_directory.FullName, spec));

        Assert.Equal("Dexpace.Sdk.Probe", facts.Id);
        Assert.Equal("1.2.3", facts.Version);
        Assert.Equal("MIT", facts.License);
        Assert.Equal("Copyright test", facts.Copyright);
        Assert.Equal(["net10.0"], facts.LibFolders);
        var group = Assert.Single(facts.DependencyGroups);
        Assert.Equal("net10.0", group.TargetFramework);
        Assert.Equal(["Dexpace.Sdk.Core:1.2.3", "Microsoft.Extensions.Logging.Abstractions:10.0.12"], group.Dependencies.Select(d => $"{d.Id}:{d.Version}"));
    }

    [Fact]
    public void It_reads_the_assembly_attributes_from_the_metadata_without_loading_it()
    {
        var spec = new PackageSpec("Dexpace.Sdk.Probe") with { InformationalVersion = "1.2.3+abcdef", InternalsVisibleTo = ["Dexpace.Sdk.Probe.Tests", "Other,PublicKey=00"] };

        var assembly = NupkgReader.Read(NupkgBuilder.Write(_directory.FullName, spec)).Assembly;

        Assert.NotNull(assembly);
        Assert.Equal("True", assembly.AssemblyMetadata["IsTrimmable"]);
        Assert.Equal("True", assembly.AssemblyMetadata["IsAotCompatible"]);
        Assert.Equal("1.2.3+abcdef", assembly.InformationalVersion);
        Assert.Equal(["Dexpace.Sdk.Probe.Tests", "Other,PublicKey=00"], assembly.InternalsVisibleTo);
        Assert.Equal(".NETCoreApp,Version=v10.0", assembly.TargetFramework);
    }

    [Fact]
    public void A_package_without_its_assembly_has_no_assembly_facts()
    {
        var facts = NupkgReader.Read(NupkgBuilder.Write(_directory.FullName, new PackageSpec("Dexpace.Sdk.Probe") with { WithAssembly = false }));

        Assert.Null(facts.Assembly);
        Assert.Empty(facts.LibFolders);
    }

    [Fact]
    public void A_real_compiled_assembly_with_attributes_of_every_shape_is_read()
    {
        // Phase 10 lesson (3.5): a compiled library carries attributes with integer and array arguments (CompilationRelaxations, Debuggable, Extension);
        // only the four string-constructor attributes may be decoded, or a real package is "not a readable managed assembly".
        var facts = AssemblyMetadataReader.Read(File.ReadAllBytes(typeof(Dexpace.Sdk.Core.Http.Request.Request).Assembly.Location));

        Assert.Equal("True", facts.AssemblyMetadata["IsTrimmable"]);
        Assert.Equal("True", facts.AssemblyMetadata["IsAotCompatible"]);
        Assert.Contains("Dexpace.Sdk.Core.Tests", facts.InternalsVisibleTo);
        Assert.Equal(".NETCoreApp,Version=v10.0", facts.TargetFramework);
        Assert.False(string.IsNullOrEmpty(facts.InformationalVersion));
    }

    [Fact]
    public void A_missing_file_names_the_file()
    {
        var failure = Assert.Throws<FileNotFoundException>(() => NupkgReader.Read(Path.Combine(_directory.FullName, "Missing.1.0.0.nupkg")));

        Assert.Contains("Missing.1.0.0.nupkg", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_corrupt_zip_is_a_clear_failure_not_a_crash()
    {
        var path = Path.Combine(_directory.FullName, "Corrupt.1.0.0.nupkg");
        File.WriteAllText(path, "this is not a zip archive");

        var failure = Assert.Throws<ConformanceException>(() => NupkgReader.Read(path));

        Assert.Contains("Corrupt.1.0.0.nupkg", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_package_without_a_nuspec_and_a_library_that_is_not_an_assembly_are_clear_failures()
    {
        var empty = Path.Combine(_directory.FullName, "Empty.1.0.0.nupkg");
        using (var archive = ZipFile.Open(empty, ZipArchiveMode.Create))
        {
            _ = archive.CreateEntry("readme.txt");
        }

        var notAssembly = Path.Combine(_directory.FullName, "Dexpace.Sdk.Fake.1.0.0.nupkg");
        using (var archive = ZipFile.Open(notAssembly, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("Dexpace.Sdk.Fake.nuspec").Open()))
            {
                writer.Write("<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata><id>Dexpace.Sdk.Fake</id><version>1.0.0</version></metadata></package>");
            }

            using var dll = new StreamWriter(archive.CreateEntry("lib/net10.0/Dexpace.Sdk.Fake.dll").Open());
            dll.Write("not a PE image");
        }

        Assert.Contains("no nuspec", Assert.Throws<ConformanceException>(() => NupkgReader.Read(empty)).Message, StringComparison.Ordinal);
        Assert.Contains("managed assembly", Assert.Throws<ConformanceException>(() => NupkgReader.Read(notAssembly)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_nuspec_reader_refuses_malformed_xml_missing_ids_and_a_dtd()
    {
        Assert.Contains("not well-formed", Assert.Throws<ConformanceException>(() => NuspecReader.Read("<package><metadata>", "x.nupkg", [], null)).Message, StringComparison.Ordinal);
        Assert.Contains("no id or version", Assert.Throws<ConformanceException>(() => NuspecReader.Read("<package><metadata><id>x</id></metadata></package>", "x.nupkg", [], null)).Message, StringComparison.Ordinal);
        Assert.Contains("no metadata", Assert.Throws<ConformanceException>(() => NuspecReader.Read("<package />", "x.nupkg", [], null)).Message, StringComparison.Ordinal);
        Assert.Throws<ConformanceException>(() => NuspecReader.Read("<!DOCTYPE package [<!ENTITY x \"y\">]><package><metadata><id>x</id><version>1</version></metadata></package>", "x.nupkg", [], null));
    }
}
