// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Security;

/// <summary>
/// Roadmap phase 1, S7 (RETRY-18, RECOV-26; design §6.1, gap analysis runtime fact 3): a hostile pacing hint must not
/// crash the call. <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/> throws
/// <see cref="ArgumentOutOfRangeException"/> above ~49.7 days, and the verified defect was <c>Retry-After: 5184000</c>
/// (60 days) escaping <c>SendAsync</c> as that exception. Every pacing delta is clamped to 365 days and a wait above
/// the timer's ceiling runs as successive bounded waits. Permanent (roadmap constraint 5); phase 6a owns the pacing
/// parser.
/// </summary>
[Trait("Category", "Security")]
public sealed class RetryPacingOverflowTests
{
    private static readonly TimeSpan s_ceiling = TimeSpan.FromDays(365);

    // Task.Delay's own limit: uint.MaxValue - 1 milliseconds.
    private static readonly TimeSpan s_timerLimit = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    [Theory]
    [InlineData("5184000", 60)]        // 60 days: the verified crash
    [InlineData("31536000", 365)]      // exactly the ceiling
    [InlineData("86400000", 365)]      // 1000 days: clamped
    [InlineData("2147483647", 365)]    // int.MaxValue seconds: clamped
    public async Task A_huge_delta_seconds_hint_is_clamped_and_waited_in_bounded_chunks(string retryAfter, int expectedDays)
    {
        var clock = new RecordingTimeProvider();
        var (response, transport) = await SendWithHintAsync(retryAfter, clock);

        using (response)
        {
            Assert.Equal(Status.Ok, response.Status);
        }

        Assert.Equal(2, transport.CallCount);
        Assert.Equal(TimeSpan.FromDays(expectedDays), clock.TotalDue);
        Assert.All(clock.DueTimes, due => Assert.InRange(due, TimeSpan.Zero, s_timerLimit));
    }

    [Fact]
    public async Task A_far_future_http_date_hint_is_clamped_to_the_ceiling()
    {
        var clock = new RecordingTimeProvider();
        var (response, _) = await SendWithHintAsync("Fri, 31 Dec 9999 23:59:59 GMT", clock);

        using (response)
        {
            Assert.Equal(Status.Ok, response.Status);
        }

        Assert.Equal(s_ceiling, clock.TotalDue);
        Assert.All(clock.DueTimes, due => Assert.InRange(due, TimeSpan.Zero, s_timerLimit));
    }

    [Fact]
    public async Task A_backoff_configured_beyond_the_timer_limit_is_clamped_and_chunked()
    {
        var scenarioRequest = Request.Get("https://api.example.com/");
        // The computed schedule is a pacing delta too: a 400-day backoff must neither throw nor exceed the ceiling.
        var clock = new RecordingTimeProvider();
        var options = new DexpaceClientOptions
        {
            Retry = new RetryOptions
            {
                MaxRetryAttempts = 3,
                BaseDelay = TimeSpan.FromDays(400),
                MaxDelay = TimeSpan.FromDays(400),
                HonorRetryAfter = false,
            },
        };
        var transport = new ScriptedTransport(
            TestResponses.Create(Status.ServiceUnavailable, scenarioRequest),
            TestResponses.Create(Status.ServiceUnavailable, scenarioRequest),
            TestResponses.Create(Status.ServiceUnavailable, scenarioRequest),
            TestResponses.Create(Status.Ok, scenarioRequest));
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(clock)).Build(transport);

        using var response = await pipeline.SendAsync(
            scenarioRequest, options, TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal(4, transport.CallCount);
        Assert.All(clock.DueTimes, due => Assert.InRange(due, TimeSpan.Zero, s_timerLimit));
        Assert.InRange(clock.TotalDue, TimeSpan.Zero, s_ceiling * 3);
    }

    private static async Task<(Response Response, ScriptedTransport Transport)> SendWithHintAsync(
        string retryAfter, TimeProvider clock)
    {
        var scenarioRequest = Request.Get("https://api.example.com/");
        var headers = new Headers.Builder().Set("Retry-After", retryAfter).Build();
        var transport = new ScriptedTransport(
            TestResponses.Create(Status.ServiceUnavailable, scenarioRequest, headers),
            TestResponses.Create(Status.Ok, scenarioRequest));
        var pipeline = new PipelineBuilder().Add(new RetryPolicy(clock)).Build(transport);
        var response = await pipeline.SendAsync(
            scenarioRequest, new DexpaceClientOptions(), TestContext.Current.CancellationToken);
        return (response, transport);
    }

    /// <summary>
    /// A pinned clock that records every timer's due time and fires each after one millisecond, so a months-long wait
    /// completes at once while the test still sees what was asked for.
    /// </summary>
    private sealed class RecordingTimeProvider : TimeProvider
    {
        private readonly List<TimeSpan> _dueTimes = [];

        public IReadOnlyList<TimeSpan> DueTimes
        {
            get
            {
                lock (_dueTimes)
                {
                    return [.. _dueTimes];
                }
            }
        }

        public TimeSpan TotalDue => DueTimes.Aggregate(TimeSpan.Zero, (sum, due) => sum + due);

        public override DateTimeOffset GetUtcNow() => new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (_dueTimes)
            {
                _dueTimes.Add(dueTime);
            }

            return base.CreateTimer(callback, state, TimeSpan.FromMilliseconds(1), period);
        }
    }
}
