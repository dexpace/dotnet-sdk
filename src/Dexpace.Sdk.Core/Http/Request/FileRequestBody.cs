// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.Http.Request;

/// <summary>
/// A replayable request body over a byte range of a file on disk (HTTP-40, BODY-11 to BODY-13), created with
/// <see cref="RequestBody.FromFile"/>.
/// </summary>
/// <remarks>
/// <para>
/// The size is captured once, at construction, and <see cref="ContentLength"/> is the exact byte count of the range, never
/// <c>-1</c>. Each write opens a fresh read-only handle, seeks to <see cref="Offset"/>, copies exactly
/// <see cref="ContentLength"/> bytes through the shared exact-length copy and disposes the handle in the same write, so no
/// handle outlives a write, successful or failed (BODY-8). The body closes only what it opened. A file that shrank after
/// construction makes the write fail with <see cref="EndOfStreamException"/> naming delivered-of-total bytes (BODY-13); one
/// that grew is ignored past the count. The handle is opened with <c>FileShare.ReadWrite | FileShare.Delete</c> so a
/// writer elsewhere (log shipping, a rotating file) is not locked out for the length of an upload; the guarantee comes
/// from the captured count. A file rewritten in place between two writes breaks byte-identity of the two.
/// </para>
/// <para>
/// <b>Recognisable by type, not zero-copy (BODY-12).</b> A transport can recognise this type and read
/// <see cref="FilePath"/>, <see cref="Offset"/> and <see cref="ContentLength"/>. The SDK does not claim a kernel
/// file-to-socket path: <c>SocketsHttpHandler</c> has none for request content, so the transfer is a pooled-buffer copy
/// from an unbuffered <c>FileStream</c> (<c>bufferSize: 0</c>).
/// </para>
/// <para>
/// <b>Domain limit (design P3b-5, §10).</b> "Regular file" is enforced where the platform can see it: a missing path
/// throws <see cref="FileNotFoundException"/>, a directory throws <see cref="ArgumentException"/>, a symbolic link is
/// resolved to its target before the size is taken. On Unix the shared framework cannot tell a FIFO or a character device
/// from a regular file, and opening a FIFO blocks; such files report a size of 0, so their range is empty and <b>a count
/// of 0 never opens the file</b>. A special file therefore uploads as an empty body instead of failing at construction.
/// </para>
/// <para>Equality is identity.</para>
/// </remarks>
public sealed class FileRequestBody : RequestBody
{
    private const FileShare Sharing = FileShare.ReadWrite | FileShare.Delete;

    internal FileRequestBody(string filePath, long offset, long count, MediaType? contentType)
    {
        FilePath = filePath;
        Offset = offset;
        ContentLength = count;
        ContentType = contentType;
    }

    /// <summary>The full path of the file, as resolved at construction.</summary>
    public string FilePath { get; }

    /// <summary>The byte offset at which the range starts.</summary>
    public long Offset { get; }

    /// <inheritdoc />
    public override MediaType? ContentType { get; }

    /// <summary>The exact number of bytes in the range; never <c>-1</c>.</summary>
    public override long ContentLength { get; }

    /// <inheritdoc />
    public override bool IsReplayable => true;

    /// <inheritdoc />
    /// <exception cref="EndOfStreamException">The file ended before the captured range was delivered.</exception>
    public override async Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (ContentLength == 0)
        {
            return;
        }

        var file = Open(FileOptions.Asynchronous);
        await using var fileScope = file.ConfigureAwait(false);
        await StreamCopy.CopyExactlyAsync(file, destination, ContentLength, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="EndOfStreamException">The file ended before the captured range was delivered.</exception>
    public override void WriteTo(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();
        if (ContentLength == 0)
        {
            return;
        }

        using var file = Open(FileOptions.None);
        StreamCopy.CopyExactly(file, destination, ContentLength, cancellationToken);
    }

    // A fresh read-only handle positioned at the range start (P3b-6); the caller disposes it in the same write.
    private FileStream Open(FileOptions extra)
    {
        var file = new FileStream(FilePath, FileMode.Open, FileAccess.Read, Sharing, bufferSize: 0, FileOptions.SequentialScan | extra);
        try
        {
            file.Seek(Offset, SeekOrigin.Begin);
            return file;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }
}
