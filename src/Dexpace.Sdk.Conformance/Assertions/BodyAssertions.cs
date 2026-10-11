// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;
using System.Text;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>The response body is a lazy stream that releases its connection (<c>TRANSPORT-25</c>, <c>SEAM-11</c>), and a body-less request is valid for any method (<c>TRANSPORT-26</c>).</summary>
internal static class BodyAssertions
{
    private const int LargeBytes = 8 * 1024 * 1024;
    private const int LargeSeed = 8;
    private const int PartialRead = 64 * 1024;

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-25.lazy-body", ["TRANSPORT-25", "SEAM-11"], RequirementLevel.Must, Both, LazyBodyAsync);
        yield return Define("transport-25.large-round-trip", ["TRANSPORT-25"], RequirementLevel.Must, Both, LargeRoundTripAsync);
        yield return Define("transport-25.dispose-releases", ["TRANSPORT-25"], RequirementLevel.Must, Both, DisposeReleasesAsync);
        yield return Define("transport-26.bodyless-methods", ["TRANSPORT-26"], RequirementLevel.Must, Both, BodylessMethodsAsync);
    }

    // TRANSPORT-25, SEAM-11: the call returns once the head is in, and the first chunk is readable while the server still
    // holds the second back: a transport that buffers the body cannot return, let alone read, before the gate opens.
    private static async Task LazyBodyAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<byte[]> Chunks([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
        {
            yield return "first-"u8.ToArray();
            await gate.Task.WaitAsync(token).ConfigureAwait(false);
            yield return "second"u8.ToArray();
        }

        var server = context.StartServer(LoopbackResponse.Streamed([new("Content-Type", "application/octet-stream")], Chunks(cancellationToken)));
        var transport = context.CreateTransport();

        var call = transport.SendAsync(Request.Get(server.Url("/lazy").AbsoluteUri), cancellationToken);
        await Bounded.WaitAsync(call, context.ReleaseTimeout, "the call to return once the response head arrived while the server held the body back (TRANSPORT-25, SEAM-11: no pre-buffering)", cancellationToken).ConfigureAwait(false);
        using var response = await Check.GuardAsync(() => call, "the lazy GET", cancellationToken).ConfigureAwait(false);
        using var stream = await BodyIo.OpenAsync(response, context.Face, cancellationToken).ConfigureAwait(false);
        var buffer = new byte[16];
        var first = await BodyIo.ReadAsync(stream, buffer, context.Face, context.ReleaseTimeout, "the first chunk, readable before the server wrote the second", cancellationToken).ConfigureAwait(false);
        var head = Encoding.ASCII.GetString(buffer, 0, first);

        Check.True(first > 0 && "first-".StartsWith(head, StringComparison.Ordinal), "the first chunk must be the first bytes the server wrote", "first-", head);
        Check.True(!gate.Task.IsCompleted, "the gate must still be closed when the first chunk is read");
        gate.SetResult();
        var rest = new StringBuilder(head);
        await BodyIo.DrainAsync(stream, chunk => rest.Append(Encoding.ASCII.GetString(chunk.Span)), context.Face, context.ReleaseTimeout, "the rest of the body once the gate opened", cancellationToken).ConfigureAwait(false);

        Check.Equal(rest.ToString(), "first-second", "the whole body");
    }

    // TRANSPORT-25: eight megabytes round-trip byte for byte (digest computed as it streams).
    private static async Task LargeRoundTripAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Large(LargeBytes, LargeSeed));
        var transport = context.CreateTransport();

        using var response = await transport.ExpectResponseAsync(Request.Get(server.Url("/large").AbsoluteUri), "a GET of an 8 MiB body", cancellationToken).ConfigureAwait(false);
        using var stream = await BodyIo.OpenAsync(response, context.Face, cancellationToken).ConfigureAwait(false);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var total = await BodyIo.DrainAsync(stream, chunk => digest.AppendData(chunk.Span), context.Face, context.AssertionBound, "the 8 MiB body", cancellationToken).ConfigureAwait(false);

        Check.Equal(total, (long)LargeBytes, "the number of body bytes read");
        Check.True(digest.GetHashAndReset().AsSpan().SequenceEqual(LargeBody.Sha256(LargeBytes, LargeSeed)), "the body read must be byte-identical to the body sent (SHA-256)");
    }

    // TRANSPORT-25: abandoning a body part-way releases its connection (observed server-side, through a reuse probe).
    private static async Task DisposeReleasesAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartProbeableServer(LoopbackResponse.Large(LargeBytes, LargeSeed, keepAlive: true));
        var transport = context.CreateTransport();

        using (var response = await transport.ExpectResponseAsync(Request.Get(server.Url("/large").AbsoluteUri), "a GET of an 8 MiB body", cancellationToken).ConfigureAwait(false))
        {
            using var stream = await BodyIo.OpenAsync(response, context.Face, cancellationToken).ConfigureAwait(false);
            var partial = new byte[PartialRead];
            var read = await BodyIo.ReadAsync(stream, partial, context.Face, context.ReleaseTimeout, "the first part of the body", cancellationToken).ConfigureAwait(false);
            Check.True(read > 0, "the body must start with data");
        }

        await Check.ReleasedAsync(context, server, 0, transport, cancellationToken).ConfigureAwait(false);
    }

    // TRANSPORT-26: a body-less POST, PUT or PATCH carries a zero-length body, a GET or HEAD carries no framing at all.
    private static async Task BodylessMethodsAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        var transport = context.CreateTransport();
        Method[] methods = [Method.Post, Method.Put, Method.Patch, Method.Get, Method.Head];

        foreach (var method in methods)
        {
            using var response = await transport.ExpectResponseAsync(Request.Create(method, server.Url("/bodyless").AbsoluteUri), $"a body-less {method}", cancellationToken).ConfigureAwait(false);
        }

        var requests = server.Requests;
        Check.Equal(requests.Count, methods.Length, "the number of requests the server received");
        foreach (var request in requests)
        {
            var length = Check.HeaderValue(request, "Content-Length");
            var chunked = Check.HeaderValue(request, "Transfer-Encoding");
            Check.Equal(request.Body.Length, 0, $"the body bytes of a body-less {request.Method}");
            if (request.Method is "GET" or "HEAD")
            {
                Check.True(length is null && chunked is null, $"a body-less {request.Method} must carry neither Content-Length nor Transfer-Encoding", "no framing headers", $"Content-Length: {length ?? "-"}; Transfer-Encoding: {chunked ?? "-"}");
            }
            else
            {
                Check.True(length == "0" || chunked is not null, $"a body-less {request.Method} must carry Content-Length: 0 or an empty chunked body", "Content-Length: 0", $"Content-Length: {length ?? "-"}; Transfer-Encoding: {chunked ?? "-"}");
            }
        }

        Check.NoFaults(server);
    }
}
