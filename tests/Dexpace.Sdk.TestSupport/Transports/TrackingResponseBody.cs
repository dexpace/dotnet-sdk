// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// A response body that records <c>open</c> and <c>dispose</c> events into a shared, ordered log. It overrides
/// <see cref="Dispose(bool)"/>, never <c>Dispose()</c> (the 3b latch rule), and can be told to throw from its dispose.
/// </summary>
public sealed class TrackingResponseBody(List<string> log, string name = "body", Exception? disposeFailure = null) : ResponseBody
{
    private int _opens;
    private int _disposes;

    /// <summary>How many times the body was opened.</summary>
    public int OpenCount => Volatile.Read(ref _opens);

    /// <summary>How many times the body's dispose body ran.</summary>
    public int DisposeCount => Volatile.Read(ref _disposes);

    /// <inheritdoc/>
    public override MediaType? ContentType => null;

    /// <inheritdoc/>
    public override long ContentLength => 4;

    /// <inheritdoc/>
    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _opens);
        lock (log)
        {
            log.Add($"{name}:open");
        }

        return Task.FromResult<Stream>(new MemoryStream([1, 2, 3, 4], writable: false));
    }

    /// <inheritdoc/>
    public override Stream OpenRead(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _opens);
        lock (log)
        {
            log.Add($"{name}:open");
        }

        return new MemoryStream([1, 2, 3, 4], writable: false);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        Interlocked.Increment(ref _disposes);
        lock (log)
        {
            log.Add($"{name}:dispose");
        }

        if (disposeFailure is not null)
        {
            throw disposeFailure;
        }

        base.Dispose(disposing);
    }
}
