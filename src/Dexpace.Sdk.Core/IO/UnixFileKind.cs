// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Formats.Tar;

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// Tells a Unix special file (a FIFO, a character or block device, a socket) from a regular one without opening it (BODY-11,
/// HTTP-40; design §10 entry 31, P11-13).
/// </summary>
/// <remarks>
/// The shared framework has no <c>stat</c> of its own: <see cref="FileInfo"/> reports a FIFO or a character device as a file of
/// length 0, and opening a FIFO for read blocks until a writer appears. <c>System.Formats.Tar</c> is in the shared framework
/// and classifies a path by its file type to build an archive header, without opening a special file, so writing one entry
/// for the path into an in-memory archive and reading the entry's type back answers the question. A regular file is never
/// classified this way except when its length is 0, where the write reads no content; a longer file is regular already by the
/// time its size is taken. No handle is left open on any path. The classification is a snapshot of the file's type at
/// construction, as the size is.
/// </remarks>
internal static class UnixFileKind
{
    // The archive is one 512-byte header and the 1024-byte end-of-archive marker; the capacity avoids any growth.
    private const int ArchiveCapacity = 2048;

    /// <summary>The kind of special file <paramref name="path"/> names, or <see langword="null"/> for a regular file.</summary>
    /// <param name="path">The full path of a file that exists and reports a length of 0.</param>
    /// <returns>
    /// A phrase naming the kind (<c>FIFO (named pipe)</c>, <c>character device</c>, <c>block device</c>, <c>socket</c>,
    /// <c>special file</c>); <see langword="null"/> when the path is a regular file.
    /// </returns>
    /// <exception cref="FileNotFoundException">The path vanished between the existence check and this one.</exception>
    internal static string? SpecialKindOf(string path)
    {
        using var archive = new MemoryStream(ArchiveCapacity);
        try
        {
            using var writer = new TarWriter(archive, TarEntryFormat.Ustar, leaveOpen: true);
            writer.WriteEntry(path, "entry");
        }
        catch (IOException ex) when (ex.GetType() == typeof(IOException))
        {
            // The runtime refuses to archive a file type it has no tar type for, and a Unix domain socket is the one that reaches
            // here; a vanished path is a FileNotFoundException, a subtype, and propagates.
            return "socket";
        }

        archive.Position = 0;
        using var reader = new TarReader(archive, leaveOpen: true);
        return reader.GetNextEntry()?.EntryType switch
        {
            TarEntryType.RegularFile or TarEntryType.V7RegularFile => null,
            TarEntryType.Fifo => "FIFO (named pipe)",
            TarEntryType.CharacterDevice => "character device",
            TarEntryType.BlockDevice => "block device",
            _ => "special file",
        };
    }
}
