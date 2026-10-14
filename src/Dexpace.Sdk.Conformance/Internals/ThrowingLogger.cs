// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A log sink that fails every time it is touched, so an assertion can prove that observability never throws into the caller's
/// request path (<c>XCUT-20</c>). It counts the touches, so an assertion can tell a pipeline that survived the failures from one
/// that never logged at all.
/// </summary>
internal sealed class ThrowingLogger : ILogger
{
    private int _touches;

    /// <summary>How many times the sink was touched (a scope opened or an event written).</summary>
    internal int Touches => Volatile.Read(ref _touches);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        Interlocked.Increment(ref _touches);
        throw new InvalidOperationException("the log sink failed to open a scope");
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Interlocked.Increment(ref _touches);
        throw new InvalidOperationException("the log sink failed to write an event");
    }
}
