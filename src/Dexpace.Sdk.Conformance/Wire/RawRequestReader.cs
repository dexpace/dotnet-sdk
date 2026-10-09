// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;

namespace Dexpace.Sdk.Conformance.Wire;

/// <summary>
/// Frames HTTP/1.1 requests off one connection while keeping every byte: the head is read line by line up to the
/// empty line, then the body by <c>Content-Length</c> or by chunked framing (RFC 9112 §6.3, §7.1). Nothing is
/// validated beyond what framing needs; a request that cannot be framed throws <see cref="MalformedRequestException"/>
/// carrying every byte received for it.
/// </summary>
internal sealed class RawRequestReader(Stream stream)
{
    private const int MaxHeadBytes = 64 * 1024;
    private const int MaxBodyBytes = 16 * 1024 * 1024;
    private readonly byte[] _buffer = new byte[16 * 1024];
    private int _start;
    private int _end;

    /// <summary>Reads the next request, or returns <see langword="null"/> when the peer closed between requests.</summary>
    public async Task<RecordedRequest?> ReadAsync(int connection, CancellationToken cancellationToken)
    {
        var raw = new MemoryStream();
        var headLines = new List<string>();
        try
        {
            return await ReadAsync(connection, raw, headLines, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            var bytes = raw.ToArray();
            var partial = headLines.Count > 0 ? new RecordedRequest(connection, bytes, headLines, []) : null;
            throw new MalformedRequestException(ex.Message, bytes, partial);
        }
    }

    private async Task<RecordedRequest?> ReadAsync(
        int connection,
        MemoryStream raw,
        List<string> headLines,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await ReadLineAsync(raw, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                return raw.Length == 0
                    ? null
                    : throw new InvalidDataException("The peer closed the connection inside a request head.");
            }

            if (raw.Length > MaxHeadBytes)
            {
                throw new InvalidDataException($"The request head exceeds {MaxHeadBytes} bytes.");
            }

            var text = Encoding.Latin1.GetString(line).TrimEnd('\n').TrimEnd('\r');
            if (text.Length == 0)
            {
                break;
            }

            headLines.Add(text);
        }

        if (headLines.Count == 0)
        {
            throw new InvalidDataException("The request has no request line.");
        }

        var body = IsChunked(headLines)
            ? await ReadChunkedAsync(raw, cancellationToken).ConfigureAwait(false)
            : await ReadExactAsync(raw, ContentLength(headLines), cancellationToken).ConfigureAwait(false);
        return new RecordedRequest(connection, raw.ToArray(), headLines, body);
    }

    private static bool IsChunked(List<string> headLines) =>
        FieldValues(headLines, "Transfer-Encoding")
            .Any(value => value.Contains("chunked", StringComparison.OrdinalIgnoreCase));

    private static int ContentLength(List<string> headLines)
    {
        var values = FieldValues(headLines, "Content-Length").Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length == 0)
        {
            return 0;
        }

        if (values.Length > 1)
        {
            throw new InvalidDataException($"Conflicting Content-Length values: {string.Join(" | ", values)}.");
        }

        if (!int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            throw new InvalidDataException($"Unparseable or overflowing Content-Length: '{values[0]}'.");
        }

        return length <= MaxBodyBytes
            ? length
            : throw new InvalidDataException($"Content-Length {length} exceeds the fixture's {MaxBodyBytes}-byte cap.");
    }

    private static IEnumerable<string> FieldValues(List<string> headLines, string name) =>
        headLines.Skip(1)
            .Where(line => line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
            .Select(line => line[(name.Length + 1)..].Trim(' ', '\t'));

    private async Task<byte[]> ReadChunkedAsync(MemoryStream raw, CancellationToken cancellationToken)
    {
        var body = new MemoryStream();
        while (true)
        {
            var sizeLine = await ReadLineAsync(raw, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The peer closed the connection inside a chunked body.");
            var sizeText = Encoding.Latin1.GetString(sizeLine).Split(';')[0].Trim();
            if (!int.TryParse(sizeText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size)
                || size < 0
                || body.Length + size > MaxBodyBytes)
            {
                throw new InvalidDataException($"Unparseable, negative or oversized chunk size: '{sizeText}'.");
            }

            if (size == 0)
            {
                // Trailer section, up to and including the empty line.
                while (true)
                {
                    var trailer = await ReadLineAsync(raw, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidDataException("The peer closed the connection inside a chunked trailer.");
                    if (Encoding.Latin1.GetString(trailer).Trim().Length == 0)
                    {
                        return body.ToArray();
                    }
                }
            }

            body.Write(await ReadExactAsync(raw, size, cancellationToken).ConfigureAwait(false));
            _ = await ReadLineAsync(raw, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The peer closed the connection after a chunk.");
        }
    }

    private async Task<byte[]> ReadExactAsync(MemoryStream raw, int count, CancellationToken cancellationToken)
    {
        var bytes = new byte[count];
        var filled = 0;
        while (filled < count)
        {
            if (_start == _end && !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException($"The peer closed the connection {count - filled} byte(s) short of the body.");
            }

            var take = Math.Min(count - filled, _end - _start);
            Array.Copy(_buffer, _start, bytes, filled, take);
            raw.Write(_buffer, _start, take);
            _start += take;
            filled += take;
        }

        return bytes;
    }

    /// <summary>
    /// Reads through the next LF, returning the line's bytes with its terminator, or null at a clean EOF. Every byte
    /// consumed is also appended to <paramref name="raw"/> as it is read, so a failure keeps the partial line.
    /// </summary>
    private async Task<byte[]?> ReadLineAsync(MemoryStream raw, CancellationToken cancellationToken)
    {
        var line = new MemoryStream();
        while (true)
        {
            if (_start == _end && !await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                return line.Length == 0
                    ? null
                    : throw new InvalidDataException("The peer closed the connection inside a line.");
            }

            var newline = Array.IndexOf(_buffer, (byte)'\n', _start, _end - _start);
            var stop = newline < 0 ? _end : newline + 1;
            line.Write(_buffer, _start, stop - _start);
            raw.Write(_buffer, _start, stop - _start);
            _start = stop;
            if (newline >= 0)
            {
                return line.ToArray();
            }

            if (line.Length > MaxHeadBytes)
            {
                throw new InvalidDataException($"A line exceeds {MaxHeadBytes} bytes.");
            }
        }
    }

    private async Task<bool> FillAsync(CancellationToken cancellationToken)
    {
        _start = 0;
        _end = await stream.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
        return _end > 0;
    }
}
