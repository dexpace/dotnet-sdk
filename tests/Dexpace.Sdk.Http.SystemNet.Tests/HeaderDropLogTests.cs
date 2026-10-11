// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-13, OBS-19, OBS-20, XCUT-18 (phase 8b, P8b-12, P8b-13): how a dropped header is logged. A drop is never silent: the
/// policy only chooses the level, <see cref="HeaderDropLogging.FirstPerName"/> warning once per name and then going quiet to
/// <see cref="LogLevel.Debug"/>. The names already seen are a bounded, per-instance set; the framing and inbound drops are always
/// <see cref="LogLevel.Debug"/> and never touch it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HeaderDropLogTests
{
    private static LogLevel[] LevelsOf(RecordingLogger logger) => [.. logger.Entries.Select(entry => entry.Level)];

    [Fact]
    public void FirstPerName_warns_for_the_first_drop_of_a_name_and_goes_quiet_after_ignoring_case()
    {
        var logger = new RecordingLogger();
        var log = new HeaderDropLog(logger, HeaderDropLogging.FirstPerName);

        log.Dropped("X-A");
        log.Dropped("X-A");
        log.Dropped("x-a");
        log.Dropped("X-B");

        Assert.Equal([LogLevel.Warning, LogLevel.Debug, LogLevel.Debug, LogLevel.Warning], LevelsOf(logger));
    }

    [Fact]
    public void Every_warns_each_time()
    {
        var logger = new RecordingLogger();
        var log = new HeaderDropLog(logger, HeaderDropLogging.Every);

        log.Dropped("X-A");
        log.Dropped("X-A");

        Assert.Equal([LogLevel.Warning, LogLevel.Warning], LevelsOf(logger));
    }

    [Fact]
    public void VerboseOnly_is_debug_each_time_and_never_a_warning_but_never_silent()
    {
        var logger = new RecordingLogger();
        var log = new HeaderDropLog(logger, HeaderDropLogging.VerboseOnly);

        log.Dropped("X-A");
        log.Dropped("X-B");

        Assert.Equal([LogLevel.Debug, LogLevel.Debug], LevelsOf(logger));
    }

    [Fact]
    public void The_three_drops_use_their_own_event_ids_and_names()
    {
        var logger = new RecordingLogger();
        var log = new HeaderDropLog(logger, HeaderDropLogging.Every);

        log.Dropped("X-A");
        log.FramingDropped("Host");
        log.InboundDropped("X-Control");

        Assert.Equal(
            [
                (110, "http.transport.header_dropped", LogLevel.Warning),
                (111, "http.transport.framing_header_dropped", LogLevel.Debug),
                (112, "http.transport.inbound_header_dropped", LogLevel.Debug),
            ],
            logger.Entries.Select(entry => (entry.EventId.Id, entry.EventId.Name!, entry.Level)));
        Assert.Equal(DexpaceLogEvents.TransportHeaderDroppedId, logger.Entries[0].EventId.Id);
    }

    [Theory]
    [InlineData(HeaderDropLogging.FirstPerName)]
    [InlineData(HeaderDropLogging.Every)]
    [InlineData(HeaderDropLogging.VerboseOnly)]
    public void Framing_and_inbound_drops_are_always_debug_and_never_touch_the_seen_set(HeaderDropLogging mode)
    {
        var logger = new RecordingLogger();
        var log = new HeaderDropLog(logger, mode);

        log.FramingDropped("Host");
        log.FramingDropped("Host");
        log.InboundDropped("X-Control");
        log.InboundDropped("X-Control");

        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Debug, entry.Level));
        Assert.Equal(0, log.SeenCount);

        // And the policy still treats the name as new afterwards.
        log.Dropped("Host");
        Assert.Equal(mode == HeaderDropLogging.VerboseOnly ? LogLevel.Debug : LogLevel.Warning, logger.Entries[^1].Level);
    }

    [Fact]
    public void The_seen_set_is_bounded_at_128_names_and_evicts_the_oldest_first()
    {
        var logger = new RecordingLogger();
        var log = new HeaderDropLog(logger, HeaderDropLogging.FirstPerName);

        for (var i = 0; i < 10_000; i++)
        {
            log.Dropped("X-" + i);
        }

        Assert.Equal(128, log.SeenCount);

        // The newest 128 are still remembered: a repeat is Debug. The first name was evicted long ago: it warns again.
        var before = logger.Entries.Count;
        log.Dropped("X-9999");
        Assert.Equal(LogLevel.Debug, logger.Entries[before].Level);
        log.Dropped("X-0");
        Assert.Equal(LogLevel.Warning, logger.Entries[before + 1].Level);
        Assert.Equal(128, log.SeenCount);
    }

    [Fact]
    public void Concurrent_first_drops_of_one_name_warn_exactly_once()
    {
        var logger = new RecordingLogger();
        var log = new HeaderDropLog(logger, HeaderDropLogging.FirstPerName);
        using var barrier = new Barrier(64);

        Parallel.For(0, 64, _ =>
        {
            barrier.SignalAndWait(TestContext.Current.CancellationToken);
            log.Dropped("X-Race");
        });

        Assert.Equal(1, logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
        Assert.Equal(63, logger.Entries.Count(entry => entry.Level == LogLevel.Debug));
    }

    [Fact]
    public void Many_threads_over_many_names_never_corrupt_the_set()
    {
        var log = new HeaderDropLog(new RecordingLogger(), HeaderDropLogging.FirstPerName);

        var thrown = Record.Exception(() => Parallel.For(0, 64, worker =>
        {
            for (var i = 0; i < 1_000; i++)
            {
                log.Dropped("X-" + ((worker * 31) + i) % 1_000);
            }
        }));

        Assert.Null(thrown);
        Assert.InRange(log.SeenCount, 1, 128);
    }

    [Fact]
    public void A_name_with_a_control_character_is_logged_escaped_and_no_value_is_ever_an_argument()
    {
        var logger = new RecordingLogger();
        var log = new HeaderDropLog(logger, HeaderDropLogging.Every);

        log.Dropped("X-Bad\r\nInjected");

        var entry = Assert.Single(logger.Entries);
        Assert.DoesNotContain('\r', entry.Message);
        Assert.DoesNotContain('\n', entry.Message);
        Assert.Contains("\\u000D", entry.Message, StringComparison.Ordinal);
        Assert.Equal(["HeaderName"], entry.State.Select(pair => pair.Key).Where(key => key != "{OriginalFormat}"));
    }

    [Fact]
    public void A_logger_that_throws_is_swallowed_and_the_drop_call_returns()
    {
        var logger = new ThrowingLogger { ThrowOnLog = true };
        var log = new HeaderDropLog(logger, HeaderDropLogging.Every);

        var thrown = Record.Exception(() =>
        {
            log.Dropped("X-A");
            log.FramingDropped("Host");
            log.InboundDropped("X-C");
        });

        Assert.Null(thrown);
        Assert.Equal(3, logger.Attempts.Count);
    }

    [Fact]
    public void A_logger_that_enables_nothing_pays_for_no_formatting()
    {
        var logger = new DisabledLogger();
        var log = new HeaderDropLog(logger, HeaderDropLogging.Every);

        var thrown = Record.Exception(() => log.Dropped("X-A"));

        Assert.Null(thrown);
        Assert.True(logger.IsEnabledCalls > 0);
    }
}
