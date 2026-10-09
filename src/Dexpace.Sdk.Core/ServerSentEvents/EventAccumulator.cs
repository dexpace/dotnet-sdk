// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>
/// The grammar of one block of lines (position A of the phase 7b design): it takes decoded lines, one at a time, and
/// returns an event when a blank line ends a block that set at least one field (SSE-1, SSE-3 to SSE-11, SSE-13, SSE-14).
/// </summary>
/// <remarks>
/// <para>
/// Pure: it reads no stream and holds no state across a dispatch (SSE-16). A line is, in order, a blank line (dispatch
/// when any field was seen, then reset), a comment (a leading colon; the text after one stripped space, latest wins, and
/// a field seen), or a field split at its first colon with exactly one leading U+0020 stripped from the value (SSE-3,
/// SSE-5). Only <c>data</c>, <c>event</c>, <c>id</c> and <c>retry</c> are interpreted, compared ordinally and
/// case-sensitively (SSE-7); a <c>data</c> value is appended unjoined (SSE-8), <c>event</c> is stored raw and latest wins
/// (SSE-10), an <c>id</c> holding U+0000 is dropped entirely (SSE-9), and a <c>retry</c> that is not a valid
/// <see cref="RetryField"/> value is dropped (SSE-11). Only a field that is <i>set</i> marks the block as seen, so a block
/// of ignored fields is skipped (SSE-13).
/// </para>
/// <para>
/// The accumulator never names a default event type or a stream sentinel (SSE-10, SSE-37).
/// </para>
/// </remarks>
internal sealed class EventAccumulator
{
    private readonly List<string> _data = [];
    private string? _id;
    private string? _event;
    private string? _comment;
    private TimeSpan? _retry;
    private bool _seen;

    /// <summary>Takes one decoded line, without its terminator.</summary>
    /// <param name="line">The line; the empty string is the blank line that ends a block.</param>
    /// <returns>The event the line completed, or <see langword="null"/> when it completed none.</returns>
    internal ServerSentEvent? Accept(string line)
    {
        if (line.Length == 0)
        {
            return Dispatch();
        }

        if (line[0] == ':')
        {
            _comment = line[ValueStart(line, 1)..];
            _seen = true;
            return null;
        }

        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var name = colon < 0 ? line.AsSpan() : line.AsSpan(0, colon);
        var valueStart = colon < 0 ? line.Length : ValueStart(line, colon + 1);
        Apply(name, line, valueStart);
        return null;
    }

    /// <summary>Ends the stream: dispatches a pending block, if any (SSE-14).</summary>
    /// <returns>The pending event, or <see langword="null"/> when nothing was pending.</returns>
    internal ServerSentEvent? Flush() => Dispatch();

    // One leading U+0020 is stripped; a second, a tab or any other character is content (SSE-5).
    private static int ValueStart(string line, int afterColon) =>
        afterColon < line.Length && line[afterColon] == ' ' ? afterColon + 1 : afterColon;

    private void Apply(ReadOnlySpan<char> name, string line, int valueStart)
    {
        switch (name)
        {
            case "data":
                _data.Add(line[valueStart..]);
                _seen = true;
                break;
            case "event":
                _event = line[valueStart..];
                _seen = true;
                break;
            case "id":
                SetId(line[valueStart..]);
                break;
            case "retry":
                if (RetryField.TryParse(line[valueStart..], out var retry))
                {
                    _retry = retry;
                    _seen = true;
                }

                break;
            default:
                // An unknown name sets no state and is not a field seen (SSE-7).
                break;
        }
    }

    private void SetId(string value)
    {
        // A NUL makes the whole field ignored: no state, not a field seen, no overwrite of an earlier id (SSE-9).
        if (value.Contains('\0', StringComparison.Ordinal))
        {
            return;
        }

        _id = value;
        _seen = true;
    }

    private ServerSentEvent? Dispatch()
    {
        if (!_seen)
        {
            return null;
        }

        var dispatched = new ServerSentEvent { Id = _id, Event = _event, Data = _data, Comment = _comment, Retry = _retry };
        _data.Clear();
        _id = null;
        _event = null;
        _comment = null;
        _retry = null;
        _seen = false;
        return dispatched;
    }
}
