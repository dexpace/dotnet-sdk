// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from ruby-sdk@5b17395 gems/dexpace-core/test/dexpace/resilience/*_test.rb and
// nodejs-sdk@54aeed4 packages/core/src/retry/retry-dispatch.test.ts, re-expressed over RecoveryDispatcher (P6a-5).
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.Core.Tests.Resilience;
using Dexpace.Sdk.TestSupport.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Time.Testing;
using Xunit;

#pragma warning disable CA2000 // The fakes and responses hold nothing a test needs to release.

namespace Dexpace.Sdk.Core.Tests.Recovery;

/// <summary>The recovery stack's retry (RETRY-27, RETRY-36, RETRY-37, RECOV-16 to RECOV-20, RECOV-27, RECOV-28, RECOV-31).</summary>
[Trait("Category", "Unit")]
public sealed class RetryRecoveryTests
{
    private static readonly Request s_get = Request.Get("https://example.test/items");

    private static RetryOptions Options(int retries = 2, double baseMs = 0, TimeSpan? fixedDelay = null, string? attemptHeader = null) => new()
    {
        MaxRetryAttempts = retries,
        BaseDelay = TimeSpan.FromMilliseconds(baseMs),
        Jitter = 0,
        FixedDelay = fixedDelay,
        AttemptHeaderName = attemptHeader,
    };

    private static RecoveryDispatcher Dispatcher(
        RetryRecovery retry,
        IEnumerable<IRequestStep>? requestSteps = null,
        IEnumerable<IResponseStep>? responseSteps = null,
        IEnumerable<IRecoveryStep>? recoverySteps = null) =>
        new(new RequestRecoveryChain(requestSteps ?? []), new ResponseRecoveryChain(responseSteps ?? [], recoverySteps ?? []), retry);

    private static Task<Response> Run(RecoveryDispatcher dispatcher, ScriptedTransport transport, bool async, Request? request = null, RequestOptions? options = null, CancellationToken? token = null)
    {
        var ct = token ?? TestContext.Current.CancellationToken;
        return async
            ? dispatcher.DispatchAsync(transport, request ?? s_get, options ?? RequestOptions.Empty, ct).AsTask()
            : Task.Run(() => dispatcher.Dispatch(transport, request ?? s_get, options ?? RequestOptions.Empty, ct), ct);
    }

    private static Response Unavailable(ResponseBody? body = null, Headers? headers = null) =>
        TestResponses.Create(Status.ServiceUnavailable, headers: headers, body: body);

