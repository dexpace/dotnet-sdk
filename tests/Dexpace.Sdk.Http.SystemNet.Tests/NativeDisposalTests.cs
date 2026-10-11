// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The throwing doubles own nothing; NativeDisposal under test is the one that disposes them.
#pragma warning disable CA2201 // A fatal exception is the point: OutOfMemoryException is what ExceptionFacts.IsFatal recognises.

using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>TRANSPORT-22 and design §3.7 (phase 8b, P8b-17): a dispose that fails on the way out never replaces the failure in flight.</summary>
[Trait("Category", "Unit")]
public sealed class NativeDisposalTests
{
    private sealed class ThrowingDisposable(Exception toThrow) : IDisposable
    {
        public int Disposals { get; private set; }

        public void Dispose()
        {
            Disposals++;
            throw toThrow;
        }
    }

    [Fact]
    public void A_throwing_dispose_is_attached_to_the_primarys_trail_and_not_thrown()
    {
        var primary = new InvalidOperationException("adaptation failed");
        var secondary = new IOException("dispose failed");
        var resource = new ThrowingDisposable(secondary);

        NativeDisposal.DisposeQuietly(resource, primary);

        Assert.Equal(1, resource.Disposals);
        Assert.Same(secondary, Assert.Single(ExceptionTrail.GetSuppressed(primary)));
        Assert.Null(primary.InnerException);
    }

    [Fact]
    public void A_fatal_exception_from_dispose_is_rethrown()
    {
        var primary = new InvalidOperationException("adaptation failed");
        var resource = new ThrowingDisposable(new OutOfMemoryException());

        Assert.Throws<OutOfMemoryException>(() => NativeDisposal.DisposeQuietly(resource, primary));
        Assert.Empty(ExceptionTrail.GetSuppressed(primary));
    }

    [Fact]
    public void A_clean_dispose_adds_nothing()
    {
        var primary = new InvalidOperationException("adaptation failed");
        using var stream = new MemoryStream();

        NativeDisposal.DisposeQuietly(stream, primary);

        Assert.Empty(ExceptionTrail.GetSuppressed(primary));
        Assert.False(stream.CanRead);
    }
}
