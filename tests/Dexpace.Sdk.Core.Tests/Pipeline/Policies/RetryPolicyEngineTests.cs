// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Resilience;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

#pragma warning disable CA2201 // OutOfMemoryException is the fatal exception under test (design §5.2).
#pragma warning disable CA2000 // ScriptedTransport and the responses it hands out own nothing that needs disposing in a test.

/// <summary>
/// The stage policy over the shared engine (RETRY-8, RETRY-21, RETRY-28 to RETRY-35, RETRY-38 to RETRY-44; PIPE-16, PIPE-36,
/// PIPE-40).
/// </summary>
[Trait("Category", "Unit")]
public sealed class RetryPolicyEngineTests
{
    private static Request Get() => Request.Get("https://api.example.com/v1/items");

    private static DexpaceClientOptions Options(
        int retries = 2,
        bool honor = true,
        TimeSpan? fixedDelay = null,
        string? attemptHeader = null,
        TimeSpan? attemptTimeout = null) => new()
        {
            AttemptTimeout = attemptTimeout,
            Retry = new RetryOptions
            {
                MaxRetryAttempts = retries,
                BaseDelay = TimeSpan.Zero,
                Jitter = 0,
                HonorRetryAfter = honor,
                FixedDelay = fixedDelay,
                AttemptHeaderName = attemptHeader,
            },
        };

    private static HttpPipeline PipelineOver(ScriptedTransport transport, RetryPolicy? policy = null, TimeProvider? clock = null) =>
        new PipelineBuilder().Add(policy ?? new RetryPolicy(clock ?? new InstantTimeProvider())).Build(transport);

    private static Response Unavailable(ResponseBody? body = null, Headers? headers = null) =>
        TestResponses.Create(Status.ServiceUnavailable, headers: headers, body: body);

    [Fact]
    public async Task A_503_then_200_returns_the_200_and_disposes_the_503()
    {
        var log = new List<string>();
        using var body503 = new TrackingResponseBody(log, "r503");
        using var body200 = new TrackingResponseBody(log, "r200");
        var transport = new ScriptedTransport(Unavailable(body503), TestResponses.Create(Status.Ok, body: body200));

        using var response = await PipelineOver(transport).SendAsync(Get(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(1, body503.DisposeCount);
        Assert.Equal(0, body200.DisposeCount);
    }

    [Fact]
    public async Task A_503_after_the_cap_is_returned_live_and_unread()
    {
        var log = new List<string>();
        using var last = new TrackingResponseBody(log, "last");
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), Unavailable(last));

        using var response = await PipelineOver(transport).SendAsync(Get(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.ServiceUnavailable, response.Status);
        Assert.Equal(3, transport.CallCount);
        Assert.Equal(0, last.OpenCount);
        Assert.Equal(0, last.DisposeCount);
    }

    [Fact]
    public async Task A_discarded_503_body_is_drained_into_an_HttpResponseException_trail_entry()
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log, "retried");
        var final = new ServiceRequestException("down");
        var transport = new ScriptedTransport(Unavailable(body), final);

        var thrown = await Assert.ThrowsAsync<ServiceRequestException>(
            async () => await PipelineOver(transport).SendAsync(Get(), Options(retries: 1), TestContext.Current.CancellationToken));

