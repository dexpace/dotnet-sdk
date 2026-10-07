// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Recovery;

/// <summary>
/// A response body for recovery tests: it supports both read forms, counts opens and releases, and can fail its read
/// or its release on demand.
/// </summary>
public sealed class ProbeResponseBody : ResponseBody
{
    private readonly byte[] _payload;
    private readonly Exception? _readFailure;
    private readonly Exception? _disposeFailure;
    private int _opens;
    private int _releases;

    /// <summary>Creates the body.</summary>
    /// <param name="payload">The bytes it serves; empty when <see langword="null"/>.</param>
    /// <param name="contentType">The media type, or <see langword="null"/>.</param>
    /// <param name="readFailure">An exception every read throws, or <see langword="null"/>.</param>
    /// <param name="disposeFailure">An exception the (single) release throws, or <see langword="null"/>.</param>
    public ProbeResponseBody(
        byte[]? payload = null,
        MediaType? contentType = null,
        Exception? readFailure = null,
        Exception? disposeFailure = null)
    {
        _payload = payload ?? [];
        ContentType = contentType;
        _readFailure = readFailure;
        _disposeFailure = disposeFailure;
    }

    /// <summary>How many times the body was opened.</summary>
    public int OpenCount => Volatile.Read(ref _opens);

    /// <summary>How many times the release ran (at most one: the base class latches it).</summary>
    public int DisposeCount => Volatile.Read(ref _releases);

    /// <inheritdoc />
    public override MediaType? ContentType { get; }

    /// <inheritdoc />
    public override long ContentLength => _payload.Length;

    /// <inheritdoc />
    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Open());

    /// <inheritdoc />
    public override Stream OpenRead(CancellationToken cancellationToken = default) => Open();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        Interlocked.Increment(ref _releases);
        base.Dispose(disposing);
        if (disposing && _disposeFailure is not null)
        {
            throw _disposeFailure;
        }
    }

    private Stream Open()
    {
        Interlocked.Increment(ref _opens);
        return _readFailure is null ? new MemoryStream(_payload, writable: false) : new FailingReadStream(_readFailure);
    }

    private sealed class FailingReadStream(Exception failure) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw failure;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(failure);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
