// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 tests/conformance/xcut/error-taxonomy.conformance.test.ts and ruby-sdk@5b17395
// gems/dexpace-conformance/lib/dexpace/conformance/invariant_suite/ (error taxonomy and retry classification). The status
// matrix is the one of appendix C, XCUT-5: 408, 429 and every 5xx except 501 and 505.
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>XCUT-4</c> to <c>XCUT-8</c>: the two-branch error taxonomy, the one status classifier behind the baked flag, retry
/// classification by capability, the configured status set that is authoritative, and the strict status-to-exception factory
/// that refuses a non-error status. The transport-level clause of <c>XCUT-4</c> is <c>transport-20</c> (P10-6).
/// </summary>
internal static class TaxonomyInvariants
{
    // XCUT-5: the one classifier, as a table. 507 is outside the lists the reference tests, and bakes retryable true.
    private static readonly int[] s_classified = [400, 404, 408, 429, 500, 501, 502, 503, 504, 505, 507, 599];

    internal static ConformanceAssertion[] Assertions() =>
    [
        InvariantAssertionBuilder.Define("xcut-4.two-branches", ["XCUT-4"], RequirementLevel.Must, InvariantAssertionBuilder.Both, TwoBranchesAsync),
        InvariantAssertionBuilder.Define("xcut-5.one-classifier", ["XCUT-5"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, OneClassifierAsync),
        InvariantAssertionBuilder.Define("xcut-6.capability-queried", ["XCUT-6"], RequirementLevel.Must, InvariantAssertionBuilder.Both, CapabilityQueriedAsync),
        InvariantAssertionBuilder.Define("xcut-7.configured-set-authoritative", ["XCUT-7"], RequirementLevel.Must, InvariantAssertionBuilder.Both, ConfiguredSetAuthoritativeAsync),
        InvariantAssertionBuilder.Define("xcut-8.strict-factory-rejects", ["XCUT-8"], RequirementLevel.Must, InvariantAssertionBuilder.NoFace, StrictFactoryRejectsAsync),
        InvariantAssertionBuilder.Define("xcut-8.convenience-returns-response", ["XCUT-8"], RequirementLevel.Must, InvariantAssertionBuilder.Both, ConvenienceReturnsResponseAsync),
    ];

    // Branch (a) carries a fully received response with status, headers and body; branch (b) carries none and is retryable, with
    // an I/O-family cause. "Catchable as a generic I/O error" is the judged part of §10 entry 7 and is not asserted (P10-25).
    private static async Task TwoBranchesAsync(InvariantContext context, CancellationToken token)
    {
        var payload = Encoding.UTF8.GetBytes("no such thing");
        var answering = context.Transport(ProbeTransport.Behavior.Of((request, _) => new Response(
            request,
            Status.FromCode(404),
            Protocol.Http11,
            new Headers.Builder().Add("X-Probe", "yes").Build(),
            ResponseBody.FromBytes(payload),
            null)));
        var protocol = InvariantContext.WithErrorMapping(context.Build(answering, InvariantOptions.Retrying(0)));

        var received = await context.FailureOfAsync(protocol, Request.Get(InvariantOptions.Url), "the call answered with a 404", token).ConfigureAwait(false);

        var http = received as HttpResponseException
            ?? throw new ConformanceException("an error status must surface as the response-carrying error", "HttpResponseException", Outcome.Name(received));
        Check.Equal(http.Status.Code, 404, "the status the response-carrying error exposes");
        Check.Equal(http.Response.Headers.Get("X-Probe"), "yes", "a header of the received response, exposed by the error");
        var body = await Check.GuardAsync(() => http.Response.Body.ReadAsBytesAsync(token), "reading the error's body", token).ConfigureAwait(false);
        Check.SequenceEqual(body, payload, "the body the response-carrying error exposes");

        var refusing = context.Transport(ProbeTransport.Behavior.Of((_, _) =>
            throw new ServiceRequestException("connection refused", new SocketException((int)SocketError.ConnectionRefused))));
        var transport = context.Build(refusing, InvariantOptions.Retrying(0));

        var refused = await context.FailureOfAsync(transport, Request.Get(InvariantOptions.Url), "the call whose connection is refused", token).ConfigureAwait(false);

        Check.True(refused is SdkException and not HttpResponseException, "a failure with no response must be the transport branch, not a response-carrying error", "an SDK exception carrying no response", Outcome.Name(refused));
        Check.True(refused is IRetryableError { IsRetryable: true }, "a transport error must report itself always retryable", "retryable", Outcome.Name(refused));
        Check.True(
            Outcome.Find<SocketException>(refused) is not null || Outcome.Find<IOException>(refused) is not null,
            "a transport error must keep the I/O-family cause it was raised for",
            "a SocketException or IOException in the cause chain",
            Outcome.Name(refused));
    }

    // The baked flag comes from one classifier: 408, 429 and every 5xx except 501 and 505 are retryable, nothing else is.
    private static Task OneClassifierAsync(InvariantContext context, CancellationToken token)
    {
        var mismatches = new List<string>();
        foreach (var code in s_classified)
        {
            using var response = new Response(Request.Get(InvariantOptions.Url), Status.FromCode(code), Protocol.Http11);
            var baked = context.Subject.CreateResponseException(response).IsRetryable;
            var expected = code is 408 or 429 or (>= 500 and <= 599 and not 501 and not 505);
            if (baked != expected)
            {
                mismatches.Add(string.Create(CultureInfo.InvariantCulture, $"{code} baked {baked} (the classifier says {expected})"));
            }
        }

        Check.True(
            mismatches.Count == 0,
            "the baked retryable flag must follow the one classifier: 408, 429 and every 5xx except 501 and 505",
            "no mismatch over " + string.Join(", ", s_classified),
            string.Join("; ", mismatches));
        return Task.CompletedTask;
    }

    // A type the classifier has never seen, declaring itself retryable, is retried; one declaring itself not retryable is not.
    private static async Task CapabilityQueriedAsync(InvariantContext context, CancellationToken token)
    {
        foreach (var retryable in new[] { true, false })
        {
            var transport = context.Transport(ProbeTransport.Behavior.Of((_, _) => throw new ProbeRetryableError(retryable)));
            var pipeline = context.Build(transport, InvariantOptions.Retrying(2));

            var failure = await context.FailureOfAsync(pipeline, Request.Get(InvariantOptions.Url), "a call failing with an unknown error type", token).ConfigureAwait(false);

            Check.True(Outcome.Find<ProbeRetryableError>(failure) is not null, "the unknown error must surface to the caller once the retries end", "the unknown error type", Outcome.Name(failure));
            Check.Equal(
                transport.Sends,
                retryable ? 3 : 1,
                retryable
                    ? "the sends of an unknown error type declaring itself retryable (retry decisions query the capability, not a concrete type)"
                    : "the sends of an unknown error type declaring itself not retryable");
        }
    }

    // The configured set decides, not the baked flag: widen it with 501, narrow it by 500, and the same set re-classifies a
    // re-sent error status.
    private static async Task ConfiguredSetAuthoritativeAsync(InvariantContext context, CancellationToken token)
    {
        await ExpectSendsAsync(context, [501], [501], 3, "a 501 in the configured set (its baked flag is false)", token).ConfigureAwait(false);
        await ExpectSendsAsync(context, [408, 429, 502, 503, 504], [500], 1, "a 500 removed from the configured set (its baked flag is true)", token).ConfigureAwait(false);
        await ExpectSendsAsync(context, [503], [503, 501, 200], 2, "a 503 then a 501 with only 503 configured (the re-sent 501 is not re-classified as a failure)", token).ConfigureAwait(false);
        await ExpectSendsAsync(context, [503, 501], [503, 501, 200], 3, "a 503 then a 501 with both configured (the re-sent 501 is re-classified as a failure)", token).ConfigureAwait(false);
    }

    private static async Task ExpectSendsAsync(InvariantContext context, int[] configured, int[] script, int expectedSends, string what, CancellationToken token)
    {
        var transport = context.Transport(ProbeTransport.Behavior.Of((request, send) => ProbeTransport.Empty(request, script[Math.Min(send, script.Length) - 1])));
        var options = new DexpaceClientOptions { Retry = InvariantOptions.Fast(2) with { RetryableStatusCodes = new HashSet<int>(configured) } };
        var pipeline = context.Build(transport, options);

        await context.FailureOfAsync(pipeline, Request.Get(InvariantOptions.Url), what, token).ConfigureAwait(false);

        Check.Equal(transport.Sends, expectedSends, $"the sends for {what}");
    }

    // The strict factory refuses 1xx, 2xx and 3xx with an argument error and maps an error status to the exception that carries it.
    private static Task StrictFactoryRejectsAsync(InvariantContext context, CancellationToken token)
    {
        var strict = InvariantContext.RequireHook(context.Subject.MapStrict, nameof(InvariantSubject.MapStrict));
        foreach (var code in new[] { 100, 200, 204, 302, 304 })
        {
            using var response = new Response(Request.Get(InvariantOptions.Url), Status.FromCode(code), Protocol.Http11);
            var (made, thrown) = Attempt(strict, response);
            Check.True(
                thrown is ArgumentException,
                $"the strict factory must raise an argument error for status {code} rather than fabricate a \"successful exception\"",
                "ArgumentException",
                made is not null ? "returned " + made.GetType().Name : Outcome.Name(thrown));
        }

        foreach (var code in new[] { 404, 503 })
        {
            using var response = new Response(Request.Get(InvariantOptions.Url), Status.FromCode(code), Protocol.Http11);
            var (made, thrown) = Attempt(strict, response);
            Check.True(made is not null, $"the strict factory must map the error status {code}", "an exception for the status", Outcome.Name(thrown));
        }

        return Task.CompletedTask;
    }

    // The convenience form returns a non-error response as it is, through the pipeline with error mapping on.
    private static async Task ConvenienceReturnsResponseAsync(InvariantContext context, CancellationToken token)
    {
        foreach (var code in new[] { 200, 201, 204, 304 })
        {
            var transport = context.Transport(ProbeTransport.Behavior.Status(code));
            var pipeline = InvariantContext.WithErrorMapping(context.Build(transport, InvariantOptions.Retrying(0)));

            using var response = await Check.GuardAsync(
                () => context.SendAsync(pipeline, Request.Get(InvariantOptions.Url), token),
                $"a call answered with the non-error status {code}",
                token).ConfigureAwait(false);

            Check.Equal(response.Status.Code, code, $"the status of the response returned for a {code}");
        }
    }

    private static (Exception? Made, Exception? Thrown) Attempt(Func<Response, Exception> factory, Response response)
    {
        try
        {
            return (factory(response), null);
        }
#pragma warning disable CA1031 // The point of the method: whatever the factory raises is the answer.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return (null, ex);
        }
    }
}
