// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.TestSupport.Transports;

/// <summary>
/// An empty, readable <see cref="ResponseBody"/> that counts how many times it was disposed, through either
/// <c>Dispose</c> or <c>DisposeAsync</c> (the base class routes the second through the first). Proves a response is
/// closed exactly once or never (SEAM-30).
/// </summary>
/// <param name="contentType">The media type to carry, or <see langword="null"/> for none.</param>
public sealed class DisposalCountingBody(MediaType? contentType = null) : ResponseBody
{
    private int _disposeCount;

    /// <summary>How many times the body has been disposed, by either form.</summary>
    public int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <inheritdoc />
    public override MediaType? ContentType { get; } = contentType;

    /// <inheritdoc />
    public override long ContentLength => 0;

    /// <inheritdoc />
    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream([], writable: false));

    /// <inheritdoc />
    public override void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
        base.Dispose();
    }
}
