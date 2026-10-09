// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pagination;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-2: a page is a response-free value, readable forever.</summary>
[Trait("Category", "Unit")]
public sealed class PageTests
{
    private static readonly Request s_request = Request.Get("https://api.example/items");

    [Fact]
    public void Page_Constructor_SetsProperties()
    {
        var values = new List<int> { 1, 2, 3 };
        var headers = Headers.Empty.With("X-Foo", "bar");

        var page = new Page<int>(values, Status.Ok, headers, s_request);

        Assert.Same(values, page.Values);
        Assert.Equal(Status.Ok, page.Status);
        Assert.Same(headers, page.Headers);
        Assert.Same(s_request, page.Request);
    }

    [Fact]
    public void Page_Constructor_NullValues_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Page<int>(null!, Status.Ok, Headers.Empty, s_request));
    }

    [Fact]
    public void Page_Constructor_NullHeaders_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Page<int>(Array.Empty<int>(), Status.Ok, null!, s_request));
    }

    [Fact]
    public void Page_Constructor_NullRequest_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new Page<int>(Array.Empty<int>(), Status.Ok, Headers.Empty, null!));
        Assert.Equal("request", ex.ParamName);
    }

    [Fact]
    public void A_page_owns_no_response_so_it_is_not_disposable()
    {
        // Design section 10 entry 17 (PAGE-3): the engine closes the response before it yields the page.
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(Page<int>)));
        Assert.False(typeof(IAsyncDisposable).IsAssignableFrom(typeof(Page<int>)));
    }
}
