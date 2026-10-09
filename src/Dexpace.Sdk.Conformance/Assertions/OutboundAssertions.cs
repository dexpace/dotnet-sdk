// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>What goes on the wire: the framing headers are the transport's to compute, never the caller's (<c>TRANSPORT-11</c>).</summary>
internal static class OutboundAssertions
{
    private const string Body = "abcde";

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-11.framing-recomputed", ["TRANSPORT-11"], RequirementLevel.Must, Both, FramingRecomputedAsync);
        yield return Define("transport-11.drop-logged", ["TRANSPORT-11"], RequirementLevel.Should, AsyncOnly, DropLoggedAsync);
    }

    // The caller sets the three framing headers to lies, plus one header that must pass through untouched.
    private static Request HostileRequest(Uri url) =>
        Request.Post(url.AbsoluteUri, RequestBody.FromString(Body, Dexpace.Sdk.Core.Http.Common.CommonMediaTypes.TextPlain))
            .WithHeader("Host", "evil.example")
            .WithHeader("Content-Length", "9999")
            .WithHeader("Transfer-Encoding", "chunked")
            .WithHeader("X-Pass", "kept");

    // TRANSPORT-11: the wire carries the real host and framing, and the pass-through header survives.
    private static async Task FramingRecomputedAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.CreateTransport();

        using var response = await transport.ExpectResponseAsync(HostileRequest(server.Url("/framing")), "a POST whose framing headers are set by the caller", cancellationToken).ConfigureAwait(false);

        var request = Assert1(server);
        Check.Equal(Check.HeaderValue(request, "Host"), $"127.0.0.1:{server.BaseUri.Port}", "the Host header on the wire");
        Check.True(!request.RawText.Contains("evil.example", StringComparison.Ordinal), "the caller's Host value must not reach the wire anywhere", "no 'evil.example'", "it was written");
        var length = Check.HeaderValues(request, "Content-Length");
        var chunked = Check.HeaderValues(request, "Transfer-Encoding");
        Check.True(
            (length.Count == 1 && length[0] == "5" && chunked.Count == 0) || (length.Count == 0 && chunked.Count == 1 && chunked[0].Contains("chunked", StringComparison.OrdinalIgnoreCase)),
            "the wire must carry Content-Length: 5 or chunked framing, computed by the transport, never the caller's values",
            "Content-Length: 5, or Transfer-Encoding: chunked without a length",
            $"Content-Length: [{string.Join(",", length)}]; Transfer-Encoding: [{string.Join(",", chunked)}]");
        Check.Equal(Encoding.UTF8.GetString(request.Body.Span), Body, "the body the server decoded");
        Check.Equal(Check.HeaderValue(request, "X-Pass"), "kept", "the pass-through header");
        Check.NoFaults(server);
    }

    // TRANSPORT-11 (SHOULD): each dropped framing header is logged by name, and no value is ever logged (XCUT-18).
    private static async Task DropLoggedAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.CreateTransport();

        using var response = await transport.ExpectResponseAsync(HostileRequest(server.Url("/logged")), "a POST whose framing headers are set by the caller", cancellationToken).ConfigureAwait(false);

        foreach (var name in new[] { "Host", "Content-Length", "Transfer-Encoding" })
        {
            Check.True(context.Logger.Dropped(name).Count > 0, $"the transport dropped the caller's '{name}' header and should have logged it by name", $"a log entry naming '{name}'", "none");
        }

        Check.True(!context.Logger.Leaks("evil.example") && !context.Logger.Leaks("9999"), "no log entry may carry a header value (XCUT-18)", "no value in any log entry", "a value was logged");
    }

    private static RecordedRequest Assert1(LoopbackServer server)
    {
        var requests = server.Requests;
        Check.Equal(requests.Count, 1, "the number of requests the server received");
        return requests[0];
    }
}
