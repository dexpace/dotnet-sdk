// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dexpace.Sdk.Conformance.Wire;

/// <summary>
/// One scripted reply of the <see cref="LoopbackServer"/>: the exact bytes it writes to the socket, and whether it
/// then closes the connection. Nothing is validated or normalised, so a script can put anything on the wire — a
/// redirect, a header with a control byte or a malformed <c>Content-Type</c>, a truncated body, a bare-LF line.
/// </summary>
public sealed class LoopbackResponse
{
    private LoopbackResponse(
        byte[] bytes,
        bool closeConnection,
        IAsyncEnumerable<byte[]>? chunks = null,
        Task? gate = null,
        ReplyMode mode = ReplyMode.Normal,
        LoopbackResponse? then = null)
    {
        Bytes = bytes;
        CloseConnection = closeConnection;
        Chunks = chunks;
        Gate = gate;
        Mode = mode;
        Then = then;
    }

    /// <summary>For <see cref="Gated"/>: the task the server waits on before it does anything else; <see langword="null"/> otherwise.</summary>
    internal Task? Gate { get; }

    /// <summary>For <see cref="Gated"/>: the reply the server sends once <see cref="Gate"/> completes.</summary>
    internal LoopbackResponse? Then { get; }

    /// <summary>What the server does at this reply: write it, abort the connection, or hang.</summary>
    internal ReplyMode Mode { get; }

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
    /// <c>Content-Length</c> and, unless <paramref name="keepAlive"/>, <c>Connection: close</c>, then the body.
    /// </summary>
    /// <param name="statusCode">The status code; any integer, so vendor and out-of-range codes can be scripted.</param>
    /// <param name="reasonPhrase">The reason phrase.</param>
    /// <param name="headers">Header lines as name and value, written as <c>name: value</c> with no validation.</param>
    /// <param name="body">The body, encoded as UTF-8.</param>
    /// <param name="keepAlive">
    /// Leave the connection open after the reply (no <c>Connection: close</c> header, no server-side close), so a test can
    /// tell a connection the <em>client</em> released from one the server closed (plan reading R2, R13).
    /// </param>
    public static LoopbackResponse Status(
        int statusCode,
        string reasonPhrase,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        string body = "",
        bool keepAlive = false)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var head = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n");
        foreach (var (name, value) in headers ?? [])
        {
            head.Append(CultureInfo.InvariantCulture, $"{name}: {value}\r\n");
        }

