// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// A transient copy buffer rented from <see cref="ArrayPool{T}.Shared"/> and returned on <see cref="Dispose"/>
/// (P3a-8, design §3.1; styleguide 13.6). It is the one sanctioned <c>ArrayPool&lt;byte&gt;.Rent</c> in core, and its
/// array is never handed to a view or a caller: captured body bytes are never pooled.
/// </summary>
/// <remarks>
/// Single-threaded contract (IO-37): the helpers hold no shared state, so one call per stream at a time, and no
/// concurrent use of the same chunk. The struct is copyable; a small private lease records the return so disposing two
/// copies returns the array once, and reading <see cref="Array"/> after disposal throws
/// <see cref="ObjectDisposedException"/>.
/// </remarks>
internal readonly struct PooledChunk : IDisposable
{
    private readonly Lease? _lease;

    private PooledChunk(Lease lease) => _lease = lease;

    /// <summary>The rented array, at least the requested length. Valid until <see cref="Dispose"/>.</summary>
    internal byte[] Array => _lease?.Array ?? throw new ObjectDisposedException(nameof(PooledChunk));

    /// <summary>Rents a buffer of at least <paramref name="minimumLength"/> bytes.</summary>
    /// <param name="minimumLength">The smallest acceptable length.</param>
    /// <returns>The rented chunk; dispose it to return the array.</returns>
    internal static PooledChunk Rent(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumLength);
#pragma warning disable RS0030 // The sole transient-buffer rent in core: design §3.1 and P3a-8 (captured bytes are never pooled).
        var array = ArrayPool<byte>.Shared.Rent(minimumLength);
#pragma warning restore RS0030
        return new PooledChunk(new Lease(array));
    }

    /// <summary>Returns the array to the pool. Idempotent across copies.</summary>
    public void Dispose() => _lease?.Return();

    private sealed class Lease(byte[] array)
    {
        private byte[]? _array = array;

        internal byte[] Array => _array ?? throw new ObjectDisposedException(nameof(PooledChunk));

        internal void Return()
        {
            var returned = Interlocked.Exchange(ref _array, null);
            if (returned is not null)
            {
                // Bodies may carry secrets (token-endpoint forms), and core cannot tell: clear before the shared pool sees it (styleguide 13.6).
                ArrayPool<byte>.Shared.Return(returned, clearArray: true);
            }
        }
    }
}
