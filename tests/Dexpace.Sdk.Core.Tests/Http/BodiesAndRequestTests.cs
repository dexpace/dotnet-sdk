// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http;

[Trait("Category", "Unit")]
public class BodiesAndRequestTests
{
    [Fact]
    public async Task RequestBody_FromString_WritesUtf8AndIsReplayable()
    {
        var body = RequestBody.FromString("héllo");
        Assert.True(body.IsReplayable);

        using var first = new MemoryStream();
        await body.WriteToAsync(first, TestContext.Current.CancellationToken);
        using var second = new MemoryStream();
        await body.WriteToAsync(second, TestContext.Current.CancellationToken);

        Assert.Equal(Encoding.UTF8.GetBytes("héllo"), first.ToArray());
        Assert.Equal(first.ToArray(), second.ToArray());
    }

    [Fact]
    public async Task RequestBody_FromStream_IsSingleUse()
    {
        var body = RequestBody.FromStream(new MemoryStream(Encoding.UTF8.GetBytes("data")));
        using var sink = new MemoryStream();
        await body.WriteToAsync(sink, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<StreamConsumedException>(
            () => body.WriteToAsync(new MemoryStream(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RequestBody_ToReplayable_BuffersSingleUseStream()
    {
        var body = RequestBody.FromStream(new MemoryStream(Encoding.UTF8.GetBytes("data")));
        var replayable = await body.ToReplayableAsync(TestContext.Current.CancellationToken);
        Assert.True(replayable.IsReplayable);

        using var a = new MemoryStream();
        await replayable.WriteToAsync(a, TestContext.Current.CancellationToken);
        using var b = new MemoryStream();
        await replayable.WriteToAsync(b, TestContext.Current.CancellationToken);
        Assert.Equal(a.ToArray(), b.ToArray());
    }

    [Fact]
    public async Task ResponseBody_ReadAsString_UsesContentTypeCharset()
    {
        var body = ResponseBody.FromBytes(
            Encoding.UTF8.GetBytes("{\"ok\":true}"),
            CommonMediaTypes.ApplicationJsonUtf8);
        Assert.Equal("{\"ok\":true}", await body.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResponseBody_SecondReadThrows()
    {
        var body = ResponseBody.FromBytes(Encoding.UTF8.GetBytes("x"));
        _ = await body.ReadAsBytesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<StreamConsumedException>(() => body.OpenReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Request_RejectsRelativeUrl()
    {
        Assert.Throws<ArgumentException>(() => Request.Create(Method.Get, "/relative"));
    }

    [Fact]
    public void Request_WithHelpers_AreNonDestructive()
    {
        var original = Request.Get("https://example.test/items");
        var modified = original.WithHeader("Accept", "application/json");
        Assert.False(original.Headers.Contains("Accept"));
        Assert.Equal("application/json", modified.Headers.Get("accept"));
        Assert.Equal(Method.Get, modified.Method);
    }
}
