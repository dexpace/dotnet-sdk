// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>Inbound leniency (<c>TRANSPORT-27</c>) and the exact-length request body (<c>HTTP-39</c>).</summary>
internal static class InboundAssertions
{
    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-27.inbound-downgrade", ["TRANSPORT-27"], RequirementLevel.Should, Both, InboundDowngradeAsync);
        yield return Define("http-39.short-source-fails", ["HTTP-39"], RequirementLevel.Must, Both, ShortSourceFailsAsync);
    }

    // TRANSPORT-27: a malformed Content-Type is no media type, an unusable Content-Length is an unknown length, and in every
    // case the body still reads.
    private static async Task InboundDowngradeAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateTransport();
        (string Reply, bool NoType, bool UnknownLength, string Name)[] cases =
        [
            ("HTTP/1.1 200 OK\r\nContent-Type: text/plain; foo\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello", true, false, "a malformed Content-Type"),
            ("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: abc\r\nConnection: close\r\n\r\nhello", false, true, "a non-numeric Content-Length"),
            ("HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nhello", true, true, "neither a Content-Type nor a Content-Length"),
        ];

        foreach (var (reply, noType, unknownLength, name) in cases)
        {
            var server = context.StartServer(LoopbackResponse.Raw(reply));

            using var response = await transport.ExpectResponseAsync(Request.Get(server.Url("/inbound").AbsoluteUri), $"a GET answered with {name}", cancellationToken).ConfigureAwait(false);
            Check.True(!noType || response.Body.ContentType is null, $"{name} must give no media type, not a failure", "ContentType == null", response.Body.ContentType?.ToString());
            Check.True(!unknownLength || response.Body.ContentLength == -1, $"{name} must give an unknown length", "ContentLength == -1", response.Body.ContentLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var body = Encoding.UTF8.GetString(await response.ReadBodyAsync($"the body of the response with {name}", cancellationToken).ConfigureAwait(false));
            Check.Equal(body, "hello", $"the body of the response with {name}");
        }
    }

    // HTTP-39: a source shorter than the declared length fails the send, and the server never sees a complete body.
    private static async Task ShortSourceFailsAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("never needed"));
        var transport = context.CreateTransport();
        using var source = new MemoryStream(new byte[5]);

        var call = transport.SendAsync(Request.Post(server.Url("/short").AbsoluteUri, RequestBody.FromStream(source, contentLength: 10)), cancellationToken);
        var failure = await Outcome.OfAsync(call, context.ReleaseTimeout, "the send of a body shorter than its declared length to end", cancellationToken).ConfigureAwait(false);

        Check.True(failure is not null, "a source shorter than its declared length must fail the send, not complete it (HTTP-39)", "a failed send", "a completed send");
        var shortRead = Outcome.Find<EndOfStreamException>(failure);
        Check.True(shortRead is not null && shortRead.Message.Contains("of 10", StringComparison.Ordinal), "the failure's cause chain must hold the end-of-stream error naming the declared length", "EndOfStreamException: ... of 10", Outcome.Name(failure));
        Check.True(server.Requests.All(request => request.Body.Length < 10), "the server must not receive a complete 10-byte body");
    }
}
