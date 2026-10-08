// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Resilience;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Resilience;

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

    // ---- CFG-35: the single classifier (XCUT-5, XCUT-6; P5a-21) ----

    [Fact]
    public void IsRetryableStatus_is_exactly_408_429_and_5xx_except_501_and_505()
    {
        var retryable = Enumerable.Range(100, 500).Where(RetryFacts.IsRetryableStatus).ToList();

        Assert.Equal(100, retryable.Count);
        List<int> expected = [408, 429, .. Enumerable.Range(500, 100).Where(c => c is not (501 or 505))];
        expected.Sort();
        Assert.Equal(expected, retryable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    [InlineData(600)]
    [InlineData(int.MaxValue)]
    public void IsRetryableStatus_is_false_outside_the_documented_range(int code)
    {
        Assert.False(RetryFacts.IsRetryableStatus(code));
    }

    [Theory]
    [InlineData(507, 1)]
    [InlineData(511, 1)]
    [InlineData(500, 2)]
    public async Task The_configured_RetryPolicy_set_is_not_the_classifier(int status, int expectedCalls)
    {
        // XCUT-5's closing note keeps the two sets separate; 6a rebuilds RetryPolicy's from XCUT-7's data.
        var request = Request.Get("https://api.example.com/v1/items");
        var transport = new ScriptedTransport(
            TestResponses.Create(Status.FromCode(status), request),
            TestResponses.Create(Status.Ok, request));
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(new InstantTimeProvider())).Build(transport);
        var options = new DexpaceClientOptions { Retry = new RetryOptions { MaxRetryAttempts = 1, BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) } };

        using var response = await pipeline.SendAsync(request, options, TestContext.Current.CancellationToken);

        Assert.True(RetryFacts.IsRetryableStatus(status) || status == 500);
        Assert.Equal(expectedCalls, transport.CallCount);
    }

    public static TheoryData<Exception> IoFamily => new()
    {
        new IOException("io"),
        new SocketException(),
        new TimeoutException("t"),
        new HttpRequestException("h"),
    };

    [Theory]
    [MemberData(nameof(IoFamily))]
    public void IsRetryableCause_accepts_the_IO_family(Exception exception)
    {
        Assert.True(RetryFacts.IsRetryableCause(exception));
    }

    public static TheoryData<Exception> NotRetryable => new()
    {
        new InvalidOperationException("x"),
        new ArgumentException("x"),
        new OperationCanceledException("x"),
        new TaskCanceledException("x"),
        new HttpRequestException("404", null, HttpStatusCode.NotFound),
    };

    [Theory]
    [MemberData(nameof(NotRetryable))]
    public void IsRetryableCause_rejects_everything_else(Exception exception)
    {
        Assert.False(RetryFacts.IsRetryableCause(exception));
    }

    [Fact]
    public void IsRetryableCause_walks_the_cause_chain()
    {
        Assert.True(RetryFacts.IsRetryableCause(new InvalidOperationException("x", new IOException())));
        Assert.True(RetryFacts.IsRetryableCause(new TaskCanceledException("t", new TimeoutException())));
        Assert.True(RetryFacts.IsRetryableCause(new AggregateException(new InvalidOperationException(), new IOException())));
        Assert.True(RetryFacts.IsRetryableCause(Wrap(new IOException(), depth: 10)));

        // ExceptionFacts.EnumerateCauses is bounded at depth 64: a cause 100 deep is not reached.
        Assert.False(RetryFacts.IsRetryableCause(Wrap(new IOException(), depth: 100)));
    }

    [Fact]
    public void IsRetryableCause_terminates_on_a_cycle()
    {
        var a = new InvalidOperationException("a");
        var b = new InvalidOperationException("b", a);
        typeof(Exception).GetField("_innerException", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(a, b);
        Assert.Same(b, a.InnerException);

        Assert.False(RetryFacts.IsRetryableCause(a));
    }

    [Fact]
    public void IsRetryableCause_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => RetryFacts.IsRetryableCause(null!));
    }

    private static Exception Wrap(Exception bottom, int depth)
    {
        var current = bottom;
        for (var i = 0; i < depth; i++)
        {
            current = new InvalidOperationException("wrapper", current);
        }

        return current;
    }
}
