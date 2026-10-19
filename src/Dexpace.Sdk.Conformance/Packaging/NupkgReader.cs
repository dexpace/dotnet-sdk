// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.IO.Compression;

namespace Dexpace.Sdk.Conformance;

/// <summary>Reads one <c>.nupkg</c> with <see cref="System.IO.Compression"/>: the nuspec at its root, the <c>lib/</c> folders, and the assembly named for the package id.</summary>
internal static class NupkgReader
{
    // A packed library is a few hundred kilobytes; a zip entry that claims more than this is a bomb or a mistake, not a library.
    private const long MaxEntryBytes = 64L * 1024 * 1024;

    /// <summary>Reads the package at <paramref name="path"/>.</summary>
    /// <param name="path">A <c>.nupkg</c> file.</param>
    /// <exception cref="FileNotFoundException">The file does not exist; the message names it.</exception>
    /// <exception cref="ConformanceException">The file is not a readable package.</exception>
    internal static PackageFacts Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The package '{Path.GetFileName(path)}' does not exist.", path);
        }

        try
        {
            using var archive = ZipFile.OpenRead(path);
            var nuspec = archive.Entries.SingleOrDefault(static e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains('/', StringComparison.Ordinal))
                ?? throw new ConformanceException($"'{Path.GetFileName(path)}' has no nuspec at its root");

            var libFolders = archive.Entries
                .Select(static e => e.FullName.Split('/'))
                .Where(static parts => parts.Length == 3 && parts[0] == "lib" && parts[2].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(static parts => parts[1])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            var id = Path.GetFileNameWithoutExtension(nuspec.FullName);
            var libraryEntry = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("lib/", StringComparison.Ordinal) && e.FullName.EndsWith("/" + id + ".dll", StringComparison.OrdinalIgnoreCase));
            var assembly = libraryEntry is null ? null : AssemblyMetadataReader.Read(ReadAll(libraryEntry, path));

            return NuspecReader.Read(System.Text.Encoding.UTF8.GetString(ReadAll(nuspec, path)).TrimStart('﻿'), path, libFolders, assembly);
        }
        catch (InvalidDataException ex)
        {
            throw new ConformanceException($"'{Path.GetFileName(path)}' is not a readable package (a corrupt zip)", ex);
        }
    }

    private static byte[] ReadAll(ZipArchiveEntry entry, string path)
    {
        if (entry.Length > MaxEntryBytes)
        {
            throw new ConformanceException($"'{entry.FullName}' in '{Path.GetFileName(path)}' claims {entry.Length} bytes, above the {MaxEntryBytes}-byte reading bound");
        }

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
