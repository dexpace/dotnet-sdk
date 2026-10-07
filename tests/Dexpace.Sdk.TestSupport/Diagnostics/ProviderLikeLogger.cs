// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>
/// An <see cref="ILogger"/> that renders like a real provider does: inside <c>Log</c> it formats the state and reads the
/// exception's <see cref="object.ToString"/> and <see cref="Exception.Message"/>, and only then records. An exception
/// whose rendering throws therefore throws out of <c>Log</c>, which is where a real console or file sink fails.
/// </summary>
public sealed class ProviderLikeLogger : ILogger
{
    private readonly List<RecordedLog> _entries = [];
    private readonly Lock _gate = new();

    /// <summary>The calls recorded so far (those whose rendering did not throw).</summary>
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
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var message = formatter(state, exception);
        _ = exception?.ToString();
        _ = exception?.Message;
        var pairs = state is IEnumerable<KeyValuePair<string, object?>> list ? [.. list] : new List<KeyValuePair<string, object?>>();
        lock (_gate)
        {
            _entries.Add(new RecordedLog(logLevel, eventId, pairs, exception, message, Activity.Current, []));
        }
    }
}
