// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text;

namespace Dexpace.Sdk.Core.IO;

/// <summary>
/// Reads UTF-8 lines from a stream (IO-14), replacing <c>StreamReader.ReadLine</c>, which splits on a lone CR that
/// IO-14 keeps as content (design §3.1, position G; the ban is in <c>BannedSymbols.txt</c>).
/// </summary>
/// <remarks>
/// <para>
/// In the default mode a lone CR is content, decided by one byte of lookahead even across a refill; a final unterminated
/// line is returned as is; an empty line is <c>""</c>; <see langword="null"/> means the stream was exhausted before any
/// further byte. A byte-order mark is ordinary content in both modes, and malformed UTF-8 decodes to U+FFFD (P3a-11).
/// The decode runs over the whole accumulated line, so a character split across reads decodes once (IO-13).
/// </para>
/// <para>
/// The line cap is mandatory: a hostile stream that never sends a newline cannot grow the reader without limit. A line
/// whose content (terminator excluded) exceeds <c>maxLineBytes</c> throws <see cref="InvalidDataException"/> naming the
/// cap, never the content, and the reader stays failed: every later read throws, because the stream is now mid-line. The
/// scan resumes where it stopped after a refill, so each byte is examined once.
/// </para>
/// <para>
/// Ownership (IO-6): the reader disposes its stream unless <c>leaveOpen</c> is set. Disposal is latched with
/// <see cref="Interlocked"/> so the stream is disposed at most once across any mix of <see cref="Dispose"/> and
/// <see cref="DisposeAsync"/> (IO-41), and reading after disposal throws <see cref="ObjectDisposedException"/> (IO-42).
/// Single-threaded contract (IO-37): one reader, one consumer at a time; reads and disposal are not safe to race.
/// The synchronous form takes no token because <see cref="Stream.Read(byte[], int, int)"/> has none; the asynchronous
/// form checks its token through the underlying read (R6). The reader never issues a zero-count read (IO-2).
/// </para>
/// </remarks>
internal sealed class Utf8LineReader : IDisposable, IAsyncDisposable
{
    /// <summary>The default line cap, design §10 entry 20's figure.</summary>
    internal const int DefaultMaxLineBytes = 1024 * 1024;

    private const int BufferSize = 4096;

    private readonly Stream _source;
    private readonly LineTerminators _terminators;
    private readonly int _maxLineBytes;
    private readonly bool _leaveOpen;
    private readonly byte[] _buffer = new byte[BufferSize];
    private readonly ArrayBufferWriter<byte> _line = new();
    private int _start;
    private int _end;
    private int _scanFrom;
    private bool _skipLeadingLf;
    private bool _eof;
    private bool _failed;
    private int _disposed;

    /// <summary>Initializes a new reader.</summary>
    /// <param name="source">The stream to read; it must be readable.</param>
    /// <param name="terminators">Which sequences end a line.</param>
    /// <param name="maxLineBytes">The most content bytes a line may hold, terminator excluded; positive.</param>
    /// <param name="leaveOpen">When <see langword="true"/>, disposing the reader leaves <paramref name="source"/> open.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> is not positive (IO-3).</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> is not readable.</exception>
    internal Utf8LineReader(
        Stream source,
        LineTerminators terminators = LineTerminators.LineFeedOrCrLf,
        int maxLineBytes = DefaultMaxLineBytes,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLineBytes);
        if (!source.CanRead)
        {
            throw new ArgumentException("The source stream must be readable.", nameof(source));
        }

