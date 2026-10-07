// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections;

namespace Dexpace.Sdk.Core.Diagnostics;

/// <summary>
/// The structured state of the header-bearing HTTP events: a read-only list of key/value pairs built only after the level
/// and <c>IsEnabled</c> checks have passed (P5b-3, OBS-1). The event carries a dynamic key set (one key per logged
/// header), which neither the <c>[LoggerMessage]</c> generator nor <c>LoggerMessage.Define</c> can express.
/// </summary>
/// <remarks>
/// The last pair is <c>{OriginalFormat}</c>, the convention that lets template-grouping sinks group the event. The message
/// the formatter returns is precomputed from non-sensitive fields only (method, redacted URL, status or error type); it
/// never contains a header value or a body preview.
/// </remarks>
internal sealed class HttpLogRecord : IReadOnlyList<KeyValuePair<string, object?>>
{
    private const string OriginalFormatKey = "{OriginalFormat}";

    private readonly KeyValuePair<string, object?>[] _pairs;
    private readonly string _message;

    private static readonly Func<HttpLogRecord, Exception?, string> s_formatter = static (record, _) => record._message;

    internal HttpLogRecord(List<KeyValuePair<string, object?>> pairs, string template, string message)
    {
        _pairs = new KeyValuePair<string, object?>[pairs.Count + 1];
        pairs.CopyTo(_pairs);
        _pairs[^1] = new KeyValuePair<string, object?>(OriginalFormatKey, template);
        _message = message;
    }

    /// <summary>The formatter handed to <c>ILogger.Log</c>; cached so the call allocates no delegate.</summary>
    internal static Func<HttpLogRecord, Exception?, string> Formatter => s_formatter;

    /// <inheritdoc/>
    public int Count => _pairs.Length;

    /// <inheritdoc/>
    public KeyValuePair<string, object?> this[int index] =>
        (uint)index < (uint)_pairs.Length ? _pairs[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() =>
        ((IEnumerable<KeyValuePair<string, object?>>)_pairs).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public override string ToString() => _message;
}
