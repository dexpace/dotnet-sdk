// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;

namespace Dexpace.Sdk.Conformance.Wire;

/// <summary>
/// One scripted reply of the <see cref="LoopbackServer"/>: the exact bytes it writes to the socket, and whether it
/// then closes the connection. Nothing is validated or normalised, so a script can put anything on the wire — a
/// redirect, a header with a control byte or a malformed <c>Content-Type</c>, a truncated body, a bare-LF line.
/// </summary>
public sealed class LoopbackResponse
{
    private LoopbackResponse(byte[] bytes, bool closeConnection, IAsyncEnumerable<byte[]>? chunks = null)
    {
        Bytes = bytes;
        CloseConnection = closeConnection;
        Chunks = chunks;
    }

    /// <summary>The bytes written to the socket, verbatim.</summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>
    /// For a <see cref="Streamed"/> reply, the body chunks the server writes after <see cref="Bytes"/>, one chunked
    /// transfer-encoding chunk each, flushing after every one; <see langword="null"/> for every other reply.
    /// </summary>
    public IAsyncEnumerable<byte[]>? Chunks { get; }

    /// <summary>Whether the server half-closes and then closes the connection after writing <see cref="Bytes"/>.</summary>
    public bool CloseConnection { get; }

    /// <summary>A reply of exactly <paramref name="bytes"/>, then the connection closes.</summary>
    /// <param name="bytes">The bytes to write.</param>
    public static LoopbackResponse Raw(ReadOnlySpan<byte> bytes) => Raw(bytes, closeConnection: true);

    /// <summary>A reply of exactly <paramref name="bytes"/>.</summary>
    /// <param name="bytes">The bytes to write.</param>
    /// <param name="closeConnection">Whether to close the connection afterwards (a close-delimited body needs it).</param>
    public static LoopbackResponse Raw(ReadOnlySpan<byte> bytes, bool closeConnection) =>
        new(bytes.ToArray(), closeConnection);

    /// <summary>A reply of exactly <paramref name="text"/>, encoded as Latin-1 so every char is one byte, then the connection closes.</summary>
    /// <param name="text">The response text, including its own CRLFs.</param>
    public static LoopbackResponse Raw(string text) => Raw(text, closeConnection: true);

    /// <summary>A reply of exactly <paramref name="text"/>, encoded as Latin-1 so every char is one byte.</summary>
    /// <param name="text">The response text, including its own CRLFs.</param>
    /// <param name="closeConnection">Whether to close the connection afterwards (a close-delimited body needs it).</param>
    public static LoopbackResponse Raw(string text, bool closeConnection)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(Encoding.Latin1.GetBytes(text), closeConnection);
    }

    /// <summary>
    /// An HTTP/1.1 reply with the given status line, header lines written verbatim in order, then
    /// <c>Content-Length</c> and <c>Connection: close</c>, then the body.
    /// </summary>
    /// <param name="statusCode">The status code; any integer, so vendor and out-of-range codes can be scripted.</param>
    /// <param name="reasonPhrase">The reason phrase.</param>
    /// <param name="headers">Header lines as name and value, written as <c>name: value</c> with no validation.</param>
    /// <param name="body">The body, encoded as UTF-8.</param>
    public static LoopbackResponse Status(
        int statusCode,
        string reasonPhrase,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        string body = "")
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var head = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n");
        foreach (var (name, value) in headers ?? [])
        {
            head.Append(CultureInfo.InvariantCulture, $"{name}: {value}\r\n");
        }

        head.Append(CultureInfo.InvariantCulture, $"Content-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        return new([.. Encoding.Latin1.GetBytes(head.ToString()), .. payload], closeConnection: true);
    }

    /// <summary>
    /// A <c>200 OK</c> whose body is <c>Transfer-Encoding: chunked</c> and is written as <paramref name="chunks"/> yields it:
    /// each chunk is framed, written and flushed before the next is requested, and the terminating chunk follows the last.
    /// A test that gates its enumerable gates the wire, so a server-sent-events consumer can be shown to receive an event
    /// before the server has written the next byte (phase 7b, P7b-23). The connection is closed afterwards.
    /// </summary>
    /// <param name="headers">Header lines as name and value, written as <c>name: value</c> with no validation.</param>
    /// <param name="chunks">The body chunks, in order; each is non-empty.</param>
    public static LoopbackResponse Streamed(IEnumerable<KeyValuePair<string, string>> headers, IAsyncEnumerable<byte[]> chunks)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(chunks);
        var head = new StringBuilder("HTTP/1.1 200 OK\r\n");
        foreach (var (name, value) in headers)
        {
            head.Append(CultureInfo.InvariantCulture, $"{name}: {value}\r\n");
        }

        head.Append("Transfer-Encoding: chunked\r\n\r\n");
        return new(Encoding.Latin1.GetBytes(head.ToString()), closeConnection: true, chunks);
    }

    /// <summary>A <c>200 OK</c> with the given body and <c>Content-Type</c>.</summary>
    /// <param name="body">The body, encoded as UTF-8.</param>
    /// <param name="contentType">The <c>Content-Type</c> value, written verbatim.</param>
    public static LoopbackResponse Ok(string body = "", string contentType = "text/plain; charset=utf-8") =>
        Status(200, "OK", [new("Content-Type", contentType)], body);

    /// <summary>A body-less redirect with the given status code and <c>Location</c> value.</summary>
    /// <param name="statusCode">The 3xx status code.</param>
    /// <param name="location">The <c>Location</c> value, written verbatim (absolute or relative).</param>
    public static LoopbackResponse Redirect(int statusCode, string location) =>
        Status(statusCode, "Redirect", [new("Location", location)]);
}
