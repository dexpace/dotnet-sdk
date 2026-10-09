// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A transport shared by concurrent callers keeps their calls apart (<c>TRANSPORT-29</c>), and never re-sends a request behind
/// the caller's back (<c>TRANSPORT-2</c>): not a single-use body, which cannot be written twice, and not a body-less request,
/// which a native client re-sends silently after a reset that comes before any response byte.
/// </summary>
internal static class ConcurrencyAssertions
{
    private const string WarmPath = "/warm";
    private const int Callers = 64;
    private const int BodyBytes = 1024;

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-2.no-silent-resend", ["TRANSPORT-2"], RequirementLevel.Must, Both, NoSilentResendAsync);
        yield return Define("transport-2.bodyless-not-resent", ["TRANSPORT-2"], RequirementLevel.Must, Both, BodylessNotResentAsync);
        yield return Define("transport-29.concurrent-no-crosstalk", ["TRANSPORT-29", "SEAM-12", "ASYNC-22"], RequirementLevel.Must, Both, ConcurrentNoCrosstalkAsync);
    }

    // TRANSPORT-2, the spec's own conformance sentence ("with a single-use body and a first-attempt connection failure, assert the
    // native client does not silently re-send"): a single-use body whose exchange fails after it was written is not written a second
    // time by the transport. Passing is not proof that the native client's retry is off: a native client never re-writes content it
    // wrote, whether or not its retry is on, and a StreamConsumedException from a refused second write is accepted here although it is
    // itself a sign the transport tried. The clause's other half, a request with no content to refuse, is transport-2.bodyless-not-resent.
    private static async Task NoSilentResendAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(request => request.Target == WarmPath ? LoopbackResponse.Ok("warm", keepAlive: true) : LoopbackResponse.Abort());
        var transport = context.CreateTransport();
        await WarmAsync(server, transport, cancellationToken).ConfigureAwait(false);

        using var source = CountingStream.OfBytes(new byte[BodyBytes]);
        var call = transport.SendAsync(Request.Post(server.Url("/single").AbsoluteUri, RequestBody.FromStream(source, contentLength: -1)), cancellationToken);
        var failure = await Outcome.OfAsync(call, context.AssertionBound, "the send of a single-use body to a server that resets the connection to end", cancellationToken).ConfigureAwait(false);

        Check.True(failure is not null, "a send whose connection was reset before any response must fail, not succeed", "a failed send", "a completed send");
        Check.True(
            failure is SdkException { IsRetryable: true } || Outcome.Find<StreamConsumedException>(failure) is not null,
            "the failure must be a retryable SDK exception, or carry StreamConsumedException from a refused second write",
            "retryable SdkException or StreamConsumedException",
            Outcome.Name(failure));
        var written = server.Requests.Where(request => request.Method == "POST").Sum(request => (long)request.Body.Length);
        Check.True(written <= BodyBytes, "the server must have received the single-use body at most once across every connection", $"at most {BodyBytes} bytes", $"{written} bytes");
        Check.True(source.BytesRead <= BodyBytes, "the single-use source must have been read at most once", $"at most {BodyBytes} bytes", $"{source.BytesRead} bytes");
    }

    // TRANSPORT-2: the native client's own retry is off, so a request that carries nothing a second write could be refused for (a POST,
    // a DELETE or a GET with no body) reaches the server exactly once when the connection is reset after the server read it and before
    // any response byte. Native clients re-send such a request silently, on a warmed keep-alive connection and on a new one, so the
    // pipeline's retry budget is spent behind its back and a non-idempotent request is delivered more than once.
    private static async Task BodylessNotResentAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(request => request.Target == WarmPath ? LoopbackResponse.Ok("warm", keepAlive: true) : LoopbackResponse.Abort());
        var transport = context.CreateTransport();

        foreach (var method in new[] { Method.Post, Method.Delete, Method.Get })
        {
            await WarmAsync(server, transport, cancellationToken).ConfigureAwait(false);
            var path = $"/bodyless-{method}";
            var failure = await Outcome.OfAsync(
                transport.SendAsync(Request.Create(method, server.Url(path).AbsoluteUri), cancellationToken),
                context.AssertionBound,
                $"the body-less {method} to a server that resets the connection to end",
                cancellationToken).ConfigureAwait(false);

            Check.True(failure is not null, $"a body-less {method} whose connection was reset before any response must fail, not succeed", "a failed send", "a completed send");
            var arrivals = server.Requests.Count(request => request.Target == path);
            Check.True(
                arrivals == 1,
                $"a body-less {method} must reach the server exactly once: the native client's own retry must be off, so the pipeline is the only retry authority (TRANSPORT-2)",
                "1 arrival",
                arrivals.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    // A GET that warms the transport's connection: a pooling client now holds a keep-alive connection to reuse for the next request.
    private static async Task WarmAsync(LoopbackServer server, IFaceTransport transport, CancellationToken cancellationToken)
    {
        using var warm = await transport.ExpectResponseAsync(Request.Get(server.Url(WarmPath).AbsoluteUri), "a GET that warms the transport's connection", cancellationToken).ConfigureAwait(false);
        _ = await warm.ReadBodyAsync("the warm-up body", cancellationToken).ConfigureAwait(false);
    }

    // TRANSPORT-29, SEAM-12, ASYNC-22: many callers on one transport each get their own response.
    private static async Task ConcurrentNoCrosstalkAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.EchoId);
        var transport = context.CreateTransport();

        var calls = Enumerable.Range(0, Callers).Select(async i =>
        {
            var id = $"id-{i}";
            using var response = await transport.ExpectResponseAsync(
                Request.Get(server.Url($"/echo?i={i}").AbsoluteUri).WithHeader("X-Conformance-Id", id),
                $"concurrent call {i}",
                cancellationToken).ConfigureAwait(false);
            var body = System.Text.Encoding.UTF8.GetString(await response.ReadBodyAsync($"the body of concurrent call {i}", cancellationToken).ConfigureAwait(false));
            return (Id: id, Body: body, Query: response.Request.Url.Query, Expected: $"?i={i}");
        }).ToArray();
        var results = await Bounded.WaitForAllAsync(calls, context.AssertionBound, $"the {Callers} concurrent calls", cancellationToken).ConfigureAwait(false);

        foreach (var result in results)
        {
            Check.Equal(result.Body, result.Id, $"the response body delivered to the caller that sent {result.Id}");
            Check.Equal(result.Query, result.Expected, $"the request the response delivered to {result.Id} reports it answers");
        }

        Check.NoFaults(server);
    }
}
