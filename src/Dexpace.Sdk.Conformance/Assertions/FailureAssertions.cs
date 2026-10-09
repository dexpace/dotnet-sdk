// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net.Http;
using System.Net.Sockets;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>When no response arrives, the failure says so and is retryable (<c>TRANSPORT-20</c>); a failed adaptation still releases the exchange (<c>TRANSPORT-22</c>).</summary>
internal static class FailureAssertions
{
    private const int AdaptedBytes = 4 * 1024 * 1024;

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-20.no-response-is-retryable", ["TRANSPORT-20", "XCUT-4"], RequirementLevel.Must, Both, NoResponseIsRetryableAsync);
        yield return Define("transport-20.retried-by-the-pipeline", ["TRANSPORT-20"], RequirementLevel.Must, AsyncOnly, RetriedByThePipelineAsync);
        yield return Define("transport-22.adaptation-failure-releases", ["TRANSPORT-22"], RequirementLevel.Must, AsyncOnly, AdaptationFailureReleasesAsync);
    }

    // TRANSPORT-20: a refused connection and a reset before any response byte are each a retryable SDK exception whose
    // cause is an I/O-family error (design section 10 entry 7), never a cancellation.
    private static async Task NoResponseIsRetryableAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateTransport();
        var reset = context.StartServer(_ => LoopbackResponse.Abort());
        (string Name, Uri Url)[] targets =
        [
            ("a refused connection", new Uri($"http://127.0.0.1:{FreePort.Release()}/")),
            ("a connection reset before any response byte", reset.Url("/reset")),
        ];

        foreach (var (name, url) in targets)
        {
            var failure = await Outcome.OfAsync(transport.SendAsync(Request.Get(url.AbsoluteUri), cancellationToken), context.AssertionBound, $"the call over {name} to end", cancellationToken).ConfigureAwait(false);

            Check.True(failure is SdkException, $"{name} must fail with an SdkException", "an SdkException", Outcome.Name(failure));
            Check.True(failure is SdkException { IsRetryable: true }, $"{name} must be retryable: no response means nothing was processed (TRANSPORT-20, XCUT-4)", "IsRetryable == true", "false");
            Check.True(
                Outcome.Find<IOException>(failure) is not null || Outcome.Find<HttpRequestException>(failure) is not null || Outcome.Find<SocketException>(failure) is not null || Outcome.Find<TimeoutException>(failure) is not null,
                $"the cause of {name} must be an I/O-family error (design section 10 entry 7)",
                "IOException, HttpRequestException, SocketException or TimeoutException in the cause chain",
                Outcome.Name(failure));
            Check.True(Outcome.Find<OperationCanceledException>(failure) is null, $"{name} is a transport failure and must not carry a cancellation");
        }
    }

    // TRANSPORT-20, phase 6a hand-off: a failure the classifier calls retryable is retried by the pipeline until a response arrives.
    private static async Task RetriedByThePipelineAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var requests = 0;
        var server = context.StartServer(_ => Interlocked.Increment(ref requests) <= 2 ? LoopbackResponse.Abort() : LoopbackResponse.Ok("done"));
        var transport = context.CreateAsyncTransport();
        using var pipeline = DexpacePipeline.CreateDefault(transport.Client);
        var options = new DexpaceClientOptions
        {
            Retry = new RetryOptions { MaxRetryAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(10), MaxDelay = TimeSpan.FromMilliseconds(50), Jitter = 0 },
        };

        using var response = await Check.GuardAsync(
            () => pipeline.SendAsync(Request.Get(server.Url("/flaky").AbsoluteUri), options, cancellationToken).AsTask(),
            "a GET through the default pipeline over a server that resets its first connections",
            cancellationToken).ConfigureAwait(false);
        var body = System.Text.Encoding.UTF8.GetString(await response.ReadBodyAsync("the body after the retries", cancellationToken).ConfigureAwait(false));

        Check.Equal(response.Status.Code, 200, "the status after the pipeline retried");
        Check.Equal(body, "done", "the body after the pipeline retried");
        Check.True(server.ConnectionCount >= 2, "the pipeline must have retried on a new connection", "at least 2 connections", server.ConnectionCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Check.NoFaults(server);
    }

    // TRANSPORT-22: when adapting a live native response throws, the native response is released first. The reply is large
    // and keep-alive, so a pool cannot quietly take the connection back: only a released response closes it.
    private static async Task AdaptationFailureReleasesAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Large(AdaptedBytes, seed: 22, keepAlive: true));
        var transport = context.RequireFaultingAdaptation();

        var failure = await Outcome.OfAsync(transport.SendAsync(Request.Get(server.Url("/adapt").AbsoluteUri), cancellationToken), context.AssertionBound, "the call whose adaptation fails to end", cancellationToken).ConfigureAwait(false);

        Check.True(failure is not null, "the faulting-adaptation hook must make the send fail (it completed instead, so the clause cannot be shown)", "a failed send", "a completed send");
        await Check.ReleasedAsync(context, server, 0, probe: null, cancellationToken).ConfigureAwait(false);
    }
}
