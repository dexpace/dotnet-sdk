// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-4, -8, -20, XCUT-1, XCUT-2 (phase 8b, P8b-7; design §C): the one classification of a native failure, driven here
/// with the shapes the runtime produces (measured on .NET 10.0.401, facts G4 and F3): <c>CancelPendingRequests</c> and a
/// disposal give <see cref="TaskCanceledException"/> over an <see cref="IOException"/>, <c>HttpClient.Timeout</c> over a
/// <see cref="TimeoutException"/>. The pipeline-level consequences are in <c>FailureClassificationTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SendFailuresTests
{
    private static TaskCanceledException CancelledByTheClient() => new("cancelled", new IOException("The request was canceled due to the configured HttpClient.Timeout..."));

    private static TaskCanceledException CancelledByTheNativeTimeout() => new("timed out", new TimeoutException("The operation was canceled."));

    private static CancellationToken Signalled()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        return source.Token;
    }

    [Fact]
    public void The_filter_matches_exactly_cancellation_and_native_request_failures()
    {
        Assert.True(SendFailures.IsTransportFailure(new TaskCanceledException()));
        Assert.True(SendFailures.IsTransportFailure(new OperationCanceledException()));
        Assert.True(SendFailures.IsTransportFailure(new HttpRequestException("x")));
        Assert.False(SendFailures.IsTransportFailure(new ObjectDisposedException("x")));
        Assert.False(SendFailures.IsTransportFailure(new NotSupportedException()));
        Assert.False(SendFailures.IsTransportFailure(new InvalidOperationException()));
        Assert.False(SendFailures.IsTransportFailure(new StreamConsumedException()));
        Assert.False(SendFailures.IsTransportFailure(new SdkException("x")));
    }

    [Fact]
    public void Row_1_a_cancellation_that_already_carries_the_callers_token_is_returned_as_it_is()
    {
        var caller = Signalled();
        var native = new TaskCanceledException("cancelled", innerException: null, caller);

        var mapped = SendFailures.Map(native, deadlineFired: false, caller);

        Assert.Same(native, mapped);
    }

    [Fact]
    public void Row_1_a_cancellation_that_carries_a_linked_token_becomes_one_that_carries_the_callers()
    {
        // With a deadline armed the native client sees a token linked to the caller's, and its exception carries that one.
        var caller = Signalled();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(caller);
        var native = new TaskCanceledException("cancelled", new IOException("x"), linked.Token);

        var mapped = SendFailures.Map(native, deadlineFired: false, caller);

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(mapped);
        Assert.NotSame(native, cancelled);
        Assert.Equal(caller, cancelled.CancellationToken);
        Assert.Null(cancelled.InnerException);
        Assert.Same(native, Assert.Single(ExceptionTrail.GetSuppressed(cancelled)));
    }

    [Fact]
    public void Row_1_wins_even_when_the_deadline_also_fired()
    {
        var caller = Signalled();
        var native = new TaskCanceledException("cancelled", innerException: null, caller);

        var mapped = SendFailures.Map(native, deadlineFired: true, caller);

        Assert.Same(native, mapped);
        Assert.IsNotType<ServiceRequestTimeoutException>(mapped);
    }

    [Fact]
    public void Row_1_a_request_failure_while_the_caller_was_cancelling_is_a_cancellation_not_a_retryable_failure()
    {
        var native = new HttpRequestException("aborted", new IOException("connection aborted"));
        var caller = Signalled();

        var mapped = SendFailures.Map(native, deadlineFired: false, caller);

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(mapped);
        Assert.Equal(caller, cancelled.CancellationToken);
        Assert.Null(cancelled.InnerException);
        Assert.Same(native, Assert.Single(ExceptionTrail.GetSuppressed(cancelled)));
    }

    [Theory]
    [InlineData("cancellation")]
    [InlineData("request")]
    public void Row_2_a_fired_deadline_is_a_retryable_timeout_with_the_native_exception_as_its_cause(string shape)
    {
        Exception native = shape == "cancellation" ? new TaskCanceledException("cancelled") : new HttpRequestException("aborted", new IOException("x"));

        var mapped = SendFailures.Map(native, deadlineFired: true, CancellationToken.None);

        var timeout = Assert.IsType<ServiceRequestTimeoutException>(mapped);
        Assert.True(timeout.IsRetryable);
        Assert.Same(native, timeout.InnerException);
    }

    [Fact]
    public void Row_3_a_native_clients_own_timeout_is_a_retryable_timeout()
    {
        var native = CancelledByTheNativeTimeout();

        var mapped = SendFailures.Map(native, deadlineFired: false, CancellationToken.None);

        var timeout = Assert.IsType<ServiceRequestTimeoutException>(mapped);
        Assert.True(timeout.IsRetryable);
        Assert.Same(native, timeout.InnerException);
    }

    [Fact]
    public void Row_4_an_internal_cancel_is_a_new_terminal_cancellation_with_the_native_exception_on_its_trail_only()
    {
        var native = CancelledByTheClient();

        var mapped = SendFailures.Map(native, deadlineFired: false, CancellationToken.None);

        var cancelled = Assert.IsType<OperationCanceledException>(mapped);
        Assert.NotSame(native, cancelled);
        Assert.Equal(CancellationToken.None, cancelled.CancellationToken);
        Assert.Contains("not a timeout", cancelled.Message, StringComparison.Ordinal);
        Assert.Null(cancelled.InnerException);
        Assert.Same(native, Assert.Single(ExceptionTrail.GetSuppressed(cancelled)));
    }

    [Fact]
    public void Row_4_a_plain_operation_cancellation_with_no_cause_is_terminal_too()
    {
        var native = new OperationCanceledException("cancelled by a handler");

        var mapped = SendFailures.Map(native, deadlineFired: false, CancellationToken.None);

        var cancelled = Assert.IsType<OperationCanceledException>(mapped);
        Assert.NotSame(native, cancelled);
        Assert.Null(cancelled.InnerException);
        Assert.Same(native, Assert.Single(ExceptionTrail.GetSuppressed(cancelled)));
    }

    [Fact]
    public void Row_4_is_checked_after_the_timeout_rows()
    {
        // A deadline that fired wins over the internal-cancel reading of the same exception shape.
        var mapped = SendFailures.Map(CancelledByTheClient(), deadlineFired: true, CancellationToken.None);

        Assert.IsType<ServiceRequestTimeoutException>(mapped);
    }

    [Fact]
    public void Row_5_a_request_failure_is_a_retryable_transport_failure_with_the_native_exception_as_its_cause()
    {
        var native = new HttpRequestException("refused", new System.Net.Sockets.SocketException());

        var mapped = SendFailures.Map(native, deadlineFired: false, CancellationToken.None);

        var failure = Assert.IsType<ServiceRequestException>(mapped);
        Assert.True(failure.IsRetryable);
        Assert.Same(native, failure.InnerException);
    }

    [Fact]
    public void Row_5_a_refused_resend_of_a_single_use_body_keeps_its_cause_chain_for_the_message()
    {
        var consumed = new StreamConsumedException("The transport re-sent a single-use request body larger than ResendBufferLimit.");
        var native = new HttpRequestException("failed", new IOException("re-send refused", consumed));

        var mapped = SendFailures.Map(native, deadlineFired: false, CancellationToken.None);

        Assert.IsType<ServiceRequestException>(mapped);
        Assert.Same(consumed, native.InnerException!.InnerException);
    }

    [Fact]
    public void No_message_carries_a_url_or_a_header_value()
    {
        var native = new HttpRequestException("failed to reach https://secret.test/path?token=abc with Authorization: Bearer xyz");

        foreach (var mapped in new[]
        {
            SendFailures.Map(native, deadlineFired: false, CancellationToken.None),
            SendFailures.Map(native, deadlineFired: true, CancellationToken.None),
            SendFailures.Map(CancelledByTheClient(), deadlineFired: false, CancellationToken.None),
            SendFailures.Map(CancelledByTheNativeTimeout(), deadlineFired: false, CancellationToken.None),
        })
        {
            Assert.DoesNotContain("secret.test", mapped.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("Bearer", mapped.Message, StringComparison.Ordinal);
        }
    }
}
