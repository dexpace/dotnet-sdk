// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

/// <summary>R1: the shared get-or-add over the bounded map.</summary>
[Trait("Category", "Unit")]
public sealed class BoundedMapExtensionsTests
{
    private sealed class Box(string name)
    {
        public string Name { get; } = name;
    }

    [Fact]
    public void GetOrAdd_returns_the_existing_value_without_calling_the_factory()
    {
        var map = new BoundedMap<string, Box>(8);
        var first = map.GetOrAdd("k", () => new Box("first"));

        var second = map.GetOrAdd("k", () => throw new InvalidOperationException("must not run"));

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetOrAdd_creates_one_value_under_a_race()
    {
        var map = new BoundedMap<string, Box>(8);
        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(
            i => Task.Run(() => map.GetOrAdd("k", () => new Box("v" + i)), TestContext.Current.CancellationToken)));

        Assert.Single(results.Distinct());
    }
}