        head.Append(CultureInfo.InvariantCulture, $"Content-Length: {payload.Length}\r\n");
        head.Append(keepAlive ? "\r\n" : "Connection: close\r\n\r\n");
        return new([.. Encoding.Latin1.GetBytes(head.ToString()), .. payload], closeConnection: !keepAlive);
    }

    /// <summary>
    /// A <c>200 OK</c> whose body is <c>Transfer-Encoding: chunked</c> and is written as <paramref name="chunks"/> yields it:
    /// each chunk is framed, written and flushed before the next is requested, and the terminating chunk follows the last.
    /// A test that gates its enumerable gates the wire, so a server-sent-events consumer can be shown to receive an event
    /// before the server has written the next byte (phase 7b, P7b-23). The connection is closed afterwards unless
    /// <paramref name="keepAlive"/>. The server does not watch for the client leaving while it waits on the enumerable; it
    /// notices on its next write.
    /// </summary>
    /// <param name="headers">Header lines as name and value, written as <c>name: value</c> with no validation.</param>
    /// <param name="chunks">The body chunks, in order; each is non-empty.</param>
    /// <param name="keepAlive">Leave the connection open after the terminating chunk (plan reading R13).</param>
    public static LoopbackResponse Streamed(
        IEnumerable<KeyValuePair<string, string>> headers,
        IAsyncEnumerable<byte[]> chunks,
        bool keepAlive = false)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(chunks);
        var head = new StringBuilder("HTTP/1.1 200 OK\r\n");
        foreach (var (name, value) in headers)
        {
            head.Append(CultureInfo.InvariantCulture, $"{name}: {value}\r\n");
        }

        head.Append("Transfer-Encoding: chunked\r\n\r\n");
        return new(Encoding.Latin1.GetBytes(head.ToString()), closeConnection: !keepAlive, chunks);
    }

    /// <summary>A <c>200 OK</c> with the given body and <c>Content-Type</c>.</summary>
    /// <param name="body">The body, encoded as UTF-8.</param>
    /// <param name="contentType">The <c>Content-Type</c> value, written verbatim.</param>
    /// <param name="keepAlive">Leave the connection open after the reply (see <see cref="Status"/>).</param>
    public static LoopbackResponse Ok(string body = "", string contentType = "text/plain; charset=utf-8", bool keepAlive = false) =>
        Status(200, "OK", [new("Content-Type", contentType)], body, keepAlive);

    /// <summary>A body-less redirect with the given status code and <c>Location</c> value.</summary>
    /// <param name="statusCode">The 3xx status code.</param>
    /// <param name="location">The <c>Location</c> value, written verbatim (absolute or relative).</param>
    public static LoopbackResponse Redirect(int statusCode, string location) =>
        Status(statusCode, "Redirect", [new("Location", location)]);

    /// <summary>
    /// Writes nothing until <paramref name="gate"/> completes, then behaves as <paramref name="then"/>: a server that hangs
    /// before the status line. While it waits the server watches the connection, so a client that gives up releases it
    /// (<see cref="LoopbackServer.WaitForConnectionReleasedAsync"/>). A faulted gate ends the connection with no reply and is
    /// recorded in <see cref="LoopbackServer.Faults"/>; a cancelled one ends it quietly.
    /// </summary>
    /// <param name="gate">The condition the test completes to let the reply go.</param>
    /// <param name="then">The reply sent once the gate opens.</param>
    public static LoopbackResponse Gated(Task gate, LoopbackResponse then)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(then);
        return new([], closeConnection: false, gate: gate, then: then);
    }

    /// <summary>
    /// A reply whose status line and headers arrive at once and whose body does not: <paramref name="rest"/> is written, as
    /// the one chunk of a <c>Transfer-Encoding: chunked</c> body, only after <paramref name="gate"/> completes (hang after
    /// headers, a dribbled body). The connection is closed afterwards.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="headers">Header lines as name and value, written as <c>name: value</c> with no validation.</param>
    /// <param name="gate">The condition the test completes to release the body.</param>
    /// <param name="rest">The body, written as one chunk; not empty.</param>
    public static LoopbackResponse HeadersThenGate(
        int statusCode,
        IEnumerable<KeyValuePair<string, string>> headers,
        Task gate,
        ReadOnlyMemory<byte> rest)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentOutOfRangeException.ThrowIfZero(rest.Length);
        var head = new StringBuilder().Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {statusCode} {ReasonFor(statusCode)}\r\n");
        foreach (var (name, value) in headers)
        {
            head.Append(CultureInfo.InvariantCulture, $"{name}: {value}\r\n");
        }

        head.Append("Transfer-Encoding: chunked\r\n\r\n");
        return new(Encoding.Latin1.GetBytes(head.ToString()), closeConnection: true, AfterGateAsync(gate, rest.ToArray()));
    }

    /// <summary>
    /// Reads the request, records it, then resets the connection (a TCP reset) without writing a byte: the first-connection
    /// failure and the peer reset of a transport that sent a request and got nothing back. It does not change
    /// <see cref="LoopbackServer.ConnectionCount"/>.
    /// </summary>
    public static LoopbackResponse Abort() => new([], closeConnection: true, mode: ReplyMode.Abort);

    /// <summary>
    /// Reads the request, records it, and never answers: the connection stays open until the client leaves it (which
    /// releases it) or the server is disposed.
    /// </summary>
    public static LoopbackResponse Hang() => new([], closeConnection: false, mode: ReplyMode.Hang);

    /// <summary>
    /// A <c>200 OK</c> with <c>Content-Length: <paramref name="bytes"/></c> and a deterministic body of that many
    /// pseudo-random octets (<c>new Random(seed)</c>), written in 64 KiB slices so a client that disposes mid-body makes the
    /// server's write fail, which it records as a release and not as a fault. Two calls with equal arguments are
    /// byte-identical.
    /// </summary>
    /// <param name="bytes">The body length; positive.</param>
    /// <param name="seed">The generator seed.</param>
    /// <param name="keepAlive">Leave the connection open after the reply (see <see cref="Status"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is not positive.</exception>
    public static LoopbackResponse Large(int bytes, int seed, bool keepAlive = false)
    {
        var payload = LargeBody.Generate(bytes, seed);
        var head = Encoding.Latin1.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: {bytes}\r\n{(keepAlive ? string.Empty : "Connection: close\r\n")}\r\n"));
        return new([.. head, .. payload], closeConnection: !keepAlive);
    }

    /// <summary>
    /// A <c>200 OK</c> whose body is the value of <paramref name="request"/>'s <c>X-Conformance-Id</c> header (empty when
    /// absent): each of many concurrent calls can check that it got its own reply. Use it as
    /// <c>LoopbackServer.Start(LoopbackResponse.EchoId)</c>.
    /// </summary>
    /// <param name="request">The request being answered.</param>
    public static LoopbackResponse EchoId(RecordedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Ok(request.HeaderValues("X-Conformance-Id") is [var id, ..] ? id : string.Empty);
    }

    private static string ReasonFor(int statusCode) => statusCode == 200 ? "OK" : "Loopback";

    private static async IAsyncEnumerable<byte[]> AfterGateAsync(Task gate, byte[] rest, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        yield return rest;
    }
}
