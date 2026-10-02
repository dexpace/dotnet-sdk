// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>HTTP-9: the single source of method idempotency (design section 6.1).</summary>
[Trait("Category", "Unit")]
public class RetryFactsTests
{
    [Fact]
    public void IdempotentMethods_is_exactly_GET_HEAD_OPTIONS_PUT_DELETE()
    {
        var names = RetryFacts.IdempotentMethods.Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["DELETE", "GET", "HEAD", "OPTIONS", "PUT"], names);
    }

    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("OPTIONS", true)]
    [InlineData("PUT", true)]
    [InlineData("DELETE", true)]
    [InlineData("POST", false)]
    [InlineData("PATCH", false)]
    [InlineData("TRACE", false)]
    [InlineData("CONNECT", false)]
    public void Method_IsIdempotent_reads_the_set_for_all_nine_methods(string token, bool expected)
    {
        var method = Method.Of(token);

        Assert.Equal(expected, method.IsIdempotent);
        Assert.Equal(expected, RetryFacts.IdempotentMethods.Contains(method));
    }

    [Fact]
    public void A_vendor_method_is_not_idempotent()
    {
        Assert.False(Method.Of("PROPFIND").IsIdempotent);
        Assert.DoesNotContain(Method.Of("PROPFIND"), RetryFacts.IdempotentMethods.AsEnumerable());
    }
}
