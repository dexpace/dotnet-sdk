// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// Builds the native <see cref="HttpRequestMessage"/> for a request: the content, then every header in its place
/// (<c>TRANSPORT-2</c>, <c>-10</c>, <c>-11</c>, <c>-12</c>, <c>-13</c>, <c>-26</c>, <c>HTTP-21</c>; design §E, phase 8b P8b-11). It is the
/// one builder both faces use, so the asynchronous and the blocking send put identical bytes on the wire.
/// </summary>
/// <remarks>
/// <para>
/// <b>The partition.</b> <c>System.Net.Http</c> keeps request headers and content headers in two collections and refuses a header
/// in the wrong one. Per header, after the trace-context strip: a framing header the transport computes (<c>Host</c>,
/// <c>Content-Length</c>, <c>Transfer-Encoding</c>, <c>Connection</c>, <c>Keep-Alive</c>, <c>Upgrade</c>, <c>TE</c>, <c>Expect</c>) is dropped
/// and logged at <see cref="Microsoft.Extensions.Logging.LogLevel.Debug"/> (event 111); a header that fails the wire re-check is
/// dropped under the policy (event 110); a content header (<c>Allow</c>, <c>Content-*</c>, <c>Expires</c>, <c>Last-Modified</c>) goes to the
/// content, which is where the caller's <c>Content-Type</c> overrides the body's; any other header goes to the request. A header
/// neither collection accepts is a drop under the policy, never a silent loss.
/// </para>
/// <para>
/// <b>The content.</b> A request with a body carries it. A body-less <c>POST</c>, <c>PUT</c>, <c>PATCH</c> or extension method carries a
/// zero-length content: the native client writes <c>Content-Length: 0</c> for these anyway, byte for byte, and with content present
/// its connection-failure retry cannot fire (<c>TRANSPORT-2</c>, design §10 entry 33). A body-less <c>DELETE</c> or <c>OPTIONS</c> carries
/// one only when the caller set a content header; a body-less <c>GET</c>, <c>HEAD</c>, <c>TRACE</c> or <c>CONNECT</c> carries none
/// (<c>TRANSPORT-26</c>), so a content header on it is dropped and logged.
/// </para>
/// <para>
/// <b>Content-Type is the caller's.</b> The body's media type is stamped only when the caller set no <c>Content-Type</c> of their
/// own (the name matches ignoring case); a multipart body's own boundary is part of its type, so a caller type that lacks it wins
/// and the caller owns the consequence.
/// </para>
/// </remarks>
internal sealed class OutboundHeaderMapper(HeaderDropLog drops, Func<RequestBody, ResendCapture?> captureFor)
{
    // TRANSPORT-11: the framing set the native client computes from the body and the connection (design §3.2).
    private static readonly FrozenSet<string> s_framingHeaders = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "host",
        "content-length",
        "transfer-encoding",
        "connection",
        "keep-alive",
        "upgrade",
        "te",
        "expect");

    // The headers HttpContentHeaders owns in System.Net.Http (Content-Length is a framing header and never gets here).
    private static readonly FrozenSet<string> s_contentHeaders = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        "allow",
        "content-disposition",
        "content-encoding",
        "content-language",
        "content-location",
        "content-md5",
        "content-range",
        "content-type",
        "expires",
        "last-modified");

    /// <summary>Builds the message for <paramref name="request"/>; the caller disposes it.</summary>
    /// <param name="request">The request to send.</param>
    internal HttpRequestMessage Map(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var message = new HttpRequestMessage(new HttpMethod(request.Method.Name), request.Url);
        try
        {
            message.Content = BuildContent(request);
            StampBodyContentType(request, message.Content);
            var (stripTraceparent, stripTracestate) = StripTraceContext(request);
            foreach (var (name, values) in request.Headers)
            {
                // Design §8.1, P5c-11: the SDK's own stamp for the current attempt is dropped so the runtime writes its child
                // span's id. Nothing the caller intended is lost, so the drop is silent.
                if ((stripTraceparent && TraceContextStripping.IsTraceparent(name))
                    || (stripTracestate && TraceContextStripping.IsTracestate(name)))
                {
                    continue;
                }

                if (s_framingHeaders.Contains(name))
                {
                    drops.FramingDropped(name);
                }
                else if (!IsWireSafe(name, values))
                {
                    // XCUT-18 defence in depth: the model validated this header, but a value accepted on the lenient inbound
                    // path can be re-used on a request. Drop it rather than let HttpClient write it or fail the whole send.
                    drops.Dropped(name);
                }
                else
                {
                    Place(message, name, values);
                }
            }

            return message;
        }
        catch
        {
            message.Dispose();
            throw;
        }
    }

    // GET, HEAD, TRACE and CONNECT carry no content (TRANSPORT-26, RFC 9110 section 9.3).
    private static bool IsBodyForbidden(Method method) =>
        method.Name.Equals("GET", StringComparison.OrdinalIgnoreCase)
        || method.Name.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
        || method.Name.Equals("TRACE", StringComparison.OrdinalIgnoreCase)
        || method.Name.Equals("CONNECT", StringComparison.OrdinalIgnoreCase);

    // The methods the native client frames with Content-Length: 0 when they carry no content. GET, HEAD, OPTIONS and DELETE get no
    // framing header, so a substitute would change the wire; TRACE gets one but stays without content (TRANSPORT-26); CONNECT is
    // never sent by the SDK.
    private static bool CarriesZeroLengthContent(Method method) =>
        !(IsBodyForbidden(method)
            || method.Name.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase)
            || method.Name.Equals("DELETE", StringComparison.OrdinalIgnoreCase));

    private HttpContent? BuildContent(Request request)
    {
        if (request.Body is { } body)
        {
            return new RequestBodyContent(body, captureFor(body));
        }

        // TRANSPORT-2 (P8b-T2, design section 10 entry 33): a zero-length content is what SocketsHttpHandler writes for these methods
        // anyway (Content-Length: 0, byte for byte), and with content present its connection-failure retry cannot fire.
        if (CarriesZeroLengthContent(request.Method))
        {
            return new ByteArrayContent([]);
        }

        // DELETE and OPTIONS: an empty content only to carry a content header the caller set (it adds Content-Length: 0).
        return !IsBodyForbidden(request.Method) && HasCarriableContentHeader(request) ? new ByteArrayContent([]) : null;
    }

    private static bool HasCarriableContentHeader(Request request)
    {
        foreach (var (name, values) in request.Headers)
        {
            if (s_contentHeaders.Contains(name) && IsWireSafe(name, values))
            {
                return true;
            }
        }

        return false;
    }

    // TRANSPORT-10: decided once, before the header loop (whose order is not guaranteed): the body's media type is used only when
    // the caller set no Content-Type of their own, matched ignoring case.
    private static void StampBodyContentType(Request request, HttpContent? content)
    {
        if (content is RequestBodyContent && request.Body?.ContentType is { } contentType && !request.Headers.Contains("Content-Type"))
        {
            content.Headers.TryAddWithoutValidation("Content-Type", contentType.ToString());
        }
    }

    // A header neither collection accepts is a logged drop, never a silent loss (TRANSPORT-12). A content header goes to the content
    // only; any other goes to the request, and to the content if the request refuses it (a header kind this table does not know).
    private void Place(HttpRequestMessage message, string name, IReadOnlyList<string> values)
    {
        var contentKind = s_contentHeaders.Contains(name);
        var refused = false;
        foreach (var value in values)
        {
            if (!TryAdd(message, name, value, contentKind))
            {
                refused = true;
            }
        }

        if (refused)
        {
            drops.Dropped(name);
        }
    }

    private static bool TryAdd(HttpRequestMessage message, string name, string value, bool contentKind) =>
        (!contentKind && message.Headers.TryAddWithoutValidation(name, value))
        || (message.Content is { } content && content.Headers.TryAddWithoutValidation(name, value));

    private static bool IsWireSafe(string name, IReadOnlyList<string> values)
    {
        if (!HttpHeaderSyntax.IsValidName(name))
        {
            return false;
        }

        foreach (var value in values)
        {
            if (!HttpHeaderSyntax.IsValidOutboundValue(value))
            {
                return false;
            }
        }

        return true;
    }

    // Decided once, before the header loop: the loop order is not guaranteed, and the tracestate decision follows the
    // traceparent's. Nothing is allocated when there is no ambient activity.
    private static (bool Traceparent, bool Tracestate) StripTraceContext(Request request)
    {
        if (Activity.Current is not { Id: { } currentId } current
            || request.Headers.Get("traceparent") is not { } carried
            || !TraceContextStripping.ShouldStripTraceparent(carried, currentId, TraceContextStripping.RuntimeInjects(current)))
        {
            return (false, false);
        }

        var tracestate = request.Headers.Get("tracestate");
        return (true, tracestate is not null && TraceContextStripping.ShouldStripTracestate(tracestate, current.TraceStateString, traceparentStripped: true));
    }
}
