// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>
/// An <see cref="ILogger"/> that enables nothing and counts how often it was asked. Its <c>Log</c> throws
/// <see cref="InvalidOperationException"/>: a caller that logs to a disabled logger is wrong.
/// </summary>
public sealed class DisabledLogger : ILogger
{
    private int _isEnabledCalls;

    /// <summary>How many times <see cref="IsEnabled"/> was called.</summary>
    public int IsEnabledCalls => Volatile.Read(ref _isEnabledCalls);

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        Interlocked.Increment(ref _isEnabledCalls);
        return false;
    }

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        throw new InvalidOperationException("Log was called on a logger that enables nothing.");
}
