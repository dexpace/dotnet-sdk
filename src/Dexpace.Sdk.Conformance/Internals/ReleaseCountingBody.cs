// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A response body that counts its releases at the resource it holds (<c>XCUT-13</c>): the count moves every time the resource is
/// released, whichever path released it, so a component that closes twice shows as two. When parked, the body's stream holds a read
/// open until the assertion lets it go, which is how an assertion tells a close that waits for it from one that does not.
/// </summary>
internal sealed class ReleaseCountingBody : ResponseBody
{
    private readonly ParkedReadStream? _parked;
    private int _releases;

    /// <summary>Creates a body.</summary>
    /// <param name="parked">Whether its stream parks every read until released.</param>
    internal ReleaseCountingBody(bool parked) => _parked = parked ? new ParkedReadStream() : null;

    /// <summary>How many times the resource was released.</summary>
    internal int Releases => Volatile.Read(ref _releases);

    /// <summary>The parked stream, when the body was created parked.</summary>
    internal ParkedReadStream? Parked => _parked;

    /// <inheritdoc />
    public override MediaType? ContentType => null;

    /// <summary>Releases the resource once more: what a close does, and what a component without a latch does on every call.</summary>
    internal void ReleaseResource()
    {
        Interlocked.Increment(ref _releases);
        _parked?.Dispose();
    }

    /// <inheritdoc />
    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(_parked ?? (Stream)new MemoryStream([], writable: false));

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReleaseResource();
        }

        base.Dispose(disposing);
    }
}
