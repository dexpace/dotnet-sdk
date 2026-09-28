// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Loopback;

/// <summary>
/// One request exactly as it arrived on the <see cref="LoopbackServer"/>'s socket. <see cref="Raw"/> is the
/// byte-for-byte record (request line, header block, and body with its chunk framing, if any); the other members
/// are parsed views of it for convenience. Header text is decoded as Latin-1, so every byte maps to one char and
/// a control byte or an obs-text octet stays visible.
/// </summary>
public sealed class RecordedRequest
{
    internal RecordedRequest(int connection, byte[] raw, IReadOnlyList<string> headLines, byte[] body)
    {
        Connection = connection;
        Raw = raw;
        RequestLine = headLines[0];
        HeaderLines = headLines.Skip(1).ToArray();
        Body = body;

        var parts = RequestLine.Split(' ', 3);
        Method = parts[0];
        Target = parts.Length > 1 ? parts[1] : string.Empty;
        Version = parts.Length > 2 ? parts[2] : string.Empty;
    }

    /// <summary>The zero-based index of the connection the request arrived on, in accept order.</summary>
    public int Connection { get; }

    /// <summary>Every byte of the request, as received.</summary>
    public ReadOnlyMemory<byte> Raw { get; }

    /// <summary><see cref="Raw"/> decoded as Latin-1: one char per byte, CRLFs included.</summary>
    public string RawText => Encoding.Latin1.GetString(Raw.Span);

    /// <summary>The request line without its line terminator, e.g. <c>GET /path?q=1 HTTP/1.1</c>.</summary>
    public string RequestLine { get; }

    /// <summary>The method token of the request line.</summary>
    public string Method { get; }

    /// <summary>The request target of the request line, as sent.</summary>
    public string Target { get; }

    /// <summary>The HTTP version of the request line, e.g. <c>HTTP/1.1</c>.</summary>
    public string Version { get; }

    /// <summary>The header lines in arrival order, each without its line terminator and otherwise untouched.</summary>
    public IReadOnlyList<string> HeaderLines { get; }

    /// <summary>The body, with any chunked framing removed; empty when the request had none.</summary>
    public ReadOnlyMemory<byte> Body { get; }

    /// <summary><see cref="Body"/> decoded as UTF-8.</summary>
    public string BodyText => Encoding.UTF8.GetString(Body.Span);

    /// <summary>
    /// The values of every header line whose name matches <paramref name="name"/> case-insensitively, each with the
    /// optional whitespace around the field value removed (RFC 9110 §5.5) and nothing else changed. Read
    /// <see cref="HeaderLines"/> to assert on the exact bytes.
    /// </summary>
    /// <param name="name">The field name.</param>
    public IReadOnlyList<string> HeaderValues(string name) =>
        HeaderLines
            .Select(line => (Line: line, Colon: line.IndexOf(':', StringComparison.Ordinal)))
            .Where(entry => entry.Colon > 0
                && string.Equals(entry.Line[..entry.Colon], name, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Line[(entry.Colon + 1)..].Trim(' ', '\t'))
            .ToArray();

    /// <summary>The single value of the header <paramref name="name"/>, or <see langword="null"/> when absent.</summary>
    /// <param name="name">The field name.</param>
    /// <exception cref="InvalidOperationException">The header appeared on more than one line.</exception>
    public string? Header(string name)
    {
        var values = HeaderValues(name);
        return values.Count switch
        {
            0 => null,
            1 => values[0],
            _ => throw new InvalidOperationException($"Header '{name}' appeared on {values.Count} lines."),
        };
    }
}
