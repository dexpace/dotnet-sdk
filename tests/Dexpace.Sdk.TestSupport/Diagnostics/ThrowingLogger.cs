// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>
/// An <see cref="ILogger"/> that throws on demand: from <see cref="IsEnabled"/>, from every <c>Log</c>, or from only the
/// first <c>n</c> <c>Log</c> calls. Every <c>Log</c> attempt is recorded in <see cref="Attempts"/> before it throws;
/// <see cref="Entries"/> holds those that returned normally.
/// </summary>
public sealed class ThrowingLogger : ILogger
{
    private readonly List<RecordedLog> _attempts = [];
    private readonly List<RecordedLog> _entries = [];
    private readonly Lock _gate = new();
    private int _logCalls;

    /// <summary>Whether <see cref="IsEnabled"/> throws.</summary>
    public bool ThrowOnIsEnabled { get; init; }

    /// <summary>Whether every <c>Log</c> call throws.</summary>
    public bool ThrowOnLog { get; init; }

    /// <summary>How many of the first <c>Log</c> calls throw before calls start succeeding; zero for none.</summary>
    public int ThrowOnLogFirst { get; init; }

    /// <summary>Creates the exception to throw; an <see cref="InvalidOperationException"/> by default.</summary>
    public Func<Exception> ExceptionFactory { get; init; } = static () => new InvalidOperationException("The logger threw.");

    /// <summary>Every <c>Log</c> call, including those that threw.</summary>
    public IReadOnlyList<RecordedLog> Attempts
    {
        get
        {
            lock (_gate)
            {
                return [.. _attempts];
            }
        }
    }

    /// <summary>The <c>Log</c> calls that returned normally.</summary>
    public IReadOnlyList<RecordedLog> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) =>
        ThrowOnIsEnabled ? throw ExceptionFactory() : true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var pairs = state is IEnumerable<KeyValuePair<string, object?>> list ? [.. list] : new List<KeyValuePair<string, object?>>();
        var entry = new RecordedLog(logLevel, eventId, pairs, exception, formatter(state, exception), Activity.Current, []);
        bool fail;
        lock (_gate)
        {
            _attempts.Add(entry);
            fail = ThrowOnLog || Interlocked.Increment(ref _logCalls) <= ThrowOnLogFirst;
            if (!fail)
            {
                _entries.Add(entry);
            }
        }

        if (fail)
        {
            throw ExceptionFactory();
        }
    }
}