    [Fact]
    public void Construction_validates_the_total_timeout()
    {
        Assert.Throws<ArgumentNullException>(() => new RetryRecovery(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryRecovery(new RetryOptions(), TimeSpan.FromTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryRecovery(new RetryOptions(), TimeSpan.MaxValue));
        Assert.Equal(TimeSpan.Zero, new RetryRecovery(new RetryOptions()).TotalTimeout);
        Assert.Equal(TimeSpan.FromTicks(long.MaxValue / 100), new RetryRecovery(new RetryOptions(), TimeSpan.FromTicks(long.MaxValue / 100)).TotalTimeout);
    }

    [Fact]
    public void Options_and_TotalTimeout_are_exposed()
    {
        var options = Options();
        var retry = new RetryRecovery(options, TimeSpan.FromSeconds(9));

        Assert.Same(options, retry.Options);
        Assert.Equal(TimeSpan.FromSeconds(9), retry.TotalTimeout);
        Assert.Same(retry, Dispatcher(retry).Retry);
        Assert.Throws<ArgumentNullException>(() => new RecoveryDispatcher(new RequestRecoveryChain([]), ResponseRecoveryChain.Empty, null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_503_503_200_reaches_the_200(bool async)
    {
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));

        using var response = await Run(Dispatcher(new RetryRecovery(Options())), transport, async);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(3, transport.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_exhausted_503_surfaces_the_HttpResponseException_with_the_trail(bool async)
    {
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), Unavailable(), Unavailable());

        var thrown = await Assert.ThrowsAsync<HttpResponseException>(() => Run(Dispatcher(new RetryRecovery(Options())), transport, async));

        Assert.Equal(3, transport.CallCount);
        Assert.Equal(Status.ServiceUnavailable, thrown.Status);
        Assert.Equal(2, thrown.Suppressed.Count);
        Assert.All(thrown.Suppressed, e => Assert.IsType<HttpResponseException>(e));
    }

    [Fact]
    public async Task A_non_retryable_error_status_passes_as_a_Success()
    {
        var transport = new ScriptedTransport(TestResponses.Create(Status.NotFound), TestResponses.Create(Status.Ok));

        using var response = await Run(Dispatcher(new RetryRecovery(Options())), transport, async: true);

        Assert.Equal(Status.NotFound, response.Status);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task The_configured_set_decides_a_501()
    {
        var options = Options() with { RetryableStatusCodes = new HashSet<int> { 501 } };
        var transport = new ScriptedTransport(TestResponses.Create(Status.FromCode(501)), TestResponses.Create(Status.Ok));
        using var response = await Run(Dispatcher(new RetryRecovery(options)), transport, async: true);
        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(2, transport.CallCount);

        // Under the default set a 501 is final, whatever the baked flag says about other codes.
        var defaults = new ScriptedTransport(TestResponses.Create(Status.FromCode(501)), TestResponses.Create(Status.Ok));
        using var untouched = await Run(Dispatcher(new RetryRecovery(Options())), defaults, async: true);
        Assert.Equal(501, untouched.Status.Code);
        Assert.Equal(1, defaults.CallCount);
    }

    [Fact]
    public async Task A_retryable_status_is_buffered_through_ErrorBodyBuffer_once()
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log, "retried");
        var transport = new ScriptedTransport(Unavailable(body), TestResponses.Create(Status.Ok));
        using var response = await Run(Dispatcher(new RetryRecovery(Options())), transport, async: true);
        Assert.Equal(["retried:open", "retried:dispose"], log);

        // An oversize body is capped at 1 MiB and the surfaced exception carries the bounded copy.
        static Response Oversize() => Unavailable(ResponseBody.FromBytes(new byte[3 * 1024 * 1024]));
        var all = new ScriptedTransport(Oversize(), Oversize(), Oversize());
        var thrown = await Assert.ThrowsAsync<HttpResponseException>(() => Run(Dispatcher(new RetryRecovery(Options())), all, async: true));
        var bytes = await thrown.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(Response.MaxBufferedErrorBytes, bytes.Length);
    }

    [Fact]
    public async Task With_ErrorMappingStep_installed_nothing_is_mapped_twice()
    {
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));
        using var response = await Run(
            Dispatcher(new RetryRecovery(Options()), responseSteps: [ErrorMappingStep.Instance]),
            transport,
            async: true);
        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(2, transport.CallCount);

        var failing = new ScriptedTransport(Unavailable(), Unavailable(), Unavailable());
        var thrown = await Assert.ThrowsAsync<HttpResponseException>(() => Run(
            Dispatcher(new RetryRecovery(Options()), responseSteps: [ErrorMappingStep.Instance]),
            failing,
            async: true));
        Assert.Equal(2, thrown.Suppressed.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_request_chain_runs_once_and_the_recovery_steps_run_once(bool async)
    {
        var requestStep = new DelegateRequestStep((request, _) => request.WithHeader("Idempotency-Key", "one-key"));
        var recoveryStep = new DelegateRecoveryStep((outcome, _) => outcome);
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));

        using var response = await Run(
            Dispatcher(new RetryRecovery(Options()), [requestStep], recoverySteps: [recoveryStep]),
            transport,
            async,
            Request.Post("https://example.test/items", RequestBody.FromString("{}")));

        Assert.Equal(1, requestStep.CallCount);
        Assert.Equal(1, recoveryStep.CallCount);
        Assert.Equal(3, transport.CallCount);
        Assert.All(transport.Requests, r => Assert.Equal("one-key", r.Headers.Get("Idempotency-Key")));
    }

