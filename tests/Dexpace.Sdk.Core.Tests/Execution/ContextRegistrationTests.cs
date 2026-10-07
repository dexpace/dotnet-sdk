// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/context/store.test.ts; Ruby cases (R2 CTX-9, R4 measurement, CTX-19) from ruby-sdk@90075b1's 4a design.
// Ruby's gem tests are not in the local clone.

using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Execution;

/// <summary>CTX-3, CTX-4, CTX-9, CTX-10, CTX-17, CTX-18: promotion registers, close evicts the terminal link only.</summary>
[Trait("Category", "Unit")]
public sealed class ContextRegistrationTests
{
    [Fact]
    public void A_constructed_dispatch_context_is_not_registered()
    {
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store);

        Assert.False(store.TryGet(dispatch.Key, out _));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void An_off_chain_request_context_is_not_registered()
    {
        var store = new ContextStore(10);
        var request = ExecutionTestSupport.NewRequest();
        using var response = ExecutionTestSupport.NewResponse(request);

        _ = new RequestContext(request, null, null, null, store);
        _ = new ExchangeContext(request, response, null, null, null, store);

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void The_first_promotion_installs_the_first_entry()
    {
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store);

        var next = dispatch.PromoteToRequest(ExecutionTestSupport.NewRequest());

        Assert.True(store.TryGet(dispatch.Key, out var found));
        Assert.Same(next, found);
    }

    [Fact]
    public void Every_flavour_of_one_chain_occupies_the_same_slot_in_turn()
    {
        var store = new ContextStore(10);
        var request = ExecutionTestSupport.NewRequest();
        using var response = ExecutionTestSupport.NewResponse(request);
        var dispatch = ExecutionTestSupport.NewDispatch(store);

        var requestContext = dispatch.PromoteToRequest(request);
        Assert.True(store.TryGet(dispatch.Key, out var afterRequest));
        Assert.Same(requestContext, afterRequest);

        var exchange = requestContext.PromoteToExchange(response);
        Assert.True(store.TryGet(dispatch.Key, out var afterExchange));
        Assert.Same(exchange, afterExchange);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Two_contexts_with_identical_trace_and_span_both_register()
    {
        var store = new ContextStore(10);
        var bundle = ExecutionTestSupport.NewBundle();

        ExecutionTestSupport.NewDispatch(store, bundle: bundle).PromoteToRequest(ExecutionTestSupport.NewRequest());
        ExecutionTestSupport.NewDispatch(store, bundle: bundle).PromoteToRequest(ExecutionTestSupport.NewRequest());

        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void Closing_an_intermediate_link_leaves_the_successor_registered()
    {
        var store = new ContextStore(10);
        var request = ExecutionTestSupport.NewRequest();
        using var response = ExecutionTestSupport.NewResponse(request);
        var dispatch = ExecutionTestSupport.NewDispatch(store);
        var requestContext = dispatch.PromoteToRequest(request);
        var exchange = requestContext.PromoteToExchange(response);

        dispatch.Close();
        requestContext.Close();

        Assert.True(store.TryGet(dispatch.Key, out var found));
        Assert.Same(exchange, found);
    }

    [Fact]
    public void Closing_the_terminal_link_evicts_the_chain()
    {
        var store = new ContextStore(10);
        var request = ExecutionTestSupport.NewRequest();
        using var response = ExecutionTestSupport.NewResponse(request);
        var dispatch = ExecutionTestSupport.NewDispatch(store);
        var exchange = dispatch.PromoteToRequest(request).PromoteToExchange(response);

        exchange.Close();

        Assert.False(store.TryGet(dispatch.Key, out _));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Closing_an_unpromoted_dispatch_is_a_no_op()
    {
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store);

        dispatch.Close();

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Promoting_the_same_dispatch_twice_is_legal_and_the_later_one_takes_the_slot()
    {
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store);
        var earlier = dispatch.PromoteToRequest(ExecutionTestSupport.NewRequest());
        var later = dispatch.PromoteToRequest(ExecutionTestSupport.NewRequest());

        earlier.Close();

        Assert.True(store.TryGet(dispatch.Key, out var found));
        Assert.Same(later, found);
    }

    [Fact]
    public void A_double_close_is_a_no_op()
    {
        var store = new ContextStore(10);
        var requestContext = ExecutionTestSupport.NewDispatch(store).PromoteToRequest(ExecutionTestSupport.NewRequest());

        requestContext.Close();
        requestContext.Close();

        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Closing_does_not_dispose_the_response()
    {
        var store = new ContextStore(10);
        var request = ExecutionTestSupport.NewRequest();
        using var stream = new DisposeCountingStream(new MemoryStream());
        using var response = ExecutionTestSupport.NewResponse(request, ResponseBody.FromStream(stream));
        var exchange = ExecutionTestSupport.NewDispatch(store).PromoteToRequest(request).PromoteToExchange(response);

        exchange.Close();

        Assert.Equal(0, stream.DisposeCount);
    }

    [Fact]
    public void A_clone_is_value_equal_reference_distinct_and_closing_it_is_a_no_op()
    {
        var store = new ContextStore(10);
        var live = ExecutionTestSupport.NewDispatch(store).PromoteToRequest(ExecutionTestSupport.NewRequest());
        var clone = live with { };

        Assert.Equal(live, clone);
        Assert.NotSame(live, clone);
        clone.Close();

        Assert.True(store.TryGet(live.Key, out var found));
        Assert.Same(live, found);
    }
}
