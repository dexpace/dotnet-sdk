// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>
/// Reads server-sent events from a byte stream, one event per pull (SSE-1 to SSE-19, SSE-39, SSE-40).
/// </summary>
/// <remarks>
/// <para>
/// <b>What it parses.</b> The line layer is the SDK's own <c>Utf8LineReader</c> in its WHATWG mode (LF, CR and CRLF each end a
/// line, a CR ends it at once without waiting for a following LF, SSE-2; malformed UTF-8 decodes to U+FFFD), and the
/// grammar above it is the block grammar of the chapter, not the strict WHATWG one: a comment, an id-only block or a
/// retry-only block is an event, an absent event name is <see langword="null"/> and never <c>message</c>, and no
/// last-event-id is carried from one event to the next (SSE-6, SSE-10, SSE-13, SSE-16). A leading BOM is consumed once
/// (SSE-12); its three bytes count against the first line's cap.
/// </para>
/// <para>
/// <b>Ownership (SSE-17).</b> The reader never owns, closes or disposes <c>source</c>: it has nothing to dispose, which is
/// why it is not <see cref="IDisposable"/>. The caller, or the <c>ServerSentEventStream</c> that owns a response,
/// releases the stream.
/// </para>
/// <para>
/// <b>Threading (SSE-18).</b> A reader is single-threaded by contract, the same as the line reader under it: one consumer
/// at a time, and two threads pulling one reader is undefined. The one sanctioned cross-thread call in the SSE types is
/// the facade's close (SSE-31).
/// </para>
/// <para>
/// <b>Pull-based (SSE-39).</b> The source is read only inside a pull, and only as far as the next event needs. The 4 KiB
/// read buffer is a read-ahead of bytes, never of events.
/// </para>
/// <para>
/// <b>The line cap (SSE-19, issue #10).</b> A line over <c>maxLineBytes</c> content bytes (default
/// <see cref="DefaultMaxLineBytes"/>, 1 MiB) throws <see cref="ServerSentEventLineTooLongException"/> and the reader stays
/// failed: every later pull throws it again, because the source is mid-line. <b>Residual R1:</b> the cap bounds a line,
/// not a block. Lines that each fit are accumulated into the pending event without a limit until a blank line ends it, so a
/// hostile server can still grow a block; bound a stream from an untrusted server with a cancellation token or
/// <c>OverallTimeout</c> (P7b-5).
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1001", Justification = "SSE-17: the reader never owns its source, so there is nothing to dispose.")]
public sealed class ServerSentEventReader
{
    /// <summary>The default cap on one line: 1 MiB of content bytes, the terminator excluded (design §10 entry 20).</summary>
    public const int DefaultMaxLineBytes = 1_048_576;

    private readonly Utf8LineReader _lines;
    private readonly EventAccumulator _accumulator = new();
    private readonly int _maxLineBytes;
    private bool _bomChecked;

    /// <summary>Initializes a reader over <paramref name="source"/>, which it never closes (SSE-17).</summary>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <param name="maxLineBytes">The most content bytes one line may hold; positive (SSE-19).</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> is not positive.</exception>
    public ServerSentEventReader(Stream source, int maxLineBytes = DefaultMaxLineBytes)
    {
        _lines = new Utf8LineReader(source, LineTerminators.Whatwg, maxLineBytes, leaveOpen: true);
        _maxLineBytes = maxLineBytes;
    }

    /// <summary>Reads the next event, or <see langword="null"/> once the stream has ended (SSE-14, SSE-15).</summary>
    /// <remarks>
    /// At the end of the stream a pending block is dispatched as one last event; after that every call returns
    /// <see langword="null"/>. A read error surfaces here, at the pull that hit it.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the underlying read.</param>
    /// <returns>The next event, or <see langword="null"/> at the end of the stream.</returns>
    /// <exception cref="ServerSentEventLineTooLongException">A line exceeded the cap; the reader stays failed.</exception>
    public async ValueTask<ServerSentEvent?> ReadNextAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            string? line;
            try
            {
                line = await _lines.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidDataException ex) when (_lines.IsFailed)
            {
                throw TooLong(ex);
            }

            if (line is null)
            {
                return _accumulator.Flush();
            }

            if (_accumulator.Accept(StripBom(line)) is { } dispatched)
            {
                return dispatched;
            }
        }
    }

    /// <summary>Reads the next event synchronously, or <see langword="null"/> once the stream has ended.</summary>
    /// <remarks>
    /// The synchronous twin of <see cref="ReadNextAsync"/>. It takes no token because <see cref="Stream.Read(byte[], int, int)"/>
    /// has none.
    /// </remarks>
    /// <returns>The next event, or <see langword="null"/> at the end of the stream.</returns>
    /// <exception cref="ServerSentEventLineTooLongException">A line exceeded the cap; the reader stays failed.</exception>
    public ServerSentEvent? ReadNext()
    {
        while (true)
        {
            string? line;
            try
            {
                line = _lines.ReadLine();
            }
            catch (InvalidDataException ex) when (_lines.IsFailed)
            {
                throw TooLong(ex);
            }

            if (line is null)
            {
                return _accumulator.Flush();
            }

            if (_accumulator.Accept(StripBom(line)) is { } dispatched)
            {
                return dispatched;
            }
        }
    }

    // A decoded line starts with U+FEFF only when the stream's first three bytes were EF BB BF (UTF-8 is prefix-free and
    // the replacement fallback never emits U+FEFF), so this string-level strip equals the specification's byte-level
    // lookahead (P7b-3, fact 2). The flag is set by the first line ever read, whether or not it held a BOM, so a BOM on
    // any later line stays in the line and makes it an unknown field (SSE-12).
    private string StripBom(string line)
    {
        if (_bomChecked)
        {
            return line;
        }

        _bomChecked = true;
        return line.Length > 0 && line[0] == '﻿' ? line[1..] : line;
    }

    private ServerSentEventLineTooLongException TooLong(InvalidDataException inner) => new(_maxLineBytes, inner);
}
