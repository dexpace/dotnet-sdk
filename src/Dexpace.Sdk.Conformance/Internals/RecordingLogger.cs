// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The kit's own recording <see cref="ILogger"/> (P8a-23: the kit cannot take the unpacked <c>Dexpace.Sdk.TestSupport</c>).
/// A subject hands it to its transport through <c>TransportSettings.Logger</c>, and assertions query what the transport
/// logged: which header drops were named, and that no value was ever logged (XCUT-18, <c>TRANSPORT-11</c>,
/// <c>TRANSPORT-13</c>). Thread-safe; every level is enabled.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly object _lock = new();
    private readonly List<LogEntry> _entries = [];

    /// <summary>A snapshot of every entry logged so far, in order.</summary>
    internal IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>The entries that name <paramref name="headerName"/>, case-insensitively, in their message or their structured state.</summary>
    /// <param name="headerName">The header name that was dropped.</param>
    internal IReadOnlyList<LogEntry> Dropped(string headerName) =>
        [.. Entries.Where(entry => entry.Mentions(headerName))];

    /// <summary>Whether any entry carries <paramref name="text"/>, case-sensitively, in its message or any structured value: the "no value was ever logged" check.</summary>
    /// <param name="text">The text that must never be logged.</param>
    internal bool Leaks(string text) => Entries.Any(entry => entry.Contains(text, StringComparison.Ordinal));

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => NoopScope.Instance;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var pairs = state is IEnumerable<KeyValuePair<string, object?>> structured ? [.. structured] : (IReadOnlyList<KeyValuePair<string, object?>>)[];
        var entry = new LogEntry(logLevel, eventId, formatter(state, exception), pairs);
        lock (_lock)
        {
            _entries.Add(entry);
        }
    }

    private sealed class NoopScope : IDisposable
    {
        internal static NoopScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

/// <summary>One entry a <see cref="RecordingLogger"/> recorded.</summary>
/// <param name="Level">The level.</param>
/// <param name="Id">The event ID.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="State">The structured state's name and value pairs; empty for an unstructured entry.</param>
internal sealed record LogEntry(LogLevel Level, EventId Id, string Message, IReadOnlyList<KeyValuePair<string, object?>> State)
{
    /// <summary>Whether <paramref name="text"/> occurs in the message or in any structured value, under <paramref name="comparison"/>.</summary>
    internal bool Contains(string text, StringComparison comparison) =>
        Message.Contains(text, comparison)
        || State.Any(pair => Convert.ToString(pair.Value, CultureInfo.InvariantCulture)?.Contains(text, comparison) == true);

    /// <summary>Whether the entry names <paramref name="headerName"/> (case-insensitively) in its message or structured values.</summary>
    internal bool Mentions(string headerName) => Contains(headerName, StringComparison.OrdinalIgnoreCase);
}
