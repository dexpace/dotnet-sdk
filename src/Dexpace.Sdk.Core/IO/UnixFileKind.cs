// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Formats.Tar;

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// Tells a Unix special file (a FIFO, a character or block device, a socket) from a regular one without opening it (BODY-11,
/// HTTP-40; design §10 entry 31, P11-13).
/// </summary>
/// <remarks>
/// <para>
/// The shared framework has no <c>stat</c> of its own: <see cref="FileInfo"/> reports a FIFO or a character device as a file of
/// length 0, and opening a FIFO for read blocks until a writer appears. <c>System.Formats.Tar</c> is in the shared framework
/// and classifies a path by its file type to build an archive header, so writing one entry for the path into an in-memory
/// archive and reading the entry's type back answers the question. A FIFO, a device and a socket are classified from the file's
/// status and opened by nothing (measured: a mode-000 FIFO and an unreadable <c>/dev/mem</c> both classify). A regular file is
/// the one type the writer does open, to attach its content, so a regular file the process may not read fails inside the
/// writer, and it is the only file type that can: that failure (<see cref="UnauthorizedAccessException"/>, or an
/// <see cref="IOException"/> with an errno behind it, a lock for one) classifies the path as regular, which is what an empty
/// file the process cannot read was before the type check existed. The check is made only for a path of length 0, where the
/// open reads no content; a longer file is regular already by the time its size is taken, and a count of 0 never opens the file
/// to write it either (<see cref="Http.Request.FileRequestBody"/>).
/// </para>
/// <para>
/// The one failure that names a kind is the plain <see cref="IOException"/> the writer throws for a file type it has no tar
/// type for, a Unix domain socket (<see cref="IsUnsupportedFileType"/>). No exception is rethrown and none is wrapped, so the
/// path in an operating-system message never reaches the caller; a vanished path is a <see cref="FileNotFoundException"/>, a
/// subtype, and propagates. The classification is a snapshot of the file's type at construction, as the size is.
/// </para>
/// </remarks>
internal static class UnixFileKind
{
    // COR_E_IO, the HResult of an IOException constructed without one: the runtime stamps an errno-derived IOException (EIO, ESTALE,
    // a lock) with the errno, so an IOException that still carries this value was thrown by managed code, not by a system call.
    private const int ManagedIOExceptionHResult = unchecked((int)0x80131620);

    // The archive is one 512-byte header and the 1024-byte end-of-archive marker; the capacity avoids any growth.
    private const int ArchiveCapacity = 2048;

    /// <summary>Whether the tar writer threw because the file's type has no tar type: a Unix domain socket.</summary>
    /// <param name="ex">The exception the writer threw.</param>
    /// <returns>
    /// <see langword="true"/> for an <see cref="IOException"/> itself (not a subtype) that no system call stamped with an errno;
    /// <see langword="false"/> for every other exception.
    /// </returns>
    internal static bool IsUnsupportedFileType(Exception ex) =>
        ex.GetType() == typeof(IOException) && ex.HResult == ManagedIOExceptionHResult;

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
        catch (UnauthorizedAccessException)
        {
            // Only a regular file is opened, so a refusal to open is a regular file the process cannot read.
            return null;
        }
        catch (IOException ex) when (ex.GetType() == typeof(IOException))
        {
            // An errno-stamped failure is an open too (a lock, a stale handle): a regular file. The managed one is the socket's.
            return IsUnsupportedFileType(ex) ? "socket" : null;
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
