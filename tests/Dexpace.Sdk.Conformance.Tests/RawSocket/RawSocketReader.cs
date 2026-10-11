// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Conformance.Tests.RawSocket;

/// <summary>A buffered reader over the response stream: the head is read line by line, and the body reads what the head read left behind first.</summary>
internal sealed class RawSocketReader(Stream stream)
{
    private const int MaxLineBytes = 64 * 1024;
    private readonly byte[] _buffer = new byte[16 * 1024];
    private int _start;
    private int _end;

    /// <summary>Reads into <paramref name="destination"/>; zero means the peer closed.</summary>
    internal async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (_start < _end)
        {
            var take = Math.Min(destination.Length, _end - _start);
            _buffer.AsMemory(_start, take).CopyTo(destination);
            _start += take;
            return take;
        }

        return await stream.ReadAsync(destination, cancellationToken);
    }

    /// <summary>
    /// Reads one line without its CRLF (or bare LF), as Latin-1 so every byte stays visible. Returns <see langword="null"/>
    /// when the peer closed before any byte; a close inside a line is an <see cref="IOException"/>.
    /// </summary>
    internal async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        while (true)
        {
            if (_start == _end)
            {
                _start = 0;
                _end = await stream.ReadAsync(_buffer, cancellationToken);
                if (_end == 0)
                {
                    return line.Count == 0 ? null : throw new IOException("The server closed the connection inside a line.");
                }
            }

            var next = _buffer[_start++];
            if (next == (byte)'\n')
            {
                if (line.Count > 0 && line[^1] == (byte)'\r')
                {
                    line.RemoveAt(line.Count - 1);
                }

                return Encoding.Latin1.GetString([.. line]);
            }

            line.Add(next);
            if (line.Count > MaxLineBytes)
            {
                throw new IOException("A response line exceeds the client's limit.");
            }
        }
    }
}
