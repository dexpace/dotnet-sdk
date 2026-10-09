// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Microsoft.Extensions.Logging;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Header mapping: the caller's <c>Content-Type</c> is authoritative (<c>TRANSPORT-10</c>), a header the native client
/// refuses is dropped alone and never silently (<c>TRANSPORT-12</c>, <c>TRANSPORT-13</c>), and a malformed inbound header
/// costs only itself (<c>TRANSPORT-14</c>, split into its three clauses so a transport that cannot meet one can waive that one,
/// the amendment phase 8b asked for). The rows belong to phase 8b; the assertions are 8a's.
/// </summary>
internal static class HeaderAssertions
{
    private static readonly string[] s_contentClassHeaders = ["Content-Language", "Content-Encoding", "Allow"];

    private static readonly (string Name, string Value)[] s_awkwardButValid =
    [
        ("X-Awkward_Name", "underscore"),
        ("X-Spaced", "a  b\tc"),
        ("X-Quoted", "\"quoted\"; v=1"),
        ("X-Long", new string('v', 2000)),
    ];

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-10.content-type-authoritative", ["TRANSPORT-10"], RequirementLevel.Must, Both, ContentTypeAuthoritativeAsync);
        yield return Define("transport-12.native-rejected-header-dropped", ["TRANSPORT-12"], RequirementLevel.Must, Both, NativeRejectedHeaderDroppedAsync);
        yield return Define("transport-13.drop-log-once-per-name", ["TRANSPORT-13", "OBS-19"], RequirementLevel.Should, AsyncOnly, DropLogOncePerNameAsync);
        yield return Define("transport-14.value-control-dropped", ["TRANSPORT-14"], RequirementLevel.Must, Both, ValueControlDroppedAsync);
        yield return Define("transport-14.obs-text-preserved", ["TRANSPORT-14"], RequirementLevel.Should, Both, ObsTextPreservedAsync);
        yield return Define("transport-14.name-malformed-dropped", ["TRANSPORT-14"], RequirementLevel.Must, Both, NameMalformedDroppedAsync);
    }

    // TRANSPORT-10: the caller's Content-Type is on the wire exactly once and the body's is used only when the caller set none.
    private static async Task ContentTypeAuthoritativeAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.CreateTransport();
        var json = RequestBody.FromString("{}", CommonMediaTypes.ApplicationJson);
        var untyped = RequestBody.FromBytes(Encoding.UTF8.GetBytes("{}"));
        (string Name, Request Request, Func<string?, bool> Accepts, string Expected)[] cases =
        [
            ("a caller type over a typed body", Request.Post(server.Url("/a").AbsoluteUri, json).WithHeader("Content-Type", "application/vnd.x+json"), value => value == "application/vnd.x+json", "application/vnd.x+json"),
            ("a typed body and no caller type", Request.Post(server.Url("/b").AbsoluteUri, json), value => value?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true, "application/json"),
            ("a caller type over an untyped body", Request.Post(server.Url("/c").AbsoluteUri, untyped).WithHeader("Content-Type", "application/vnd.y"), value => value == "application/vnd.y", "application/vnd.y"),
        ];

        foreach (var (name, request, accepts, expected) in cases)
        {
            using var response = await transport.ExpectResponseAsync(request, name, cancellationToken).ConfigureAwait(false);
        }

        var recorded = server.Requests;
        Check.Equal(recorded.Count, cases.Length, "the requests the server received");
        for (var i = 0; i < cases.Length; i++)
        {
            var values = Check.HeaderValues(recorded[i], "Content-Type");
            Check.True(values.Count == 1 && cases[i].Accepts(values[0]), $"{cases[i].Name}: the wire must carry the one Content-Type the clause names (TRANSPORT-10)", cases[i].Expected, string.Join(" | ", values));
        }
    }

    // TRANSPORT-12: a header the native client refuses is dropped alone: every header is on the wire or logged by name as dropped,
    // no send throws, and the rest of the request goes out. F7: a body-less request used to lose its content-class headers unseen.
    private static async Task NativeRejectedHeaderDroppedAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.CreateTransport();
        foreach (var bodied in new[] { false, true })
        {
            var request = bodied
                ? Request.Post(server.Url("/bodied").AbsoluteUri, RequestBody.FromString("payload", CommonMediaTypes.TextPlain))
                : Request.Get(server.Url("/bodyless").AbsoluteUri);
            request = s_contentClassHeaders.Aggregate(request, (current, name) => current.WithHeader(name, name == "Allow" ? "GET" : "identity"));
            request = s_awkwardButValid.Aggregate(request, (current, header) => current.WithHeader(header.Name, header.Value));

            using var response = await transport.ExpectResponseAsync(request, $"a {(bodied ? "bodied" : "body-less")} request carrying headers the native client may refuse", cancellationToken).ConfigureAwait(false);
        }

        var recorded = server.Requests;
        Check.Equal(recorded.Count, 2, "the requests the server received (the rest of each request must still be dispatched)");
        foreach (var request in recorded)
        {
            foreach (var name in s_contentClassHeaders.Concat(s_awkwardButValid.Select(header => header.Name)))
            {
                var onWire = request.HeaderValues(name).Count > 0;
                var logged = context.Logger.Dropped(name).Count > 0;
                Check.True(onWire || logged, $"the '{name}' header of the {request.Target} request must be on the wire or logged as dropped, never silently lost (TRANSPORT-12)", "on the wire, or a log entry naming it", "neither");
            }
        }
    }

    // TRANSPORT-13 (SHOULD): under the default policy the first drop of each name is logged loudly and the rest are quiet, with
    // names compared case-insensitively. "Loudly" is Warning or above.
    private static async Task DropLogOncePerNameAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.CreateTransport();
        (string First, string Second)[] sends = [("Content-Language", "Allow"), ("content-language", "ALLOW"), ("Content-Language", "Allow")];

        foreach (var (first, second) in sends)
        {
            var request = Request.Get(server.Url("/drop").AbsoluteUri).WithHeader(first, "en").WithHeader(second, "GET");
            using var response = await transport.ExpectResponseAsync(request, "a body-less request dropping two content-class header names", cancellationToken).ConfigureAwait(false);
        }

        foreach (var name in new[] { "Content-Language", "Allow" })
        {
            var loud = context.Logger.Dropped(name).Count(entry => entry.Level >= LogLevel.Warning);
            Check.True(loud == 1, $"under the default policy the first drop of '{name}' is logged loudly and every later drop quietly, across three sends and two casings (TRANSPORT-13)", "exactly 1 entry at Warning or above", loud.ToString(CultureInfo.InvariantCulture));
        }
    }

    // Serves one raw reply and returns the delivered response head for the inbound-header assertions.
    private static async Task<(Dexpace.Sdk.Core.Http.Response.Response Response, string Body)> InboundAsync(SuiteContext context, IFaceTransport transport, string reply, string what, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Raw(reply));
        var response = await transport.ExpectResponseAsync(Request.Get(server.Url("/inbound").AbsoluteUri), what, cancellationToken).ConfigureAwait(false);
        var body = Encoding.UTF8.GetString(await response.ReadBodyAsync($"the body of {what}", cancellationToken).ConfigureAwait(false));
        return (response, body);
    }

    // TRANSPORT-14, value half: a control byte in a field value costs only that header.
    private static async Task ValueControlDroppedAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateTransport();

        var (response, body) = await InboundAsync(context, transport, "HTTP/1.1 200 OK\r\nX-Control: a\u0001b\r\nX-Fine: ok\r\nConnection: close\r\n\r\nhello", "a reply with a control byte in a header value", cancellationToken).ConfigureAwait(false);
        using var scope = response;

        Check.True(!response.Headers.Contains("X-Control"), "a header whose value carries a control byte must be dropped (TRANSPORT-14)", "X-Control absent", "present");
        Check.Equal(response.Headers.Get("X-Fine"), "ok", "the header next to the malformed one");
        Check.Equal(body, "hello", "the body delivered with the malformed header dropped");
    }

    // TRANSPORT-14, obs-text SHOULD: a non-ASCII byte in a value is preserved, not stripped.
    private static async Task ObsTextPreservedAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateTransport();

        var (response, body) = await InboundAsync(context, transport, "HTTP/1.1 200 OK\r\nX-Obs: café\r\nConnection: close\r\n\r\nhello", "a reply with an obs-text byte in a header value", cancellationToken).ConfigureAwait(false);
        using var scope = response;

        Check.Equal(response.Headers.Get("X-Obs"), "café", "the value carrying an obs-text byte (RFC 9110 permits it; a Latin-1 filename must survive)");
        Check.Equal(body, "hello", "the body of the reply with obs-text");
    }

    // TRANSPORT-14, name half: a space, a control byte or a non-ASCII byte in a field name costs only that header. A native
    // client that rejects the whole response before the adapter sees it (F1) cannot meet this; it waives this assertion alone.
    private static async Task NameMalformedDroppedAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateTransport();
        foreach (var (name, label) in new[] { ("Bad Name", "a space"), ("Bad\u0001Name", "a control byte"), ("Café", "a non-ASCII byte") })
        {
            var (response, body) = await InboundAsync(context, transport, $"HTTP/1.1 200 OK\r\n{name}: x\r\nX-Fine: ok\r\nConnection: close\r\n\r\nhello", $"a reply with {label} in a header name", cancellationToken).ConfigureAwait(false);
            using var scope = response;

            Check.Equal(response.Headers.Get("X-Fine"), "ok", $"the header next to the one with {label} in its name");
            Check.Equal(body, "hello", $"the body of the reply with {label} in a header name");
        }
    }
}
