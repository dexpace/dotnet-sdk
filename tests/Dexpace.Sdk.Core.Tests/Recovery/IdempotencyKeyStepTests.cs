// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/recovery/idempotency-key.test.ts.
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Recovery;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class IdempotencyKeyStepTests
{
    private static Request Of(Method method) => Request.Create(method, "https://example.test/items");

    private static readonly Method[] s_wellKnown =
    [
        Method.Get, Method.Head, Method.Post, Method.Put, Method.Patch, Method.Delete, Method.Options, Method.Trace, Method.Connect,
    ];

    public static TheoryData<string, bool> AllMethods()
    {
        var data = new TheoryData<string, bool>();
        foreach (var method in s_wellKnown)
        {
            data.Add(method.Name, true);
            data.Add(method.Name, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllMethods))]
    public async Task The_default_methods_are_POST_PUT_and_PATCH(string name, bool async)
    {
        var request = Of(Method.Of(name));

        var result = await Run.Apply(new IdempotencyKeyStep(), request, async);

        if (name is "POST" or "PUT" or "PATCH")
        {
            Assert.NotNull(result.Headers.Get("Idempotency-Key"));
        }
        else
        {
            Assert.Same(request, result);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_default_header_is_Idempotency_Key_and_the_default_key_is_a_GUID(bool async)
    {
        var result = await Run.Apply(new IdempotencyKeyStep(), Of(Method.Post), async);

        var key = result.Headers.Get(HttpHeaderName.WellKnown.IdempotencyKey);
        Assert.True(Guid.TryParseExact(key, "D", out _), "Expected a GUID in the D format, got: " + key);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RespectExisting_true_leaves_a_present_header_untouched_and_does_not_invoke_the_strategy(bool async)
    {
        var calls = 0;
        var step = new IdempotencyKeyStep { KeyStrategy = () => "k" + Interlocked.Increment(ref calls) };
        var request = Of(Method.Post).WithHeader("Idempotency-Key", "caller");

        var result = await Run.Apply(step, request, async);

        Assert.Same(request, result);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RespectExisting_false_overwrites_a_present_header(bool async)
    {
        var step = new IdempotencyKeyStep { RespectExisting = false, KeyStrategy = () => "fresh" };
        var request = Of(Method.Post).WithHeader("Idempotency-Key", "caller");

        var result = await Run.Apply(step, request, async);

        Assert.Equal("fresh", Assert.Single(result.Headers.GetAll("Idempotency-Key")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_strategy_runs_at_most_once_per_applicable_request(bool async)
    {
        var calls = 0;
        var step = new IdempotencyKeyStep { KeyStrategy = () => "k" + Interlocked.Increment(ref calls) };

        await Run.Apply(step, Of(Method.Post), async);
        Assert.Equal(1, calls);

        await Run.Apply(step, Of(Method.Get), async);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("", false)]
    [InlineData("   ", true)]
    [InlineData("   ", false)]
    public async Task A_strategy_returning_null_empty_or_whitespace_throws_InvalidOperationException_naming_the_step(string? key, bool async)
    {
        var step = new IdempotencyKeyStep { KeyStrategy = () => key! };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Run.Apply(step, Of(Method.Post), async));

        Assert.Contains(nameof(IdempotencyKeyStep), thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_strategy_value_failing_header_validation_throws_the_ArgumentException_Headers_raises(bool async)
    {
        var step = new IdempotencyKeyStep { KeyStrategy = () => "bad\r\nvalue" };

        await Assert.ThrowsAsync<ArgumentException>(() => Run.Apply(step, Of(Method.Post), async));
    }

    [Fact]
    public void Methods_are_copied_on_init()
    {
        var source = new HashSet<Method> { Method.Delete };
        var step = new IdempotencyKeyStep { Methods = source };

        source.Add(Method.Get);
        source.Remove(Method.Delete);

        Assert.Contains(Method.Delete, step.Methods);
        Assert.DoesNotContain(Method.Get, step.Methods);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_custom_header_name_and_method_set_are_honoured(bool async)
    {
        var step = new IdempotencyKeyStep
        {
            HeaderName = HttpHeaderName.Of("X-Request-Key"),
            Methods = new HashSet<Method> { Method.Delete },
            KeyStrategy = () => "custom",
        };

        var deleted = await Run.Apply(step, Of(Method.Delete), async);
        var posted = Of(Method.Post);

        Assert.Equal("custom", deleted.Headers.Get("X-Request-Key"));
        Assert.Null(deleted.Headers.Get("Idempotency-Key"));
        Assert.Same(posted, await Run.Apply(step, posted, async));
    }

    [Fact]
    public void The_internal_key_source_overload_uses_the_given_source_and_not_the_strategy()
    {
        var strategyCalls = 0;
        var step = new IdempotencyKeyStep { KeyStrategy = () => "strategy" + Interlocked.Increment(ref strategyCalls) };

        var result = step.Apply(Of(Method.Post), () => "from-source");

        Assert.Equal("from-source", result.Headers.Get("Idempotency-Key"));
        Assert.Equal(0, strategyCalls);
    }
}
