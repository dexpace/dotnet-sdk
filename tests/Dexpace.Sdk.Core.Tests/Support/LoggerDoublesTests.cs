// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Support;

/// <summary>The logger doubles behave as the suites that use them assume.</summary>
[Trait("Category", "Unit")]
public sealed class LoggerDoublesTests
{
    private static readonly KeyValuePair<string, object?>[] s_state = [new("k", 1), new("n", null)];

    private static void Write(ILogger logger, Exception? exception = null) =>
        logger.Log(LogLevel.Information, new EventId(7, "e"), s_state, exception, static (s, _) => $"{s.Length} pairs");

    [Fact]
    public void RecordingLogger_copies_state_and_captures_the_current_activity()
    {
        var logger = new RecordingLogger();
        using var activity = new Activity("op");
        activity.Start();
        using (logger.BeginScope("scope-1"))
        {
            Write(logger);
        }

        Write(logger);

        var first = logger.Entries[0];
        Assert.Equal(7, first.EventId.Id);
        Assert.Equal(1, first["k"]);
        Assert.Null(first["n"]);
        Assert.True(first.Has("n"));
        Assert.False(first.Has("missing"));
        Assert.Equal("2 pairs", first.Message);
        Assert.Same(activity, first.Activity);
        Assert.Equal(["scope-1"], first.Scopes);
        Assert.Empty(logger.Entries[1].Scopes);
        Assert.Throws<KeyNotFoundException>(() => first["missing"]);
    }

    [Fact]
    public void RecordingLogger_honours_MinimumLevel()
    {
        var logger = new RecordingLogger { MinimumLevel = LogLevel.Warning };

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public void ThrowingLogger_throws_for_the_first_n_calls_only()
    {
        var logger = new ThrowingLogger { ThrowOnLogFirst = 2 };

        Assert.Throws<InvalidOperationException>(() => Write(logger));
        Assert.Throws<InvalidOperationException>(() => Write(logger));
        Write(logger);

        Assert.Equal(3, logger.Attempts.Count);
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void ThrowingLogger_can_throw_from_IsEnabled_and_from_every_Log()
    {
        Assert.Throws<InvalidOperationException>(() => new ThrowingLogger { ThrowOnIsEnabled = true }.IsEnabled(LogLevel.Error));
        var always = new ThrowingLogger { ThrowOnLog = true };
        Assert.Throws<InvalidOperationException>(() => Write(always));
        Assert.Throws<InvalidOperationException>(() => Write(always));
        Assert.Equal(2, always.Attempts.Count);
        Assert.Empty(always.Entries);
    }

    [Fact]
    public void ProviderLikeLogger_renders_the_exception_inside_Log()
    {
        var logger = new ProviderLikeLogger();

        Assert.Throws<InvalidOperationException>(() => Write(logger, new MessageThrows()));
        Assert.Empty(logger.Entries);

        Write(logger, new InvalidOperationException("fine"));
        Assert.Single(logger.Entries);
    }

    [Fact]
    public void DisabledLogger_counts_IsEnabled_calls_and_never_accepts_a_log()
    {
        var logger = new DisabledLogger();

        Assert.False(logger.IsEnabled(LogLevel.Critical));
        Assert.False(logger.IsEnabled(LogLevel.Trace));
        Assert.Equal(2, logger.IsEnabledCalls);
        Assert.Throws<InvalidOperationException>(() => Write(logger));
    }

    private sealed class MessageThrows : Exception
    {
        public override string Message => throw new InvalidOperationException("Message getter threw.");
    }
}
