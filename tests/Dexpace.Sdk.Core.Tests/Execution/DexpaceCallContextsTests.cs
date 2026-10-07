// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/context/context.test.ts; Ruby cases (R2 CTX-9, R4 measurement, CTX-19) from ruby-sdk@90075b1's 4a design.
// Ruby's gem tests are not in the local clone.

using Dexpace.Sdk.Core.Execution;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Execution;

/// <summary>CTX-18: the public lookup over the shared store (asserts only on its own keys).</summary>
[Trait("Category", "Unit")]
public sealed class DexpaceCallContextsTests
{
    [Fact]
    public void TryGet_of_an_unknown_key_returns_false_and_null()
    {
        Assert.False(DexpaceCallContexts.TryGet(CallKey.Next(), out var found));
        Assert.Null(found);
    }

    [Fact]
    public void TryGet_resolves_a_promoted_context_registered_in_the_shared_store()
    {
        var key = CallKey.Next();
        var requestContext = new DispatchContext(null, key).PromoteToRequest(ExecutionTestSupport.NewRequest());
        try
        {
            Assert.True(DexpaceCallContexts.TryGet(key, out var found));
            Assert.Same(requestContext, found);
        }
        finally
        {
            requestContext.Close();
        }
    }

    [Fact]
    public void TryGet_after_Close_returns_false()
    {
        var key = CallKey.Next();
        var requestContext = new DispatchContext(null, key).PromoteToRequest(ExecutionTestSupport.NewRequest());

        requestContext.Close();

        Assert.False(DexpaceCallContexts.TryGet(key, out _));
    }
}
