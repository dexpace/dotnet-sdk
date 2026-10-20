// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
/// why it is not <see cref="IDisposable"/>. The caller, or the <see cref="ServerSentEventStream"/> that owns a response,
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
/// <b>The caps (SSE-19, issue #10, P11-19).</b> A line over <c>maxLineBytes</c> content bytes (default
/// <see cref="DefaultMaxLineBytes"/>, 1 MiB) throws <see cref="ServerSentEventLineTooLongException"/> and the reader stays
/// failed: every later pull throws it again, because the source is mid-line. The cap bounds a line, not an event, so a second
/// cap bounds the lines of one event (residual R1, closed by phase 11): the <c>data</c> lines of an event cost their UTF-8
/// value bytes plus one each, and an event whose data passes <c>maxEventBytes</c> (default <see cref="DefaultMaxEventBytes"/>,
/// 8 MiB) throws <see cref="ServerSentEventTooLargeException"/> at the line that passed it and stays failed the same way.
/// Only <c>data</c> accumulates, so no other field counts. The read stops at the failing line: nothing further is pulled.
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1001", Justification = "SSE-17: the reader never owns its source, so there is nothing to dispose.")]
public sealed class ServerSentEventReader
{
    /// <summary>The default cap on one line: 1 MiB of content bytes, the terminator excluded (design §10 entry 20).</summary>
    public const int DefaultMaxLineBytes = 1_048_576;

    /// <summary>
    /// The default cap on the data of one event: 8 MiB, eight lines of the line cap. A <c>data</c> line costs its UTF-8 value bytes plus one for
    /// the line break that ends it (SSE-19, design §10 entry 20, P11-19).
    /// </summary>
    public const int DefaultMaxEventBytes = 8_388_608;

    private readonly Utf8LineReader _lines;
    private readonly EventAccumulator _accumulator;
    private readonly int _maxLineBytes;
    private readonly int _maxEventBytes;
    private bool _bomChecked;

    /// <summary>Initializes a reader over <paramref name="source"/> with the default caps, which it never closes (SSE-17).</summary>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    public ServerSentEventReader(Stream source)
        : this(source, DefaultMaxLineBytes, DefaultMaxEventBytes)
    {
    }

    /// <summary>Initializes a reader over <paramref name="source"/> with a line cap and the default event cap, which it never closes (SSE-17).</summary>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <param name="maxLineBytes">The most content bytes one line may hold; positive (SSE-19).</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> is not positive.</exception>
    public ServerSentEventReader(Stream source, int maxLineBytes)
        : this(source, maxLineBytes, DefaultMaxEventBytes)
    {
    }