        _source = source;
        _terminators = terminators;
        _maxLineBytes = maxLineBytes;
        _leaveOpen = leaveOpen;
    }

    /// <summary>The number of bytes the scan has examined; each byte of a line is examined once (test hook).</summary>
    internal long ScannedBytes { get; private set; }

    /// <summary>Reads the next line, or <see langword="null"/> when the stream is exhausted.</summary>
    /// <returns>The line without its terminator, or <see langword="null"/>.</returns>
    /// <exception cref="InvalidDataException">A line exceeded the cap; the reader stays failed.</exception>
    /// <exception cref="ObjectDisposedException">The reader was disposed (IO-42).</exception>
    internal string? ReadLine()
    {
        BeginRead();
        while (true)
        {
            if (TryAdvance(out var line))
            {
                return line;
            }

            PrepareForRead();
            var read = _source.Read(_buffer, _end, _buffer.Length - _end);
            AcceptRead(read);
        }
    }

    /// <summary>Reads the next line asynchronously, or <see langword="null"/> when the stream is exhausted.</summary>
    /// <param name="cancellationToken">A token to cancel the underlying read.</param>
    /// <returns>The line without its terminator, or <see langword="null"/>.</returns>
    internal async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        BeginRead();
        while (true)
        {
            if (TryAdvance(out var line))
            {
                return line;
            }

            PrepareForRead();
            var read = await _source.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);
            AcceptRead(read);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && !_leaveOpen)
        {
            _source.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && !_leaveOpen)
        {
            await _source.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void BeginRead()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_failed)
        {
            throw LineTooLong();
        }
    }

    private InvalidDataException LineTooLong() =>
        new($"A line exceeded the cap of {_maxLineBytes} bytes; the stream is now mid-line and the reader has failed.");

    /// <summary>Tries to produce a line from buffered bytes; returns <see langword="false"/> when more data is needed.</summary>
    private bool TryAdvance(out string? line)
    {
        line = null;
        if (_skipLeadingLf)
        {
            if (_start < _end)
            {
                if (_buffer[_start] == (byte)'\n')
                {
                    _start++;
                    _scanFrom = Math.Max(_scanFrom, _start);
                }

                _skipLeadingLf = false;
            }
            else if (_eof)
            {
                _skipLeadingLf = false;
            }
            else
            {
                return false;
            }
        }

        if (ScanForTerminator(out var contentEnd, out var next))
        {
            line = TakeLine(contentEnd);
            _start = next;
            _scanFrom = Math.Max(_scanFrom, _start);
            return true;
        }

        var pending = _line.WrittenCount + (_end - _start);
        if (_eof)
        {
            line = pending == 0 ? null : TakeLine(_end);
            _start = _end;
            return true;
        }

        // A trailing CR in the default mode is held back, undecided; it does not count against the cap yet.
        var counted = pending - (HoldsBackTrailingCr() ? 1 : 0);
        if (counted > _maxLineBytes)
        {
            throw Fail();
        }

        return false;
    }

    private bool ScanForTerminator(out int contentEnd, out int next)
    {
        contentEnd = 0;
        next = 0;
        for (var i = _scanFrom; i < _end; i++)
        {
            ScannedBytes++;
            var b = _buffer[i];
            if (_terminators == LineTerminators.Whatwg)
            {
                if (b != (byte)'\r' && b != (byte)'\n')
                {
                    continue;
                }

                _skipLeadingLf = b == (byte)'\r';
                contentEnd = i;
                next = i + 1;
                return true;
            }

            if (b == (byte)'\n')
            {
                contentEnd = i > _start && _buffer[i - 1] == (byte)'\r' ? i - 1 : i;
                next = i + 1;
                return true;
            }
        }

        _scanFrom = _end;
        return false;
    }

    private string TakeLine(int contentEnd)
    {
        var windowBytes = contentEnd - _start;
        if (_line.WrittenCount + (long)windowBytes > _maxLineBytes)
        {
            throw Fail();
        }

        string text;
        if (_line.WrittenCount == 0)
        {
            text = Encoding.UTF8.GetString(_buffer, _start, windowBytes);
        }
        else
        {
            _line.Write(_buffer.AsSpan(_start, windowBytes));
            text = Encoding.UTF8.GetString(_line.WrittenSpan);
            _line.ResetWrittenCount();
        }

        return text;
    }

    private InvalidDataException Fail()
    {
        _failed = true;
        _line.ResetWrittenCount();
        return LineTooLong();
    }

    private bool HoldsBackTrailingCr() =>
        _terminators == LineTerminators.LineFeedOrCrLf && _end > _start && _buffer[_end - 1] == (byte)'\r';

    /// <summary>Moves the scanned window into the line accumulator (holding back an undecided CR) so a read has room.</summary>
    private void PrepareForRead()
    {
        var hold = HoldsBackTrailingCr();
        var flushEnd = hold ? _end - 1 : _end;
        if (flushEnd > _start)
        {
            _line.Write(_buffer.AsSpan(_start, flushEnd - _start));
        }

        if (hold)
        {
            _buffer[0] = (byte)'\r';
            _start = 0;
            _end = 1;
        }
        else
        {
            _start = 0;
            _end = 0;
        }

        _scanFrom = _end;
    }

    private void AcceptRead(int read)
    {
        if (read == 0)
        {
            _eof = true;
            return;
        }

        _end += read;
    }
}
