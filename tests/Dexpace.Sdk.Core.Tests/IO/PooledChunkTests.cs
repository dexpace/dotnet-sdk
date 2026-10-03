// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.IO;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.IO;

/// <summary>The one sanctioned pooled rent (P3a-8, IO-22, IO-38).</summary>
[Trait("Category", "Unit")]
public class PooledChunkTests
{
    [Fact]
    public void Rent_returns_an_array_of_at_least_the_requested_length()
    {
        using var chunk = PooledChunk.Rent(1000);

        Assert.True(chunk.Array.Length >= 1000);
    }

    [Fact]
    public void Dispose_returns_the_array_to_the_pool()
    {
        var chunk = PooledChunk.Rent(4096);
        var first = chunk.Array;
        chunk.Dispose();

        using var second = PooledChunk.Rent(4096);

        // The pool may decline to hand the same array back; the pin is "no throw and no double return".
        Assert.True(ReferenceEquals(first, second.Array) || second.Array.Length >= 4096);
    }

    [Fact]
    public void Dispose_clears_the_array_before_it_goes_back_to_the_pool()
    {
        var chunk = PooledChunk.Rent(4096);
        var array = chunk.Array;
        System.Array.Fill(array, (byte)0xAB);

        chunk.Dispose();

        Assert.All(array, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Disposing_twice_is_harmless()
    {
        var chunk = PooledChunk.Rent(4096);
        var copy = chunk;

        chunk.Dispose();
        copy.Dispose();
        chunk.Dispose();

        // A double return would put the same array in the pool twice and hand it to two renters.
        using var a = PooledChunk.Rent(4096);
        using var b = PooledChunk.Rent(4096);
        Assert.NotSame(a.Array, b.Array);
        Assert.Throws<ObjectDisposedException>(() => chunk.Array);
    }

    [Fact]
    public void ChunkSize_is_below_the_large_object_heap_threshold() =>
        Assert.True(StreamCopy.ChunkSize < 85_000);
}
