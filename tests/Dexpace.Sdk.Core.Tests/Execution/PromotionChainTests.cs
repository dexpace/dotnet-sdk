// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/context/context.test.ts; Ruby cases (R2 CTX-9, R4 measurement, CTX-19) from ruby-sdk@90075b1's 4a design.
// Ruby's gem tests are not in the local clone.

using System.Reflection;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Execution;

/// <summary>CTX-1, CTX-2, CTX-5, CTX-6, CTX-16: the promotion chain.</summary>
[Trait("Category", "Unit")]
public sealed class PromotionChainTests
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    private static string[] PublicProperties(Type type) =>
        [.. type.GetProperties(PublicInstance).Select(p => p.Name).Where(n => n != "EqualityContract").Order(StringComparer.Ordinal)];

    [Fact]
    public void Each_stage_exposes_exactly_its_artifacts()
    {
        Assert.Equal(["Instrumentation", "Key"], PublicProperties(typeof(DispatchContext)));
        Assert.Equal(["Instrumentation", "Key", "OperationName", "Request"], PublicProperties(typeof(RequestContext)));
        Assert.Equal(["Instrumentation", "Key", "OperationName", "Request", "Response"], PublicProperties(typeof(ExchangeContext)));
    }

    [Fact]
    public void Promoting_dispatch_adds_exactly_the_request_and_carries_key_and_bundle_by_reference()
    {
        var store = new ContextStore(10);
        var bundle = ExecutionTestSupport.NewBundle();
        var dispatch = ExecutionTestSupport.NewDispatch(store, bundle: bundle);
        var request = ExecutionTestSupport.NewRequest();

        var next = dispatch.PromoteToRequest(request, "ListWidgets");

        Assert.Same(bundle, next.Instrumentation);
        Assert.Equal(dispatch.Key, next.Key);
        Assert.Same(request, next.Request);
    }

    [Fact]
    public void Promoting_request_to_exchange_carries_request_and_operation_name_by_reference()
    {
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store, bundle: ExecutionTestSupport.NewBundle());
        var request = ExecutionTestSupport.NewRequest();
        var requestContext = dispatch.PromoteToRequest(request, "ListWidgets");
        using var response = ExecutionTestSupport.NewResponse(request);

        var exchange = requestContext.PromoteToExchange(response);

        Assert.Same(requestContext.Instrumentation, exchange.Instrumentation);
        Assert.Equal(requestContext.Key, exchange.Key);
        Assert.Same(request, exchange.Request);
        Assert.Same(response, exchange.Response);
        Assert.Equal("ListWidgets", exchange.OperationName);
    }

    [Fact]
    public void The_source_is_unchanged_by_promotion()
    {
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store);
        var before = dispatch with { };

        _ = dispatch.PromoteToRequest(ExecutionTestSupport.NewRequest());

        Assert.Equal(before, dispatch);
    }

    [Fact]
    public void Default_construction_binds_None_and_mints_a_fresh_key()
    {
        var a = new DispatchContext();
        var b = new DispatchContext();

        Assert.Same(InstrumentationContext.None, a.Instrumentation);
        Assert.NotEqual(a.Key, b.Key);
        Assert.NotEqual(default, a.Key);
    }

    [Fact]
    public void Two_default_constructed_contexts_with_identical_fields_are_not_equal()
    {
        var store = new ContextStore(10);
        var request = ExecutionTestSupport.NewRequest();

        Assert.NotEqual(new RequestContext(request, "op", null, null, store), new RequestContext(request, "op", null, null, store));
    }

    [Fact]
    public void A_pinned_shared_key_makes_them_equal()
    {
        var store = new ContextStore(10);
        var request = ExecutionTestSupport.NewRequest();
        var key = CallKey.Next();

        Assert.Equal(new RequestContext(request, "op", null, key, store), new RequestContext(request, "op", null, key, store));
    }

    [Fact]
    public void The_default_key_is_rejected()
    {
        using var response = ExecutionTestSupport.NewResponse();
        var request = ExecutionTestSupport.NewRequest();

        Assert.Equal("key", Assert.Throws<ArgumentException>(() => new DispatchContext(null, default(CallKey))).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentException>(() => new RequestContext(request, null, null, default(CallKey))).ParamName);
        Assert.Equal("key", Assert.Throws<ArgumentException>(() => new ExchangeContext(request, response, null, null, default(CallKey))).ParamName);
    }

    [Fact]
    public void Default_keys_are_distinct_across_all_three_flavours()
    {
        using var response = ExecutionTestSupport.NewResponse();
        var request = ExecutionTestSupport.NewRequest();

        var keys = new[]
        {
            new DispatchContext().Key,
            new RequestContext(request).Key,
            new ExchangeContext(request, response).Key,
        };

        Assert.Equal(3, keys.Distinct().Count());
    }

    [Fact]
    public void The_operation_name_is_absent_at_dispatch()
    {
        Assert.Null(typeof(DispatchContext).GetProperty("OperationName", PublicInstance));
        var next = ExecutionTestSupport.NewDispatch(new ContextStore(10)).PromoteToRequest(ExecutionTestSupport.NewRequest(), "Op");
        Assert.Equal("Op", next.OperationName);
    }

    [Fact]
    public void The_operation_name_is_carried_forward_unchanged()
    {
        using var response = ExecutionTestSupport.NewResponse();
        var request = ExecutionTestSupport.NewRequest();
        var exchange = new RequestContext(request, "Op", null, null, new ContextStore(10)).PromoteToExchange(response);

        Assert.Equal("Op", exchange.OperationName);
    }

    [Fact]
    public void The_operation_name_does_not_change_the_key()
    {
        var dispatch = ExecutionTestSupport.NewDispatch(new ContextStore(10));

        Assert.Equal(dispatch.Key, dispatch.PromoteToRequest(ExecutionTestSupport.NewRequest(), "Op").Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void A_blank_operation_name_is_rejected(string name)
    {
        var dispatch = ExecutionTestSupport.NewDispatch(new ContextStore(10));
        using var response = ExecutionTestSupport.NewResponse();

        Assert.Throws<ArgumentException>(() => dispatch.PromoteToRequest(ExecutionTestSupport.NewRequest(), name));
        Assert.Throws<ArgumentException>(() => new ExchangeContext(ExecutionTestSupport.NewRequest(), response, name));
        Assert.Null(dispatch.PromoteToRequest(ExecutionTestSupport.NewRequest(), null).OperationName);
    }

    [Fact]
    public void A_null_request_or_response_is_rejected()
    {
        var dispatch = ExecutionTestSupport.NewDispatch(new ContextStore(10));
        var requestContext = dispatch.PromoteToRequest(ExecutionTestSupport.NewRequest());

        Assert.Throws<ArgumentNullException>(() => dispatch.PromoteToRequest(null!));
        Assert.Throws<ArgumentNullException>(() => requestContext.PromoteToExchange(null!));
        Assert.Throws<ArgumentNullException>(() => new ExchangeContext(null!, ExecutionTestSupport.NewResponse()));
    }

    [Fact]
    public void ToString_names_the_stage_key_operation_and_status_and_never_prints_headers_or_bodies()
    {
        var request = new Request(Method.Get, new Uri("https://api.example.test/widgets?token=sekrit-query"))
            .WithHeader("Authorization", "Bearer sekrit-header");
        using var body = new StrictReadStream(new MemoryStream("payload"u8.ToArray()));
        using var response = ExecutionTestSupport.NewResponse(request, ResponseBody.FromStream(body));
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store);
        var exchange = dispatch.PromoteToRequest(request, "ListWidgets").PromoteToExchange(response);

        var text = exchange.ToString();

        Assert.Contains("Exchange", text, StringComparison.Ordinal);
        Assert.Contains(dispatch.Key.ToString(), text, StringComparison.Ordinal);
        Assert.Contains("ListWidgets", text, StringComparison.Ordinal);
        Assert.Contains("200", text, StringComparison.Ordinal);
        Assert.DoesNotContain("sekrit", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", text, StringComparison.Ordinal);
        Assert.False(body.AnyRead);
    }

    [Fact]
    public void The_ToString_is_sealed()
    {
        var method = typeof(CallContext).GetMethod(nameof(ToString), PublicInstance, Type.EmptyTypes);

        Assert.NotNull(method);
        Assert.True(method.IsFinal);
    }
}