        Assert.Same(final, thrown);
        var entry = Assert.IsType<HttpResponseException>(Assert.Single(thrown.Suppressed));
        Assert.Equal(Status.ServiceUnavailable, entry.Status);
        Assert.Equal(1, body.OpenCount);
        Assert.Equal(1, body.DisposeCount);
    }

    /// <summary>The documented RETRY-29 recipe: a server header flips the condition, nothing else.</summary>
    private sealed class HeaderRetryPolicy(TimeProvider clock) : RetryPolicy(clock)
    {
        protected override bool? ShouldRetry(RetryAttemptContext attempt) =>
            attempt.Response?.Headers.Get("X-Should-Retry") switch
            {
                "true" or "1" or "yes" or "retry" => true,
                "false" or "0" or "no" or "stop" => false,
                _ => null,
            };
    }

    private static Response WithShouldRetry(Status status, string? value) =>
        TestResponses.Create(status, headers: value is null ? null : new Headers.Builder().Set("X-Should-Retry", value).Build());

    [Theory]
    [InlineData(200, "yes", 3)]
    [InlineData(200, "retry", 3)]
    [InlineData(200, "1", 3)]
    [InlineData(200, "true", 3)]
    [InlineData(503, "no", 1)]
    [InlineData(503, "stop", 1)]
    [InlineData(503, "0", 1)]
    [InlineData(503, "false", 1)]
    [InlineData(503, "maybe", 3)]
    [InlineData(404, null, 1)]
    public async Task ShouldRetry_true_false_null_and_the_cap_still_applies(int status, string? header, int expectedSends)
    {
        var script = Enumerable.Range(0, 10).Select(_ => (object)WithShouldRetry(Status.FromCode(status), header)).ToArray();
        var transport = new ScriptedTransport(script);

        using var response = await PipelineOver(transport, new HeaderRetryPolicy(new InstantTimeProvider()))
            .SendAsync(Get(), Options(), TestContext.Current.CancellationToken);

        Assert.Equal(expectedSends, transport.CallCount);
    }

    [Fact]
    public async Task ShouldRetry_cannot_override_the_resend_gate()
    {
        var transport = new ScriptedTransport(WithShouldRetry(Status.ServiceUnavailable, "yes"), TestResponses.Create(Status.Ok));
        var pipeline = PipelineOver(transport, new HeaderRetryPolicy(new InstantTimeProvider()));

        using var response = await pipeline.SendAsync(
            new Request(Method.Post, new Uri("https://api.example.com/v1/items")),
            Options(),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, transport.CallCount);
    }

    private sealed class ThrowingShouldRetryPolicy(Exception failure) : RetryPolicy(new InstantTimeProvider())
    {
        public int Asked { get; private set; }

        protected override bool? ShouldRetry(RetryAttemptContext attempt)
        {
            Asked++;
            throw failure;
        }
    }

    [Fact]
    public async Task ShouldRetry_throwing_aborts_with_InvalidOperationException_and_suppressed_failure()
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log, "r");
        var hookFailure = new FormatException("hook");
        var policy = new ThrowingShouldRetryPolicy(hookFailure);

        var transport503 = new ScriptedTransport(Unavailable(body));
        var thrown503 = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PipelineOver(transport503, policy).SendAsync(Get(), Options(), TestContext.Current.CancellationToken));
        Assert.Same(hookFailure, thrown503.InnerException);
        Assert.Equal(1, body.DisposeCount);

        var attemptFailure = new ServiceRequestException("attempt");
        var transportFailure = new ScriptedTransport(attemptFailure);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PipelineOver(transportFailure, policy).SendAsync(Get(), Options(), TestContext.Current.CancellationToken));
        Assert.Same(hookFailure, thrown.InnerException);
        Assert.Equal([attemptFailure], ExceptionTrail.GetSuppressed(thrown));

        // A fatal exception from the hook passes unchanged.
        var fatal = new OutOfMemoryException();
        var fatalPolicy = new ThrowingShouldRetryPolicy(fatal);
        var thrownFatal = await Assert.ThrowsAsync<OutOfMemoryException>(
            async () => await PipelineOver(new ScriptedTransport(Unavailable()), fatalPolicy).SendAsync(Get(), Options(), TestContext.Current.CancellationToken));
        Assert.Same(fatal, thrownFatal);
    }

    [Fact]
    public async Task ShouldRetry_is_never_asked_about_a_cancelled_call_or_a_fatal_exception()
    {
        var policy = new ThrowingShouldRetryPolicy(new FormatException("hook"));
        using var cts = new CancellationTokenSource();
        var transport = new ScriptedTransport(new object[]
        {
            new Func<Response>(() =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            }),
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await PipelineOver(transport, policy).SendAsync(Get(), Options(), cts.Token));

        var fatal = new OutOfMemoryException();
        await Assert.ThrowsAsync<OutOfMemoryException>(
            async () => await PipelineOver(new ScriptedTransport(fatal), policy).SendAsync(Get(), Options(), TestContext.Current.CancellationToken));

        Assert.Equal(0, policy.Asked);
    }

    private sealed class OverridePolicy(TimeProvider clock, TimeSpan? value, Exception? failure = null) : RetryPolicy(clock)
    {
        protected override TimeSpan? GetDelayOverride(RetryAttemptContext attempt) => failure is null ? value : throw failure;
    }

    [Fact]
    public async Task GetDelayOverride_wins_over_everything()
    {
        var clock = new RecordingFakeTimeProvider();
        var hinted = new Headers.Builder().Set("Retry-After", "30").Build();
        var transport = new ScriptedTransport(Unavailable(headers: hinted), TestResponses.Create(Status.Ok));
        var pipeline = PipelineOver(transport, new OverridePolicy(clock, TimeSpan.FromSeconds(7)));

        var task = pipeline.SendAsync(Get(), Options(fixedDelay: TimeSpan.FromSeconds(2)), TestContext.Current.CancellationToken).AsTask();
        using var response = await EngineHarness.DriveAsync(task, clock);

        Assert.Equal([TimeSpan.FromSeconds(7)], clock.Timers);
    }

    [Fact]
    public async Task A_throwing_delay_override_logs_event_140_once_and_falls_back()
    {
        var logger = new RecordingLogger();
        var clock = new RecordingFakeTimeProvider();
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder()
            .Add(new InstrumentationPolicy(logger))
            .Add(new OverridePolicy(clock, null, new FormatException("override")))
            .Build(transport);

        var task = pipeline.SendAsync(Get(), Options(fixedDelay: TimeSpan.FromSeconds(2)), TestContext.Current.CancellationToken).AsTask();
        using var response = await EngineHarness.DriveAsync(task, clock);

        Assert.Equal([TimeSpan.FromSeconds(2)], clock.Timers);
        var entry = Assert.Single(logger.Entries, e => e.EventId.Id == DexpaceLogEvents.RetryDelayOverrideFailedId);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(DexpaceLogEvents.RetryDelayOverrideFailed, entry.EventId.Name);
        Assert.IsType<FormatException>(entry.Exception);
    }

    [Fact]
    public async Task A_logger_that_throws_while_reporting_a_failed_override_does_not_mask_the_call()
    {
        var clock = new RecordingFakeTimeProvider();
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder()
            .Add(new InstrumentationPolicy(new ThrowingLogger()))
            .Add(new OverridePolicy(clock, TimeSpan.FromSeconds(-1)))
            .Build(transport);

        var task = pipeline.SendAsync(Get(), Options(fixedDelay: TimeSpan.FromSeconds(1)), TestContext.Current.CancellationToken).AsTask();
        using var response = await EngineHarness.DriveAsync(task, clock);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal([TimeSpan.FromSeconds(1)], clock.Timers);
    }

    [Fact]
    public async Task FixedDelay_replaces_the_backoff_and_still_sits_below_the_pacing_hint()
    {
        var clock = new RecordingFakeTimeProvider();
        var hinted = new Headers.Builder().Set("Retry-After", "5").Build();
        var transport = new ScriptedTransport(Unavailable(headers: hinted), Unavailable(), TestResponses.Create(Status.Ok));
        var pipeline = PipelineOver(transport, new RetryPolicy(clock));

        var task = pipeline.SendAsync(Get(), Options(fixedDelay: TimeSpan.FromSeconds(2)), TestContext.Current.CancellationToken).AsTask();
        using var response = await EngineHarness.DriveAsync(task, clock);

        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2)], clock.Timers);
    }

    [Theory]
    [InlineData("Retry-After", "30")]
    [InlineData("retry-after-ms", "30000")]
    [InlineData("x-ms-retry-after-ms", "30000")]
    [InlineData("X-RateLimit-Reset", "99999999999")]
    public async Task HonorRetryAfter_false_ignores_all_four_headers(string name, string value)
    {
        var clock = new RecordingFakeTimeProvider();
        var hinted = new Headers.Builder().Set(name, value).Build();
        var transport = new ScriptedTransport(Unavailable(headers: hinted), TestResponses.Create(Status.Ok));
        var pipeline = PipelineOver(transport, new RetryPolicy(clock));

        using var response = await pipeline.SendAsync(Get(), Options(honor: false), TestContext.Current.CancellationToken);

        // The base delay is zero and there is no jitter: no timer is armed.
        Assert.Empty(clock.Timers);
        Assert.Equal(2, transport.CallCount);
    }

    [Theory]
    [InlineData("Retry-After", "30", 30_000)]
    [InlineData("retry-after-ms", "1500", 1_500)]
    [InlineData("x-ms-retry-after-ms", "2500", 2_500)]
    public async Task Each_pacing_header_is_honoured_when_the_switch_is_on(string name, string value, int expectedMilliseconds)
    {
        var clock = new RecordingFakeTimeProvider();
        var hinted = new Headers.Builder().Set(name, value).Build();
        var transport = new ScriptedTransport(Unavailable(headers: hinted), TestResponses.Create(Status.Ok));

        var task = PipelineOver(transport, new RetryPolicy(clock)).SendAsync(Get(), Options(), TestContext.Current.CancellationToken).AsTask();
        using var response = await EngineHarness.DriveAsync(task, clock);

        Assert.Equal([TimeSpan.FromMilliseconds(expectedMilliseconds)], clock.Timers);
    }

    [Fact]
    public async Task The_attempt_header_stamps_a_one_based_ordinal_on_a_per_attempt_copy()
    {
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));
        var original = Get().WithHeader("Idempotency-Key", "k-1");
        var pipeline = PipelineOver(transport);

        using var response = await pipeline.SendAsync(original, Options(attemptHeader: "X-Attempt"), TestContext.Current.CancellationToken);

        Assert.Equal(["1", "2", "3"], transport.Requests.Select(r => r.Headers.Get("X-Attempt")));
        Assert.All(transport.Requests, r => Assert.Equal("k-1", r.Headers.Get("Idempotency-Key")));
        Assert.Null(original.Headers.Get("X-Attempt"));
    }

    [Fact]
    public async Task A_disabled_attempt_header_adds_no_header()
    {
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));

        using var response = await PipelineOver(transport).SendAsync(Get(), Options(), TestContext.Current.CancellationToken);

        Assert.All(transport.Requests, r => Assert.Null(r.Headers.Get("X-Attempt")));
    }

    [Fact]
    public async Task MaxRetries_on_RequestOptions_wins_and_zero_means_no_retries()
    {
        var raised = new ScriptedTransport(Unavailable(), Unavailable(), Unavailable(), Unavailable(), Unavailable());
        using var more = await PipelineOver(raised).SendAsync(Get(), new RequestOptions { MaxRetries = 4 }, TestContext.Current.CancellationToken);
        Assert.Equal(5, raised.CallCount);

        var none = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));
        using var once = await PipelineOver(none).SendAsync(Get(), new RequestOptions { MaxRetries = 0 }, TestContext.Current.CancellationToken);
        Assert.Equal(1, none.CallCount);
        Assert.Equal(Status.ServiceUnavailable, once.Status);
    }

    [Fact]
    public async Task The_policy_is_stateless_across_calls_and_concurrency()
    {
        var policy = new RetryPolicy(new InstantTimeProvider());
        var tasks = Enumerable.Range(0, 64).Select(async i =>
        {
            var failures = i % 3;
            var script = Enumerable.Range(0, failures).Select(_ => (object)Unavailable()).Append(TestResponses.Create(Status.Ok)).ToArray();
            var transport = new ScriptedTransport(script);
            using var response = await PipelineOver(transport, policy).SendAsync(Get(), Options(), TestContext.Current.CancellationToken);
            return (failures, transport.CallCount, response.Status);
        }).ToList();

        foreach (var (failures, calls, status) in await Task.WhenAll(tasks))
        {
            Assert.Equal(failures + 1, calls);
            Assert.Equal(Status.Ok, status);
        }
    }

    [Fact]
    public async Task Each_attempt_drives_a_fresh_ForAttempt_copy()
    {
        var attempts = new List<int>();
        var requests = new List<Request>();
        var probe = new DelegatePolicy(PipelineStage.PerAttempt, (request, context, next) =>
        {
            attempts.Add(context.AttemptNumber);
            requests.Add(request);
            return next.RunAsync(request.WithHeader("X-Downstream", "stamp"), context);
        });
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(new InstantTimeProvider())).Add(probe).Build(transport);

        var seed = Get();
        using var response = await pipeline.SendAsync(seed, Options(), TestContext.Current.CancellationToken);

        Assert.Equal([0, 1, 2], attempts);
        Assert.All(requests, r => Assert.Null(r.Headers.Get("X-Downstream")));
    }

    [Fact]
    public async Task A_service_request_exception_is_retried_a_raw_IOException_is_retried_and_an_InvalidOperationException_is_not()
    {
        var sre = new ScriptedTransport(new ServiceRequestException("x"), TestResponses.Create(Status.Ok));
        using var r1 = await PipelineOver(sre).SendAsync(Get(), Options(), TestContext.Current.CancellationToken);
        Assert.Equal(2, sre.CallCount);

        var io = new ScriptedTransport(new IOException("x"), TestResponses.Create(Status.Ok));
        using var r2 = await PipelineOver(io).SendAsync(Get(), Options(), TestContext.Current.CancellationToken);
        Assert.Equal(2, io.CallCount);

        var ioe = new ScriptedTransport(new InvalidOperationException("x"), TestResponses.Create(Status.Ok));
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await PipelineOver(ioe).SendAsync(Get(), Options(), TestContext.Current.CancellationToken));
        Assert.Equal(1, ioe.CallCount);
    }

    [Fact]
    public async Task A_wrapped_HttpResponseException_is_decided_by_the_configured_status_set()
    {
        var wrapped = new ServiceResponseException("w", new HttpResponseException(TestResponses.Create(Status.FromCode(501))));
        var plain = new ScriptedTransport(wrapped, TestResponses.Create(Status.Ok));
        var options = new DexpaceClientOptions
        {
            Retry = new RetryOptions { BaseDelay = TimeSpan.Zero, Jitter = 0, RetryableStatusCodes = new HashSet<int> { 501 } },
        };

        using var response = await PipelineOver(plain).SendAsync(Get(), options, TestContext.Current.CancellationToken);

        Assert.Equal(2, plain.CallCount);
    }

    [Fact]
    public void The_sync_and_async_paths_agree()
    {
        static ScriptedTransport Make() => new(
            new object[] { Unavailable(), new ServiceRequestException("x"), new ServiceRequestException("y") });

        var asyncTransport = Make();
        var syncTransport = Make();
        var asyncFailure = Assert.Throws<ServiceRequestException>(
            () => PipelineOver(asyncTransport).SendAsync(Get(), Options(), TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult());
        var syncFailure = Assert.Throws<ServiceRequestException>(
            () => PipelineOver(syncTransport).Send(Get(), Options(), TestContext.Current.CancellationToken));

        Assert.Equal(asyncTransport.CallCount, syncTransport.CallCount);
        Assert.Equal(asyncFailure.Message, syncFailure.Message);
        Assert.Equal(asyncFailure.Suppressed.Count, syncFailure.Suppressed.Count);
        Assert.Equal(3, syncTransport.CallCount);
    }

    [Fact]
    public void RetryPolicy_subclass_cannot_change_its_stage()
    {
        Assert.False(typeof(RetryPolicy).IsSealed);
        foreach (var name in new[] { nameof(RetryPolicy.Stage), nameof(RetryPolicy.Process), nameof(RetryPolicy.ProcessAsync) })
        {
            var member = typeof(RetryPolicy).GetMember(name, BindingFlags.Public | BindingFlags.Instance).First();
            var method = member is PropertyInfo property ? property.GetMethod! : (MethodInfo)member;
            Assert.True(method.IsFinal, name);
        }
    }

    [Fact]
    public void RetryPolicy_carries_no_budget()
    {
        Assert.DoesNotContain(
            typeof(RetryOptions).GetProperties(),
            property => property.Name.Contains("Total", StringComparison.Ordinal) || property.Name.Contains("Budget", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(RetryPolicy).GetFields(BindingFlags.Instance | BindingFlags.NonPublic),
            field => field.FieldType.Name.Contains("Budget", StringComparison.Ordinal));
    }

    // ---- AttemptTimeout (XCUT-2; P6a-23) ----

    private static DelegatePolicy HangingUntilCancelled(List<int>? attempts = null, int hangAttempts = int.MaxValue) =>
        new(PipelineStage.PerAttempt, async (request, context, next) =>
        {
            attempts?.Add(context.AttemptNumber);
            if (context.AttemptNumber < hangAttempts)
            {
                await Task.Delay(Timeout.Infinite, context.CancellationToken);
            }

            return await next.RunAsync(request, context);
        });

    [Fact]
    public async Task An_attempt_timeout_surfaces_a_retried_ServiceRequestTimeoutException()
    {
        var clock = new FakeTimeProvider();
        var attempts = new List<int>();
        var transport = new ScriptedTransport(TestResponses.Create(Status.Ok));
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(clock))
            .Add(HangingUntilCancelled(attempts, hangAttempts: 1))
            .Build(transport);

        var task = pipeline.SendAsync(Get(), Options(attemptTimeout: TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken).AsTask();
        using var response = await EngineHarness.DriveAsync(task, clock);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal([0, 1], attempts);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task An_attempt_timeout_that_keeps_firing_surfaces_the_timeout_with_the_trail()
    {
        var clock = new FakeTimeProvider();
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(clock))
            .Add(HangingUntilCancelled())
            .Build(new ScriptedTransport());

        var task = pipeline.SendAsync(Get(), Options(attemptTimeout: TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken).AsTask();
        var thrown = await Assert.ThrowsAsync<ServiceRequestTimeoutException>(async () => await EngineHarness.DriveAsync(task, clock));

        Assert.Equal(2, thrown.Suppressed.Count);
        Assert.All(thrown.Suppressed, e => Assert.IsType<ServiceRequestTimeoutException>(e));
    }

    [Fact]
    public async Task A_caller_cancellation_during_an_attempt_is_not_a_timeout_and_is_not_retried()
    {
        var clock = new FakeTimeProvider();
        var attempts = new List<int>();
        using var cts = new CancellationTokenSource();
        var pipeline = new PipelineBuilder()
            .Add(new RetryPolicy(clock))
            .Add(HangingUntilCancelled(attempts))
            .Build(new ScriptedTransport());

        var task = pipeline.SendAsync(Get(), Options(attemptTimeout: TimeSpan.FromSeconds(5)), cts.Token).AsTask();
        while (attempts.Count == 0)
        {
            await Task.Yield();
        }

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.Equal([0], attempts);
    }

    [Fact]
    public async Task No_AttemptTimeout_arms_no_extra_timer()
    {
        var clock = new RecordingFakeTimeProvider();
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));

        using var response = await PipelineOver(transport, new RetryPolicy(clock)).SendAsync(Get(), Options(), TestContext.Current.CancellationToken);

        Assert.Empty(clock.Timers);
    }

    [Fact]
    public async Task An_attempt_timeout_without_a_RetryPolicy_is_not_enforced()
    {
        // A documented pin: only RetryPolicy reads AttemptTimeout (P6a-23).
        var clock = new FakeTimeProvider();
        var seenToken = CancellationToken.None;
        var probe = new DelegatePolicy(PipelineStage.PerAttempt, (request, context, next) =>
        {
            seenToken = context.CancellationToken;
            return next.RunAsync(request, context);
        });
        var pipeline = new PipelineBuilder().Add(probe).Build(new ScriptedTransport(TestResponses.Create(Status.Ok)));

        using var response = await pipeline.SendAsync(Get(), Options(attemptTimeout: TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromHours(1));

        Assert.False(seenToken.IsCancellationRequested);
    }
}
