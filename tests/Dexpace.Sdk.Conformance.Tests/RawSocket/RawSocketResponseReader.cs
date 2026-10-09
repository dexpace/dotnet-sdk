// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance.Tests.RawSocket;

/// <summary>
/// Reads a response head and frames its body, sharing no code with <c>HttpClient</c>. Lenient on the way in
/// (<c>TRANSPORT-14</c>, <c>TRANSPORT-27</c>): a header whose name is not a token or whose value carries a control byte is
/// dropped alone, obs-text is kept, a malformed <c>Content-Type</c> gives no media type and a non-numeric
/// <c>Content-Length</c> gives an unknown length read to the end of the connection.
/// </summary>
internal static class RawSocketResponseReader
{
    /// <summary>Reads the head from <paramref name="reader"/> and builds the response; the body owns <paramref name="exchange"/> unless there is none to read.</summary>
    internal static async Task<Response> ReadAsync(Request request, RawSocketReader reader, RawSocketExchange exchange, CancellationToken cancellationToken)
    {
        var statusLine = await reader.ReadLineAsync(cancellationToken) ?? throw new IOException("The server closed the connection before sending a status line.");
        var (protocol, code, reason) = ParseStatusLine(statusLine);
        var headers = new Headers.Builder();
        while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 } line)
        {
            Add(headers, line);
        }

        var built = headers.Build();
        var contentType = MediaType.TryParse(built.Get("Content-Type"), out var parsed) ? parsed : null;
        if (request.Method.Name == "HEAD" || code is >= 100 and < 200 or 204 or 304)
        {
            exchange.Dispose();
            return new Response(request, Status.FromCode(code), protocol, built, ResponseBody.FromStream(Stream.Null, contentType, 0), reason);
        }

        var (body, length) = Frame(built, reader, exchange);
        return new Response(request, Status.FromCode(code), protocol, built, ResponseBody.FromStream(body, contentType, length), reason);
    }

    private static (Stream Body, long Length) Frame(Headers headers, RawSocketReader reader, RawSocketExchange exchange)
    {
        if (headers.Get("Transfer-Encoding")?.Contains("chunked", StringComparison.OrdinalIgnoreCase) == true)
        {
            return (FramedBodyStream.Chunked(reader, exchange), -1);
        }

        return long.TryParse(headers.Get("Content-Length"), NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            ? (FramedBodyStream.Fixed(reader, exchange, length), length)
            : (FramedBodyStream.UntilClose(reader, exchange), -1);
    }

    private static (Protocol Protocol, int Code, string Reason) ParseStatusLine(string line)
    {
        var parts = line.Split(' ', 3);
        if (parts.Length < 2
            || !parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal)
            || parts[1].Length != 3
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var code))
        {
            throw new InvalidStatusLineException();
        }

        return (parts[0] == "HTTP/1.0" ? Protocol.Http10 : Protocol.Http11, code, parts.Length > 2 ? parts[2] : string.Empty);
    }

    private static void Add(Headers.Builder headers, string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            return;
        }

        try
        {
            headers.AddInbound(line[..colon], line[(colon + 1)..].Trim(' ', '\t'));
        }
        catch (ArgumentException)
        {
            // TRANSPORT-14: the one header goes, the response stays.
        }
    }

    /// <summary>The status line was not HTTP/1.x with a three-digit code.</summary>
    internal sealed class InvalidStatusLineException : IOException
    {
        internal InvalidStatusLineException()
            : base("The server sent an invalid status line.")
        {
        }
    }
}