    /// <summary>Initializes a reader over <paramref name="source"/> with both caps, which it never closes (SSE-17, SSE-19).</summary>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <param name="maxLineBytes">The most content bytes one line may hold; positive (SSE-19).</param>
    /// <param name="maxEventBytes">The most bytes of <c>data</c> one event may hold, each line costing its UTF-8 value bytes plus one; positive (SSE-19).</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> or <paramref name="maxEventBytes"/> is not positive.</exception>
    public ServerSentEventReader(Stream source, int maxLineBytes, int maxEventBytes)
    {
        _lines = new Utf8LineReader(source, LineTerminators.Whatwg, maxLineBytes, leaveOpen: true);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEventBytes);
        _maxLineBytes = maxLineBytes;
        _maxEventBytes = maxEventBytes;
        _accumulator = new EventAccumulator(maxEventBytes);
    }

    /// <summary>Reads the next event, or <see langword="null"/> once the stream has ended (SSE-14, SSE-15).</summary>
    /// <remarks>
    /// At the end of the stream a pending block is dispatched as one last event; after that every call returns
    /// <see langword="null"/>. A read error surfaces here, at the pull that hit it.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the underlying read.</param>
    /// <returns>The next event, or <see langword="null"/> at the end of the stream.</returns>
    /// <exception cref="ServerSentEventLineTooLongException">A line exceeded the cap; the reader stays failed.</exception>
    /// <exception cref="ServerSentEventTooLargeException">An event's data exceeded the event cap; the reader stays failed.</exception>
    public async ValueTask<ServerSentEvent?> ReadNextAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            FailIfEventTooLarge();
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
    /// <exception cref="ServerSentEventTooLargeException">An event's data exceeded the event cap; the reader stays failed.</exception>
    public ServerSentEvent? ReadNext()
    {
        while (true)
        {
            FailIfEventTooLarge();
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

    /// <summary>Walks every event of <paramref name="source"/> lazily and asynchronously (SSE-40).</summary>
    /// <remarks>
    /// <para>
    /// One reader is built per walk, at its first pull, so the BOM is consumed per stream; nothing is read before then. A
    /// read error surfaces at the pull that hit it, after the events already parsed. The sequence can be enumerated once:
    /// a second <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator"/> throws <see cref="InvalidOperationException"/> at the
    /// call. The enumeration token (<c>WithCancellation</c>) reaches every read.
    /// </para>
    /// <para>
    /// <b>Caller obligation (SSE-40):</b> do not call this twice over the same source; a second reader would start
    /// mid-stream and the first one's read-ahead bytes are lost to it. The sequence never closes <paramref name="source"/>
    /// (SSE-17).
    /// </para>
    /// </remarks>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <param name="maxLineBytes">The most content bytes one line may hold; positive (SSE-19).</param>
    /// <returns>A single-use, lazy sequence of the events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> is not positive.</exception>
    public static IAsyncEnumerable<ServerSentEvent> ReadAllAsync(Stream source, int maxLineBytes) =>
        ReadAllAsync(source, maxLineBytes, DefaultMaxEventBytes);

    /// <summary>Walks every event of <paramref name="source"/> lazily and asynchronously with the default caps (SSE-40).</summary>
    /// <remarks>The same walk as the two-argument form, with the line cap at <see cref="DefaultMaxLineBytes"/>.</remarks>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <returns>A single-use, lazy sequence of the events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    public static IAsyncEnumerable<ServerSentEvent> ReadAllAsync(Stream source) =>
        ReadAllAsync(source, DefaultMaxLineBytes, DefaultMaxEventBytes);

    /// <summary>Walks every event of <paramref name="source"/> lazily and asynchronously with both caps (SSE-19, SSE-40).</summary>
    /// <remarks>The same walk as the two-argument form; an event whose data passes the event cap throws <see cref="ServerSentEventTooLargeException"/> at that pull.</remarks>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <param name="maxLineBytes">The most content bytes one line may hold; positive (SSE-19).</param>
    /// <param name="maxEventBytes">The most bytes of <c>data</c> one event may hold; positive (SSE-19).</param>
    /// <returns>A single-use, lazy sequence of the events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> or <paramref name="maxEventBytes"/> is not positive.</exception>
    public static IAsyncEnumerable<ServerSentEvent> ReadAllAsync(Stream source, int maxLineBytes, int maxEventBytes)
    {
        Validate(source, maxLineBytes, maxEventBytes);
        return new SingleUseAsyncSequence<ServerSentEvent>(token => WalkAsync(source, maxLineBytes, maxEventBytes, token).GetAsyncEnumerator(token));
    }

    /// <summary>Walks every event of <paramref name="source"/> lazily and synchronously (SSE-40).</summary>
    /// <remarks>
    /// The blocking twin of <see cref="ReadAllAsync(Stream, int)"/>, with the same laziness, single-use rule, per-stream BOM and caller
    /// obligation; it reads with <see cref="Stream.Read(byte[], int, int)"/>.
    /// </remarks>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <param name="maxLineBytes">The most content bytes one line may hold; positive (SSE-19).</param>
    /// <returns>A single-use, lazy sequence of the events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> is not positive.</exception>
    public static IEnumerable<ServerSentEvent> ReadAll(Stream source, int maxLineBytes) =>
        ReadAll(source, maxLineBytes, DefaultMaxEventBytes);

    /// <summary>Walks every event of <paramref name="source"/> lazily and synchronously with the default caps (SSE-40).</summary>
    /// <remarks>The blocking twin of <see cref="ReadAllAsync(Stream)"/>.</remarks>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <returns>A single-use, lazy sequence of the events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    public static IEnumerable<ServerSentEvent> ReadAll(Stream source) =>
        ReadAll(source, DefaultMaxLineBytes, DefaultMaxEventBytes);

    /// <summary>Walks every event of <paramref name="source"/> lazily and synchronously with both caps (SSE-19, SSE-40).</summary>
    /// <remarks>The blocking twin of <see cref="ReadAllAsync(Stream, int, int)"/>.</remarks>
    /// <param name="source">A readable stream positioned at the start of the event stream.</param>
    /// <param name="maxLineBytes">The most content bytes one line may hold; positive (SSE-19).</param>
    /// <param name="maxEventBytes">The most bytes of <c>data</c> one event may hold; positive (SSE-19).</param>
    /// <returns>A single-use, lazy sequence of the events.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> or <paramref name="maxEventBytes"/> is not positive.</exception>
    public static IEnumerable<ServerSentEvent> ReadAll(Stream source, int maxLineBytes, int maxEventBytes)
    {
        Validate(source, maxLineBytes, maxEventBytes);
        return new SingleUseSequence<ServerSentEvent>(() => Walk(source, maxLineBytes, maxEventBytes).GetEnumerator());
    }

    // The arguments are checked eagerly, at the call, even though iteration is lazy: the public views are not iterators.
    private static void Validate(Stream source, int maxLineBytes, int maxEventBytes)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLineBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEventBytes);
        if (!source.CanRead)
        {
            throw new ArgumentException("The source stream must be readable.", nameof(source));
        }
    }

    private static async IAsyncEnumerable<ServerSentEvent> WalkAsync(
        Stream source,
        int maxLineBytes,
        int maxEventBytes,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var reader = new ServerSentEventReader(source, maxLineBytes, maxEventBytes);
        while (await reader.ReadNextAsync(cancellationToken).ConfigureAwait(false) is { } next)
        {
            yield return next;
        }
    }

    private static IEnumerable<ServerSentEvent> Walk(Stream source, int maxLineBytes, int maxEventBytes)
    {
        var reader = new ServerSentEventReader(source, maxLineBytes, maxEventBytes);
        while (reader.ReadNext() is { } next)
        {
            yield return next;
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

    // The accumulator drops the data line that passed the cap and raises its flag; checking it before every pull makes the failure land at
    // that line, stop the read there and stay (SSE-19, P11-19).
    private void FailIfEventTooLarge()
    {
        if (_accumulator.OverCap)
        {
            throw new ServerSentEventTooLargeException(_maxEventBytes);
        }
    }
}
