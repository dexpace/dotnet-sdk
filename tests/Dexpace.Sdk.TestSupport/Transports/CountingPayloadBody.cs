// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Streams;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// A readable <see cref="ResponseBody"/> over a fixed payload, for the typed-reader and response-handler tests: it counts how
/// often the body is released and how often the stream it handed out is disposed, can fail its own release, and
/// <b>throws</b> from every materialising reader, so a test proves a decode streams rather than buffering (the "reads never
/// materialise" rule of SERDE-27).
/// </summary>
/// <param name="payload">The bytes served by every open.</param>
/// <param name="contentLength">The declared length; <c>-1</c> for unknown.</param>
/// <param name="disposeFailure">An exception thrown by the body's release (after the count), or <see langword="null"/>.</param>
public sealed class CountingPayloadBody(byte[] payload, long contentLength = -1, Exception? disposeFailure = null) : ResponseBody
{
    private int _disposeCount;
    private int _openCount;

    /// <summary>How many times the body was released: at most one (the base class latches it).</summary>
    public int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <summary>How many streams the body handed out.</summary>
    public int OpenCount => Volatile.Read(ref _openCount);

    /// <summary>The stream the last open handed out, or <see langword="null"/>.</summary>
    public DisposeCountingStream? LastStream { get; private set; }

    /// <summary>The token the last open was given.</summary>
    public CancellationToken LastOpenToken { get; private set; }

    /// <summary>How many times the last handed-out stream was disposed.</summary>
    public int StreamDisposeCount => LastStream?.DisposeCount ?? 0;

    /// <inheritdoc />
    public override MediaType? ContentType => null;

    /// <inheritdoc />
    public override long ContentLength => contentLength;

    /// <inheritdoc />
    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(Open(cancellationToken));

    /// <inheritdoc />
    public override Stream OpenRead(CancellationToken cancellationToken = default) => Open(cancellationToken);

    /// <inheritdoc />
    public override Task<byte[]> ReadAsBytesAsync(CancellationToken cancellationToken = default) =>
        throw Materialised();

    /// <inheritdoc />
    public override byte[] ReadAsBytes(CancellationToken cancellationToken = default) => throw Materialised();

    /// <inheritdoc />
    public override Task<string> ReadAsStringAsync(CancellationToken cancellationToken = default) =>
        throw Materialised();

    /// <inheritdoc />
    public override string ReadAsString(CancellationToken cancellationToken = default) => throw Materialised();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        Interlocked.Increment(ref _disposeCount);
        base.Dispose(disposing);
        if (disposeFailure is not null)
        {
            throw disposeFailure;
        }
    }

    private DisposeCountingStream Open(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _openCount);
        LastOpenToken = cancellationToken;
        LastStream = new DisposeCountingStream(new MemoryStream(payload, writable: false));
        return LastStream;
    }

    private static InvalidOperationException Materialised() =>
        new("A typed read must stream the body; it materialised it instead.");
}
