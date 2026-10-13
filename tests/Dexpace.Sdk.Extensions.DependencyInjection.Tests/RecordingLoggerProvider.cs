// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>Records the category and message of every log entry the container's logging produces.</summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly List<(string Category, LogLevel Level, string Message)> _entries = [];

    internal IReadOnlyList<(string Category, LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, this);

    public void Dispose()
    {
    }

    private sealed class Recorder(string category, RecordingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (owner._entries)
            {
                owner._entries.Add((category, logLevel, formatter(state, exception)));
            }
        }
    }
}
