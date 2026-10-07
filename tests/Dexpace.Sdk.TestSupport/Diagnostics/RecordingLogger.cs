// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>
/// An <see cref="ILogger"/> that records every call: level, event id, the structured state's pairs, the exception, the
/// rendered message, <see cref="Activity.Current"/> and the open scopes. Scopes live in an <see cref="AsyncLocal{T}"/>,
/// so a scope opened by a caller is visible at a log call that runs after the transport completed on another thread.
/// </summary>
public sealed class RecordingLogger : ILogger
{
    private readonly AsyncLocal<ImmutableStack<object>> _scopes = new();
    private readonly List<RecordedLog> _entries = [];
    private readonly Lock _gate = new();

    /// <summary>The lowest level <see cref="IsEnabled"/> accepts; everything by default.</summary>
    public LogLevel MinimumLevel { get; init; } = LogLevel.Trace;

    /// <summary>The calls recorded so far, in order.</summary>
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
        where TState : notnull
    {
        var previous = _scopes.Value ?? ImmutableStack<object>.Empty;
        _scopes.Value = previous.Push(state);
        return new Scope(this, previous);
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

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
        var scopes = (_scopes.Value ?? ImmutableStack<object>.Empty).Reverse().ToList();
        var entry = new RecordedLog(logLevel, eventId, pairs, exception, formatter(state, exception), Activity.Current, scopes);
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    private sealed class Scope(RecordingLogger owner, ImmutableStack<object> previous) : IDisposable
    {
        public void Dispose() => owner._scopes.Value = previous;
    }
}
