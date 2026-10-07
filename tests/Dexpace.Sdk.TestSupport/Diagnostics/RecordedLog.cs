// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>One call to <see cref="ILogger.Log{TState}"/> as a logger double saw it.</summary>
/// <param name="Level">The log level.</param>
/// <param name="EventId">The event id and name.</param>
/// <param name="State">The structured state's key/value pairs, copied at the call (empty when the state was not a pair list).</param>
/// <param name="Exception">The exception argument.</param>
/// <param name="Message">The formatter's rendering of the state and exception.</param>
/// <param name="Activity"><see cref="System.Diagnostics.Activity.Current"/> at the call.</param>
/// <param name="Scopes">The scope states open at the call, outermost first.</param>
public sealed record RecordedLog(
    LogLevel Level,
    EventId EventId,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    Exception? Exception,
    string Message,
    Activity? Activity,
    IReadOnlyList<object> Scopes)
{
    /// <summary>The value of the first state pair named <paramref name="key"/>.</summary>
    /// <param name="key">The state key.</param>
    /// <returns>The value (possibly <see langword="null"/>).</returns>
    /// <exception cref="KeyNotFoundException">No pair has that key.</exception>
    public object? this[string key] =>
        State.Where(p => p.Key == key).Select(p => (true, p.Value)).FirstOrDefault() is (true, var value)
            ? value
            : throw new KeyNotFoundException($"The log state has no '{key}' key.");

    /// <summary>Whether the state has a pair named <paramref name="key"/>.</summary>
    /// <param name="key">The state key.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool Has(string key) => State.Any(p => p.Key == key);
}