    [Fact]
    public async Task Each_send_sees_the_response_steps()
    {
        var step = new DelegateResponseStep((response, _) => response);
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));

        using var response = await Run(Dispatcher(new RetryRecovery(Options()), responseSteps: [step]), transport, async: true);

        Assert.Equal(3, step.CallCount);
    }

    [Fact]
    public async Task A_throwing_response_step_becomes_a_Failure_with_the_response_released()
    {
        var log = new List<string>();
        using var body = new TrackingResponseBody(log, "released");
        var boom = new InvalidOperationException("step");
        var step = new DelegateResponseStep((_, _) => throw boom);
        Outcome? seen = null;
        var transport = new ScriptedTransport(TestResponses.Create(Status.Ok, body: body));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(
            Dispatcher(new RetryRecovery(Options()), responseSteps: [step], recoverySteps: [new DelegateRecoveryStep((o, _) => seen = o)]),
            transport,
            async: true));

        Assert.Same(boom, thrown);
        Assert.Same(boom, Assert.IsType<Outcome.Failure>(seen).Error);
        Assert.Equal(1, transport.CallCount);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task The_total_timeout_aborts_on_elapsed_and_on_elapsed_plus_delay_and_clamps_the_delay()
    {
        var clock = new RecordingFakeTimeProvider();
        var retry = new RetryRecovery(Options(5, fixedDelay: TimeSpan.FromSeconds(4)), TimeSpan.FromSeconds(10), clock);
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), Unavailable(), Unavailable());

        var task = Run(Dispatcher(retry), transport, async: true);
        var thrown = await Assert.ThrowsAsync<HttpResponseException>(async () => await EngineHarness.DriveAsync(task, clock, TimeSpan.FromSeconds(1)));

        // 4 s and 8 s waited; a third 4 s wait would end at 12 s, past the 10 s budget: the last failure is surfaced unchanged.
        Assert.Equal(3, transport.CallCount);
        Assert.Equal([TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)], clock.Timers);
        Assert.Equal(2, thrown.Suppressed.Count);

        // Zero disables the budget.
        var unbounded = new RetryRecovery(Options(3, fixedDelay: TimeSpan.FromHours(1)), TimeSpan.Zero, clock);
        var again = new ScriptedTransport(Unavailable(), Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));
        using var ok = await EngineHarness.DriveAsync(Run(Dispatcher(unbounded), again, async: true), clock, TimeSpan.FromMinutes(30));
        Assert.Equal(4, again.CallCount);
    }

    [Fact]
    public async Task A_pacing_hint_replaces_the_schedule_and_is_clamped_by_the_budget()
    {
        var clock = new RecordingFakeTimeProvider();
        var hint = new Headers.Builder().Set("Retry-After", "3").Build();
        var retry = new RetryRecovery(Options(2, baseMs: 5_000), TimeSpan.FromSeconds(10), clock);
        var transport = new ScriptedTransport(Unavailable(headers: hint), TestResponses.Create(Status.Ok));

        using var response = await EngineHarness.DriveAsync(Run(Dispatcher(retry), transport, async: true), clock);
        Assert.Equal([TimeSpan.FromSeconds(3)], clock.Timers);

        // A hint longer than the budget abandons the retry: one send, the failure surfaced.
        var longHint = new Headers.Builder().Set("Retry-After", "60").Build();
        var abandoned = new ScriptedTransport(Unavailable(headers: longHint), TestResponses.Create(Status.Ok));
        await Assert.ThrowsAsync<HttpResponseException>(() => Run(Dispatcher(retry), abandoned, async: true));
        Assert.Equal(1, abandoned.CallCount);
    }

    [Fact]
    public async Task A_cancelled_wait_yields_Failure_OperationCanceledException_and_the_dispatcher_rethrows_it_signalled()
    {
        var clock = new RecordingFakeTimeProvider();
        using var cts = new CancellationTokenSource();
        Outcome? seen = null;
        var retry = new RetryRecovery(Options(2, baseMs: 5_000), timeProvider: clock);
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));
        var dispatcher = Dispatcher(retry, recoverySteps: [new DelegateRecoveryStep((o, _) => seen = o)]);

        var task = Run(dispatcher, transport, async: true, token: cts.Token);
        while (clock.Timers.Count == 0)
        {
            await Task.Yield();
        }

        await cts.CancelAsync();

        var thrown = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(cts.Token.IsCancellationRequested);
        Assert.Same(thrown, Assert.IsType<Outcome.Failure>(seen).Error);
        Assert.Single(ExceptionTrail.GetSuppressed(thrown));
    }

    [Fact]
    public async Task The_attempt_header_stamps_from_one_on_every_send()
    {
        var transport = new ScriptedTransport(Unavailable(), Unavailable(), TestResponses.Create(Status.Ok));

        using var response = await Run(Dispatcher(new RetryRecovery(Options(attemptHeader: "X-Attempt"))), transport, async: true);

        Assert.Equal(["1", "2", "3"], transport.Requests.Select(r => r.Headers.Get("X-Attempt")));
    }

    [Fact]
    public async Task The_dispatcher_and_RetryRecovery_hold_no_per_call_state()
    {
        var dispatcher = Dispatcher(new RetryRecovery(Options(5)));
        var tasks = Enumerable.Range(0, 64).Select(async i =>
        {
            var failures = i % 4;
            var script = Enumerable.Range(0, failures).Select(_ => (object)Unavailable()).Append(TestResponses.Create(Status.Ok)).ToArray();
            var transport = new ScriptedTransport(script);
            using var response = await Run(dispatcher, transport, async: true);
            return (failures, transport.CallCount);
        }).ToList();

        foreach (var (failures, calls) in await Task.WhenAll(tasks))
        {
            Assert.Equal(failures + 1, calls);
        }
    }

    [Fact]
    public async Task A_dispatcher_without_RetryRecovery_behaves_exactly_as_before()
    {
        var dispatcher = new RecoveryDispatcher(new RequestRecoveryChain([]), ResponseRecoveryChain.Empty);
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));

        using var response = await Run(dispatcher, transport, async: true);

        Assert.Null(dispatcher.Retry);
        Assert.Equal(Status.ServiceUnavailable, response.Status);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task MaxRetries_zero_on_RequestOptions_disables_retries_for_the_call()
    {
        var transport = new ScriptedTransport(Unavailable(), TestResponses.Create(Status.Ok));

        var thrown = await Assert.ThrowsAsync<HttpResponseException>(() => Run(
            Dispatcher(new RetryRecovery(Options())),
            transport,
            async: true,
            options: new RequestOptions { MaxRetries = 0 }));

        Assert.Equal(1, transport.CallCount);
        Assert.Empty(thrown.Suppressed);
    }

    [Fact]
    public async Task A_transport_exception_is_retried_by_the_classifier()
    {
        var transport = new ScriptedTransport(new ServiceRequestException("never sent"), new System.IO.IOException("reset"), TestResponses.Create(Status.Ok));

        using var response = await Run(Dispatcher(new RetryRecovery(Options())), transport, async: true);

        Assert.Equal(3, transport.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_null_transport_response_is_a_non_retryable_failure_naming_the_transport(bool async)
    {
        var transport = new ScriptedTransport((Func<Response>)(() => null!), TestResponses.Create(Status.Ok));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(Dispatcher(new RetryRecovery(Options())), transport, async));

        Assert.Equal(1, transport.CallCount);
        Assert.Contains(typeof(ScriptedTransport).FullName!, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("null response", thrown.Message, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> GateCases()
    {
        var bodies = new (string Name, Func<RequestBody?> Make, bool Replayable)[]
        {
            ("none", () => null, false),
            ("bytes", () => RequestBody.FromBytes(new byte[] { 1 }), true),
            ("string", () => RequestBody.FromString("x"), true),
            ("form", () => RequestBody.FromForm([new KeyValuePair<string, string>("a", "b")]), true),
            ("file", () => RequestBody.FromFile(typeof(RetryRecoveryTests).Assembly.Location), true),
            ("seekable stream", () => RequestBody.FromStream(new MemoryStream(new byte[] { 1 }), null, 1), true),
            ("single-use stream", () => RequestBody.FromStream(new SingleUseStream()), false),
        };
        foreach (var method in new[] { "GET", "PUT", "DELETE", "POST", "PATCH" })
        {
            foreach (var (name, make, replayable) in bodies)
            {
                foreach (var transportFailure in new[] { true, false })
                {
                    var idempotent = method is "GET" or "PUT" or "DELETE";
                    var resendable = name == "none" ? idempotent : replayable;
                    yield return new object[] { method, name, transportFailure, resendable ? 3 : 1 };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GateCases))]
    public async Task The_resend_gate_decides_the_send_count_for_every_method_body_and_failure_kind(
        string methodName, string bodyName, bool transportFailure, int expectedSends)
    {
        var body = bodyName switch
        {
            "none" => null,
            "bytes" => RequestBody.FromBytes(new byte[] { 1 }),
            "string" => RequestBody.FromString("x"),
            "form" => RequestBody.FromForm([new KeyValuePair<string, string>("a", "b")]),
            "file" => RequestBody.FromFile(typeof(RetryRecoveryTests).Assembly.Location),
            "seekable stream" => RequestBody.FromStream(new MemoryStream(new byte[] { 1 }), null, 1),
            _ => RequestBody.FromStream(new SingleUseStream()),
        };
        var method = Method.Of(methodName);
        Request request;
        try
        {
            request = new Request(method, new Uri("https://example.test/items"), null, body);
        }
        catch (ArgumentException)
        {
            return; // The model refuses a body on GET; there is no such request.
        }

        object Failure() => transportFailure ? new ServiceRequestException("never sent") : Unavailable();
        var transport = new ScriptedTransport(Failure(), Failure(), Failure());

        await Assert.ThrowsAnyAsync<SdkException>(() => Run(Dispatcher(new RetryRecovery(Options())), transport, async: true, request));

        Assert.Equal(expectedSends, transport.CallCount);
    }

    private sealed class SingleUseStream : MemoryStream
    {
        public SingleUseStream()
            : base(new byte[] { 1 })
        {
        }

        public override bool CanSeek => false;
    }
}
