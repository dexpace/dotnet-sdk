// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Resilience;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

#pragma warning disable CA2201 // OutOfMemoryException is the fatal exception under test (design §5.2).

namespace Dexpace.Sdk.Core.Tests.Resilience;

/// <summary>The single failure classifier and the capability (RETRY-1 to RETRY-4, RETRY-23 to RETRY-25, RETRY-37, RECOV-17).</summary>
[Trait("Category", "Unit")]
public sealed class RetryClassifierTests
{
    private static readonly IReadOnlySet<int> s_default = RetryFacts.DefaultRetryableStatusCodes;
    private static readonly FrozenSet<int> s_set501 = new[] { 501 }.ToFrozenSet();
    private static readonly FrozenSet<int> s_set429 = new[] { 429 }.ToFrozenSet();

    private static HttpResponseException Http(int code) =>
        new(TestResponses.Create(Status.FromCode(code)));

    private static bool Classify(Exception failure, IReadOnlySet<int>? set = null) =>
        RetryFacts.IsRetryableFailure(failure, set ?? s_default, TestContext.Current.CancellationToken);

    private static bool ClassifyWith(Exception failure, CancellationToken token) =>
        RetryFacts.IsRetryableFailure(failure, s_default, token);

    [Fact]
    public void Every_status_100_to_599_matches_the_classifier()
    {
        var trueCount = 0;
        for (var code = 100; code <= 599; code++)
        {
            var expected = RetryFacts.IsRetryableStatus(code);
            Assert.Equal(expected, Http(code).IsRetryable);
            trueCount += expected ? 1 : 0;
        }

        Assert.Equal(100, trueCount);
        Assert.False(Http(301).IsRetryable);
        Assert.False(Http(100).IsRetryable);
    }

    [Fact]
    public void The_default_configured_set_is_a_subset_of_the_classifier()
    {
        foreach (var code in RetryFacts.DefaultRetryableStatusCodes)
        {
            Assert.True(RetryFacts.IsRetryableStatus(code));
        }

        Assert.Equal([408, 429, 500, 502, 503, 504], RetryFacts.DefaultRetryableStatusCodes.Order().ToArray());
    }

    [Fact]
    public void A_service_request_or_response_exception_is_always_retryable()
    {
        Assert.True(new ServiceRequestException("x").IsRetryable);
        Assert.True(new ServiceResponseException("x").IsRetryable);
        Assert.True(new ServiceRequestTimeoutException("x").IsRetryable);
        Assert.True(typeof(ServiceRequestException).GetProperty(nameof(IRetryableError.IsRetryable))!.GetMethod!.IsFinal);
        Assert.True(typeof(ServiceResponseException).GetProperty(nameof(IRetryableError.IsRetryable))!.GetMethod!.IsFinal);
        Assert.True(Classify(new ServiceRequestException("x")));
        Assert.True(Classify(new ServiceResponseException("x")));
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
    public void A_raw_IO_family_exception_is_retryable(Exception exception)
    {
        Assert.True(Classify(exception));
    }

    [Fact]
    public void A_plain_SdkException_and_an_unrelated_exception_are_not_retryable()
    {
        Assert.False(Classify(new SdkException("x")));
        Assert.False(Classify(new InvalidOperationException("x")));
        Assert.False(Classify(new HttpRequestException("404", null, System.Net.HttpStatusCode.NotFound)));
    }

    private sealed class Custom(bool retryable) : Exception, IRetryableError
    {
        public bool IsRetryable => retryable;
    }

    [Fact]
    public void A_custom_IRetryableError_that_reports_true_is_retryable_and_one_that_reports_false_is_not()
    {
        Assert.True(Classify(new Custom(true)));
        Assert.False(Classify(new Custom(false)));
        Assert.True(Classify(new InvalidOperationException("w", new Custom(true))));
    }

    [Fact]
    public void A_capability_that_is_false_does_not_veto_an_IO_cause()
    {
        var wrapped = new DeserializationException("reset mid-body", new IOException("reset"));

        Assert.False(wrapped.IsRetryable);
        Assert.True(Classify(wrapped));
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

    [Fact]
    public void The_cause_chain_is_walked_to_depth_64_and_is_cycle_safe()
    {
        Assert.True(Classify(Wrap(new IOException(), 10)));
        Assert.False(Classify(Wrap(new IOException(), 100)));

        var a = new InvalidOperationException("a");
        var b = new InvalidOperationException("b", a);
        typeof(Exception).GetField("_innerException", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(a, b);
        Assert.False(Classify(a));
    }

    [Fact]
    public void An_HttpResponseException_decides_by_the_configured_set_alone()
    {
        var withSet501 = s_set501;
        var only429 = s_set429;

        Assert.False(Http(501).IsRetryable);
        Assert.True(Classify(Http(501), withSet501));
        Assert.False(Classify(Http(503), only429));

        var wrapper = new ServiceResponseException("w", Http(503));
        Assert.True(wrapper.IsRetryable);
        Assert.False(Classify(wrapper, only429));
        Assert.True(Classify(wrapper, s_default));
        Assert.True(Classify(new InvalidOperationException("w", Http(501)), withSet501));
    }

    [Fact]
    public void A_cancelled_call_token_is_never_retryable_whatever_the_exception()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.False(ClassifyWith(new IOException("x"), cts.Token));
        Assert.False(ClassifyWith(new ServiceRequestException("x"), cts.Token));
        Assert.False(ClassifyWith(Http(503), cts.Token));
    }

    [Fact]
    public void A_TaskCanceledException_over_a_TimeoutException_with_an_unsignalled_token_is_retryable()
    {
        Assert.True(Classify(new TaskCanceledException("t", new TimeoutException())));
    }

    [Fact]
    public void A_TaskCanceledException_with_no_TimeoutException_is_not()
    {
        Assert.False(Classify(new TaskCanceledException("caller")));
        Assert.False(Classify(new OperationCanceledException("caller")));
    }

    [Fact]
    public void ThreadInterruptedException_is_not_retryable()
    {
        Assert.False(Classify(new ThreadInterruptedException()));
    }

    [Fact]
    public void A_fatal_exception_is_never_classified()
    {
        Assert.False(Classify(new OutOfMemoryException()));
    }

    [Fact]
    public void IsRetryableCause_still_passes_its_5a_cases_and_accepts_a_custom_IRetryableError()
    {
        Assert.True(RetryFacts.IsRetryableCause(new Custom(true)));
        Assert.False(RetryFacts.IsRetryableCause(new Custom(false)));
        Assert.True(RetryFacts.IsRetryableCause(new IOException()));
    }

    [Fact]
    public void IsRetryable_is_computed_once_in_the_constructor()
    {
        var field = typeof(HttpResponseException).GetField("_isRetryable", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        Assert.True(field!.IsInitOnly);

        var exception = Http(503);
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(exception.IsRetryable);
        }
    }

    [Fact]
    public void The_overrides_are_sealed()
    {
        foreach (var type in new[] { typeof(ServiceRequestException), typeof(ServiceResponseException), typeof(HttpResponseException) })
        {
            var getter = type.GetProperty(nameof(IRetryableError.IsRetryable))!.GetMethod!;
            Assert.True(getter.IsFinal, type.Name);
            Assert.Equal(type, getter.DeclaringType);
        }
    }

    [Fact]
    public void SdkException_is_not_retryable_by_default_and_the_interface_is_implemented()
    {
        SdkException error = new("x");
        Assert.False(((IRetryableError)error).IsRetryable);
    }
}
