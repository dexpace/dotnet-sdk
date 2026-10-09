// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pagination;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-2 and PAGE-4: the strategy's one output.</summary>
[Trait("Category", "Unit")]
public sealed class PageInfoTests
{
    [Fact]
    public void A_null_item_list_is_rejected()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new PageInfo<int>(null!, nextRequest: null));
        Assert.Equal("items", ex.ParamName);
    }

    [Fact]
    public void A_null_next_request_is_the_end_signal_and_is_kept_as_null()
    {
        var info = new PageInfo<int>([1, 2], null);
        Assert.Null(info.NextRequest);
        Assert.Equal([1, 2], info.Items);
    }

    [Fact]
    public void A_next_request_is_carried_with_an_empty_item_list()
    {
        // PAGE-4: an empty list with a non-null next is allowed; the engine fetches again (P7c-2).
        var next = Request.Get("https://api.example/items?page=2");
        var info = new PageInfo<string>([], next);
        Assert.Empty(info.Items);
        Assert.Same(next, info.NextRequest);
    }

    [Fact]
    public void PageInfo_is_a_sealed_class_with_reference_equality()
    {
        // P7c-19: record equality over a list would be reference equality and mislead.
        Assert.True(typeof(PageInfo<int>).IsSealed);
        var items = new[] { 1 };
        Assert.NotEqual(new PageInfo<int>(items, null), new PageInfo<int>(items, null));
    }
}
