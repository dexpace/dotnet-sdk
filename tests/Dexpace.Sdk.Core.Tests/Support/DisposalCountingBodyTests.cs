// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Support;

/// <summary>The disposal-counting body the SEAM-30 bridge tests rely on (plan task 3.1).</summary>
[Trait("Category", "Unit")]
public sealed class DisposalCountingBodyTests
{
    [Fact]
    public async Task Dispose_and_DisposeAsync_release_once()
    {
        var body = new DisposalCountingBody();
        Assert.Equal(0, body.DisposeCount);

        body.Dispose();
        Assert.Equal(1, body.DisposeCount);

        await body.DisposeAsync();
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task The_body_is_empty_and_readable()
    {
        using var body = new DisposalCountingBody();

        await using var stream = await body.OpenReadAsync(TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);
        Assert.Equal(0, stream.Length);
        Assert.Empty(await body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void It_carries_the_media_type_it_was_given()
    {
        using var typed = new DisposalCountingBody(CommonMediaTypes.TextPlain);
        using var untyped = new DisposalCountingBody();

        Assert.Equal(CommonMediaTypes.TextPlain, typed.ContentType);
        Assert.Null(untyped.ContentType);
    }
}
