// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Support;
using Xunit;
using static Dexpace.Sdk.Conformance.Tests.Support.TestAssertions;

namespace Dexpace.Sdk.Conformance.Tests.Suite;

/// <summary>
/// Design section C: statuses, waivers, the stale-waiver rule and the per-assertion lifecycle, driven with hand-built
/// assertions so these tests do not depend on the real catalogue.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SuiteRunnerTests
{
    private static readonly TransportSuiteOptions s_defaults = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<ConformanceResult> RunAsync(
        TransportSubject subject,
        ConformanceAssertion assertion,
        TransportSuiteOptions? options = null,
        TransportFace face = TransportFace.Async) =>
        SuiteRunner.RunAsync(subject, assertion, face, options ?? s_defaults, Ct);

    private static ConformanceAssertion Asserting(Func<SuiteContext, CancellationToken, Task> body, string name = "transport-1.a", string id = "TRANSPORT-1", TransportFace[]? faces = null) =>
        Make(name, [id], RequirementLevel.Must, faces, body);

    [Fact]
    public async Task A_body_that_returns_is_Passed()
    {
        var result = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => Task.CompletedTask));

        Assert.Equal(ConformanceStatus.Passed, result.Status);
        Assert.Equal(TransportFace.Async, result.Face);
        Assert.Null(result.Exception);
    }

    [Fact]
    public async Task A_conformance_exception_is_Failed_and_its_detail_carries_message_expected_and_actual()
    {
        var failure = new ConformanceException("the clause", "520", "502");

        var result = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => throw failure));

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("the clause", result.Detail, StringComparison.Ordinal);
        Assert.Contains("520", result.Detail, StringComparison.Ordinal);
        Assert.Contains("502", result.Detail, StringComparison.Ordinal);
        Assert.Same(failure, result.Exception);
    }

    [Fact]
    public async Task Any_other_exception_is_Errored_never_silently_a_failure()
    {
        var result = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => throw new InvalidOperationException("kit bug")));

        Assert.Equal(ConformanceStatus.Errored, result.Status);
        Assert.Contains("InvalidOperationException", result.Detail, StringComparison.Ordinal);
        Assert.Contains("kit bug", result.Detail, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(result.Exception);
    }

    [Fact]
    public async Task A_vacuous_signal_is_Vacuous_carrying_its_reason()
    {
        var result = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => throw new ConformanceVacuousException("no antecedent")));

        Assert.Equal(ConformanceStatus.Vacuous, result.Status);
        Assert.Equal("no antecedent", result.Detail);
    }

    [Fact]
    public async Task A_missing_hook_is_NotExercised_naming_the_hook_and_never_vacuous()
    {
        var result = await RunAsync(new FakeSubject().Subject(), Asserting((ctx, _) =>
        {
            ctx.RequireBorrowed();
            return Task.CompletedTask;
        }));

        Assert.Equal(ConformanceStatus.NotExercised, result.Status);
        Assert.Contains(nameof(TransportSubject.CreateBorrowed), result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_face_is_NotExercised_naming_the_face()
    {
        var subject = new FakeSubject().Subject(blocking: false);

        var report = await SuiteRunner.RunAllAsync(subject, [Asserting((_, _) => Task.CompletedTask, faces: [TransportFace.Async, TransportFace.Blocking])], s_defaults, Ct);

        Assert.Equal(ConformanceStatus.Passed, report.Results.Single(r => r.Face == TransportFace.Async).Status);
        var blocking = report.Results.Single(r => r.Face == TransportFace.Blocking);
        Assert.Equal(ConformanceStatus.NotExercised, blocking.Status);
        Assert.Contains("Blocking", blocking.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_body_that_outlives_the_assertion_timeout_is_Failed_naming_the_bound_and_the_assertion()
    {
        var options = new TransportSuiteOptions { AssertionTimeout = TimeSpan.FromMilliseconds(300) };
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => never.Task, name: "transport-3.hangs"), options);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("300 ms", result.Detail, StringComparison.Ordinal);
        Assert.Contains("transport-3.hangs", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_outer_token_cancelled_abandons_the_run_with_cancellation_and_still_disposes()
    {
        var fake = new FakeSubject();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var assertion = Asserting(async (ctx, token) =>
        {
            ctx.CreateTransport();
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SuiteRunner.RunAsync(fake.Subject(), assertion, TransportFace.Async, s_defaults, cts.Token));

        Assert.Equal([1], fake.Built.Select(t => t.Disposed));
    }

    [Fact]
    public async Task A_waiver_turns_a_failure_or_an_error_into_Waived_and_keeps_the_original_detail()
    {
        var waiver = new ConformanceWaiver("TRANSPORT-1", "known gap") { Owner = "8b" };
        var options = new TransportSuiteOptions { Waivers = [waiver] };

        var failed = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => throw new ConformanceException("broken")), options);
        var errored = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => throw new InvalidOperationException("crash")), options);

        Assert.Equal(ConformanceStatus.Waived, failed.Status);
        Assert.Same(waiver, failed.Waiver);
        Assert.Contains("known gap", failed.Detail, StringComparison.Ordinal);
        Assert.Contains("broken", failed.Detail, StringComparison.Ordinal);
        Assert.Equal(ConformanceStatus.Waived, errored.Status);
        Assert.Contains("crash", errored.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_waiver_that_is_no_longer_needed_fails_the_run()
    {
        var waiver = new ConformanceWaiver("TRANSPORT-1", "was broken") { Owner = "8b" };

        var result = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => Task.CompletedTask), new TransportSuiteOptions { Waivers = [waiver] });

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("waiver for TRANSPORT-1 is no longer needed", result.Detail, StringComparison.Ordinal);
        Assert.Same(waiver, result.Waiver);
    }

    [Fact]
    public async Task A_waiver_for_an_id_the_assertion_does_not_cite_does_nothing()
    {
        var options = new TransportSuiteOptions { Waivers = [new("TRANSPORT-2", "elsewhere")] };

        var failing = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => throw new ConformanceException("x")), options);
        var passing = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => Task.CompletedTask), options);

        Assert.Equal(ConformanceStatus.Failed, failing.Status);
        Assert.Equal(ConformanceStatus.Passed, passing.Status);
    }

    [Fact]
    public async Task A_waiver_never_converts_vacuous_or_not_exercised()
    {
        var options = new TransportSuiteOptions { Waivers = [new("TRANSPORT-1", "r")] };

        var vacuous = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => throw new ConformanceVacuousException("none")), options);
        var missingFace = await RunAsync(new FakeSubject().Subject(blocking: false), Asserting((_, _) => Task.CompletedTask, faces: [TransportFace.Blocking]), options, TransportFace.Blocking);

        Assert.Equal(ConformanceStatus.Vacuous, vacuous.Status);
        Assert.Equal(ConformanceStatus.NotExercised, missingFace.Status);
    }

    [Fact]
    public async Task A_waiver_narrowed_by_assertion_and_face_covers_only_that_pair()
    {
        var narrowed = new ConformanceWaiver("TRANSPORT-1", "only the log clause") { Assertion = "transport-1.log", Face = TransportFace.Async };
        var options = new TransportSuiteOptions { Waivers = [narrowed] };
        var fails = (SuiteContext _, CancellationToken __) => Task.FromException(new ConformanceException("x"));

        var log = await RunAsync(new FakeSubject().Subject(), Asserting(fails, name: "transport-1.log"), options);
        var framing = await RunAsync(new FakeSubject().Subject(), Asserting(fails, name: "transport-1.framing"), options);
        var framingPasses = await RunAsync(new FakeSubject().Subject(), Asserting((_, _) => Task.CompletedTask, name: "transport-1.framing"), options);
        var blockingLog = await RunAsync(new FakeSubject().Subject(), Asserting(fails, name: "transport-1.log", faces: [TransportFace.Async, TransportFace.Blocking]), options, TransportFace.Blocking);

        Assert.Equal(ConformanceStatus.Waived, log.Status);
        Assert.Equal(ConformanceStatus.Failed, framing.Status);
        Assert.Equal(ConformanceStatus.Passed, framingPasses.Status);
        Assert.Equal(ConformanceStatus.Failed, blockingLog.Status);
    }

    [Fact]
    public async Task An_unknown_waiver_target_is_an_extra_failed_result_with_a_truncated_name()
    {
        var oversized = new string('x', 200);
        var options = new TransportSuiteOptions
        {
            Waivers = [new("TRANSPORT-1", "typo") { Assertion = oversized }, new("TRANSPORT-1", "id-wide")],
        };

        var report = await SuiteRunner.RunAllAsync(new FakeSubject().Subject(), [Asserting((_, _) => throw new ConformanceException("x"))], options, Ct);

        var unknown = report.Results.Single(r => r.Assertion.Name == "waiver");
        Assert.Equal(ConformanceStatus.Failed, unknown.Status);
        Assert.StartsWith("unknown waiver target '", unknown.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 65), unknown.Detail, StringComparison.Ordinal);
        Assert.False(report.IsGreen);
        Assert.Single(report.Results, r => r.Assertion.Name != "waiver");
    }

    [Fact]
    public async Task Every_transport_built_is_disposed_on_pass_fail_error_and_timeout()
    {
        var options = new TransportSuiteOptions { AssertionTimeout = TimeSpan.FromMilliseconds(300) };
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<SuiteContext, CancellationToken, Task>[] bodies =
        [
            (ctx, _) => { ctx.CreateTransport(); return Task.CompletedTask; },
            (ctx, _) => { ctx.CreateTransport(); throw new ConformanceException("x"); },
            (ctx, _) => { ctx.CreateTransport(); throw new InvalidOperationException("x"); },
            (ctx, _) => { ctx.CreateTransport(); return never.Task; },
        ];

        foreach (var body in bodies)
        {
            var fake = new FakeSubject();
            await RunAsync(fake.Subject(), Asserting(body), options);

            var built = Assert.Single(fake.Built);
            Assert.Equal(1, built.Disposed);
        }
    }

    [Fact]
    public async Task A_transport_whose_disposal_throws_is_reported_as_a_failure_when_the_body_passed()
    {
        var fake = new FakeSubject();
        var subject = new TransportSubject
        {
            Name = "throws-on-dispose",
            CreateAsync = _ => new ThrowingDisposeTransport(),
        };

        var result = await RunAsync(subject, Asserting((ctx, _) => { ctx.CreateTransport(); return Task.CompletedTask; }));

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("TRANSPORT-16", result.Detail, StringComparison.Ordinal);
        Assert.Empty(fake.Built);
    }

    [Fact]
    public async Task Two_assertions_get_two_transports_and_two_servers()
    {
        var fake = new FakeSubject();
        var ports = new List<int>();
        var assertion = Asserting((ctx, _) =>
        {
            ctx.CreateTransport();
            ports.Add(ctx.StartServer().BaseUri.Port);
            return Task.CompletedTask;
        });

        await SuiteRunner.RunAllAsync(fake.Subject(), [assertion, assertion], s_defaults, Ct);

        Assert.Equal(2, fake.Built.Count);
        Assert.NotSame(fake.Built[0], fake.Built[1]);
        Assert.Equal(2, ports.Distinct().Count());
    }

    [Fact]
    public async Task Statuses_do_not_depend_on_the_order_the_assertions_run_in()
    {
        var servers = new List<LoopbackServerProbe>();
        var first = Asserting(async (ctx, _) => servers.Add(await LoopbackServerProbe.UseAsync(ctx, "first")), name: "transport-1.first");
        var second = Asserting(async (ctx, _) => servers.Add(await LoopbackServerProbe.UseAsync(ctx, "second")), name: "transport-1.second");

        var forward = await SuiteRunner.RunAllAsync(new FakeSubject().Subject(), [first, second], s_defaults, Ct);
        var reverse = await SuiteRunner.RunAllAsync(new FakeSubject().Subject(), [second, first], s_defaults, Ct);

        Assert.Equal(
            forward.Results.OrderBy(r => r.Assertion.Name, StringComparer.Ordinal).Select(r => (r.Assertion.Name, r.Status)),
            reverse.Results.OrderBy(r => r.Assertion.Name, StringComparer.Ordinal).Select(r => (r.Assertion.Name, r.Status)));
        Assert.Equal(4, servers.Select(s => s.Port).Distinct().Count());
    }

    [Fact]
    public async Task An_assertion_that_declares_both_faces_yields_two_results_and_one_that_declares_one_yields_one()
    {
        var both = Asserting((_, _) => Task.CompletedTask, name: "transport-1.both", faces: [TransportFace.Async, TransportFace.Blocking]);
        var one = Asserting((_, _) => Task.CompletedTask, name: "transport-1.one");

        var report = await SuiteRunner.RunAllAsync(new FakeSubject().Subject(), [both, one], s_defaults, Ct);

        Assert.Equal(3, report.Results.Count);
        Assert.Equal([TransportFace.Async, TransportFace.Blocking], report.Results.Where(r => r.Assertion == both).Select(r => r.Face));
        Assert.True(report.IsGreen);
    }

    [Fact]
    public async Task The_async_face_routes_through_ExecuteAsync_and_the_blocking_face_through_Execute_on_a_dedicated_thread()
    {
        var fake = new FakeSubject();
        var assertion = Asserting(
            async (ctx, token) =>
            {
                await using var transport = ctx.CreateTransport();
                await using var response = await transport.SendAsync(Dexpace.Sdk.Core.Http.Request.Request.Get("http://127.0.0.1:1/"), token);
            },
            faces: [TransportFace.Async, TransportFace.Blocking]);

        await SuiteRunner.RunAllAsync(fake.Subject(), [assertion], s_defaults, Ct);

        Assert.Equal(1, fake.Built[0].AsyncCalls);
        Assert.Equal(0, fake.Built[0].BlockingCalls);
        Assert.Equal(0, fake.Built[1].AsyncCalls);
        Assert.Equal(1, fake.Built[1].BlockingCalls);
        Assert.False(fake.Built[1].LastBlockingCallOnPoolThread);
    }

    [Fact]
    public async Task Two_blocked_blocking_calls_do_not_starve_each_other()
    {
        var gate = new ManualResetEventSlim();
        var fake = new FakeSubject { Configure = t => t.OnBlocking = (request, token) => { gate.Wait(Waits.Bound, token); return FakeTransport.Ok(request); } };
        var assertion = Asserting(
            async (ctx, token) =>
            {
                await using var transport = ctx.CreateTransport();
                var calls = Enumerable.Range(0, 8).Select(_ => transport.SendAsync(Dexpace.Sdk.Core.Http.Request.Request.Get("http://127.0.0.1:1/"), token)).ToArray();
                gate.Set();
                foreach (var response in await Task.WhenAll(calls))
                {
                    response.Dispose();
                }
            },
            faces: [TransportFace.Blocking]);

        var result = await RunAsync(fake.Subject(), assertion, face: TransportFace.Blocking);

        Assert.Equal(ConformanceStatus.Passed, result.Status);
        gate.Dispose();
    }

    [Fact]
    public async Task The_report_carries_every_result_and_every_configured_waiver_and_a_stale_waiver_makes_it_red()
    {
        var stale = new ConformanceWaiver("TRANSPORT-1", "stale");
        var unused = new ConformanceWaiver("TRANSPORT-2", "never applied");
        var options = new TransportSuiteOptions { Waivers = [stale, unused] };

        var report = await SuiteRunner.RunAllAsync(new FakeSubject().Subject(), [Asserting((_, _) => Task.CompletedTask)], options, Ct);

        Assert.False(report.IsGreen);
        Assert.Equal("fake", report.SubjectName);
        var text = report.ToString();
        Assert.Contains("stale", text, StringComparison.Ordinal);
        Assert.Contains("never applied", text, StringComparison.Ordinal);
    }

    private sealed class ThrowingDisposeTransport : Dexpace.Sdk.Core.Client.IAsyncHttpClient
    {
        public Task<Dexpace.Sdk.Core.Http.Response.Response> ExecuteAsync(
            Dexpace.Sdk.Core.Http.Request.Request request,
            Dexpace.Sdk.Core.Http.Request.RequestOptions options,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => throw new InvalidOperationException("dispose failed");
    }

    private sealed record LoopbackServerProbe(int Port)
    {
        internal static Task<LoopbackServerProbe> UseAsync(SuiteContext ctx, string text) =>
            Task.FromResult(new LoopbackServerProbe(ctx.StartServer(Dexpace.Sdk.Conformance.Wire.LoopbackResponse.Ok(text)).BaseUri.Port));
    }
}
