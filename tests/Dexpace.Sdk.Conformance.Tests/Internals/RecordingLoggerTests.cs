// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Internals;

[Trait("Category", "Unit")]
public sealed class RecordingLoggerTests
{
    private static readonly Action<ILogger, string, Exception?> s_dropped = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(1, "Dropped"),
        "Dropped the '{HeaderName}' header.");

    [Fact]
    public void It_records_level_event_message_and_structured_state_in_order()
    {
        var logger = new RecordingLogger();

        s_dropped(logger, "X-One", null);
        logger.Log(LogLevel.Warning, new EventId(2), "plain", null, (state, _) => state);

        var entries = logger.Entries;
        Assert.Equal(2, entries.Count);
        Assert.Equal(LogLevel.Debug, entries[0].Level);
        Assert.Equal(1, entries[0].Id.Id);
        Assert.Equal("Dropped the 'X-One' header.", entries[0].Message);
        Assert.Contains(entries[0].State, pair => pair.Key == "HeaderName" && (string?)pair.Value == "X-One");
        Assert.Equal(LogLevel.Warning, entries[1].Level);
        Assert.Empty(entries[1].State);
    }

    [Fact]
    public void Entries_is_a_snapshot()
    {
        var logger = new RecordingLogger();
        s_dropped(logger, "A", null);
        var before = logger.Entries;

        s_dropped(logger, "B", null);

        Assert.Single(before);
        Assert.Equal(2, logger.Entries.Count);
    }

    [Fact]
    public void Every_level_is_enabled_and_a_scope_is_a_no_op()
    {
        var logger = new RecordingLogger();

        Assert.All(Enum.GetValues<LogLevel>(), level => Assert.True(logger.IsEnabled(level)));
        using var scope = logger.BeginScope("anything");
        Assert.NotNull(scope);
    }

    [Fact]
    public void Dropped_finds_a_header_by_name_case_insensitively_and_ignores_others()
    {
        var logger = new RecordingLogger();
        s_dropped(logger, "X-One", null);
        s_dropped(logger, "X-Two", null);

        Assert.Single(logger.Dropped("x-one"));
        Assert.Single(logger.Dropped("X-TWO"));
        Assert.Empty(logger.Dropped("X-Three"));
    }

    [Fact]
    public void Leaks_finds_a_value_in_the_message_or_in_structured_state_and_is_case_sensitive()
    {
        var logger = new RecordingLogger();
        KeyValuePair<string, object?>[] state = [new("Secret", "evil.example")];
        logger.Log(LogLevel.Information, new EventId(3), state, null, (_, _) => "no value here");

        Assert.True(logger.Leaks("evil.example"));
        Assert.False(logger.Leaks("EVIL.EXAMPLE"));
        Assert.False(logger.Leaks("9999"));
    }

    [Fact]
    public async Task It_is_safe_under_concurrent_logging()
    {
        var logger = new RecordingLogger();

        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            var name = string.Concat("H", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (var n = 0; n < 100; n++)
            {
                s_dropped(logger, name, null);
            }
        }, TestContext.Current.CancellationToken)));

        Assert.Equal(3200, logger.Entries.Count);
    }
}
