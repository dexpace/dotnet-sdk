// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Conformance.Tests.RawSocket;

/// <summary>
/// Writes a request as HTTP/1.1 text, sharing no code with <c>HttpClient</c> (P8a-2). The framing the transport owns (host,
/// length, transfer coding, connection) is computed here and the caller's own copies are dropped (<c>TRANSPORT-11</c>); the
/// caller's <c>Content-Type</c> wins over the body's, which is used only when the caller set none (<c>TRANSPORT-10</c>); a
/// body-less <c>POST</c>, <c>PUT</c> or <c>PATCH</c> carries <c>Content-Length: 0</c> (<c>TRANSPORT-26</c>).
/// </summary>
internal static class RawSocketRequestWriter
{
    private static readonly HashSet<string> s_framing = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Content-Length", "Transfer-Encoding", "Connection", "Keep-Alive", "Upgrade", "TE", "Expect",
    };

    /// <summary>Writes the head, then the body.</summary>
    internal static async Task WriteAsync(Request request, Stream stream, CancellationToken cancellationToken)
    {
        await stream.WriteAsync(Head(request), cancellationToken);
        if (request.Body is { } body)
        {
            if (body.ContentLength >= 0)
            {
                await body.WriteToAsync(stream, cancellationToken);
            }
            else
            {
                var chunked = new ChunkedWriteStream(stream);
                await body.WriteToAsync(chunked, cancellationToken);
                await chunked.FinishAsync(cancellationToken);
            }
        }

        await stream.FlushAsync(cancellationToken);
    }

    private static byte[] Head(Request request)
    {
        var url = request.Url;
        var host = url.HostNameType == UriHostNameType.IPv6 ? $"[{url.Host.Trim('[', ']')}]" : url.Host;
        var head = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{request.Method} {(string.IsNullOrEmpty(url.PathAndQuery) ? "/" : url.PathAndQuery)} HTTP/1.1\r\n")
            .Append(CultureInfo.InvariantCulture, $"Host: {host}{(url.IsDefaultPort ? string.Empty : ":" + url.Port.ToString(CultureInfo.InvariantCulture))}\r\n");
        foreach (var (name, values) in request.Headers.Where(header => !s_framing.Contains(header.Key)))
        {
            foreach (var value in values)
            {
                head.Append(name).Append(": ").Append(value).Append("\r\n");
            }
        }

        if (!request.Headers.Contains("Content-Type") && request.Body?.ContentType is { } bodyType)
        {
            head.Append("Content-Type: ").Append(bodyType).Append("\r\n");
        }

        head.Append(Framing(request));
        head.Append("Connection: close\r\n\r\n");
        return Encoding.Latin1.GetBytes(head.ToString());
    }

    private static string Framing(Request request) => request.Body switch
    {
        null when request.Method.Name is "POST" or "PUT" or "PATCH" => "Content-Length: 0\r\n",
        null => string.Empty,
        { ContentLength: >= 0 } body => string.Create(CultureInfo.InvariantCulture, $"Content-Length: {body.ContentLength}\r\n"),
        _ => "Transfer-Encoding: chunked\r\n",
    };
}
