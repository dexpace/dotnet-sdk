// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Common;

public class HeadersTests
{
    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var headers = Headers.Empty.Set("Content-Type", "application/json");
        Assert.Equal("application/json", headers.Get("content-type"));
        Assert.Equal("application/json", headers.Get("CONTENT-TYPE"));
        Assert.True(headers.Contains("Content-Type"));
    }

    [Fact]
    public void With_AppendsMultipleValues()
    {
        var headers = Headers.Empty
            .With("Accept", "text/plain")
            .With("Accept", "application/json");
        Assert.Equal((string[])["text/plain", "application/json"], headers.GetAll("accept"));
    }

    [Fact]
    public void Set_ReplacesExistingValues()
    {
        var headers = Headers.Empty
            .With("X-Test", "one")
            .With("X-Test", "two")
            .Set("X-Test", "final");
        Assert.Equal((string[])["final"], headers.GetAll("x-test"));
    }

    [Fact]
    public void Mutation_IsNonDestructive()
    {
        var original = Headers.Empty.Set("A", "1");
        var modified = original.With("A", "2");
        Assert.Single(original.GetAll("a"));
        Assert.Equal(2, modified.GetAll("a").Count);
    }

    [Fact]
    public void Without_RemovesName()
    {
        var headers = Headers.Empty.Set("A", "1").Without("a");
        Assert.False(headers.Contains("A"));
    }

    [Fact]
    public void Builder_BatchesEdits()
    {
        var headers = new Headers.Builder()
            .Add("A", "1")
            .Add("A", "2")
            .Set("B", "x")
            .Build();
        Assert.Equal((string[])["1", "2"], headers.GetAll("a"));
        Assert.Equal("x", headers.Get("b"));
    }
}
