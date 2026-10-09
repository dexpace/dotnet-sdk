// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Cancellation: it is terminal and not retryable (<c>TRANSPORT-3</c>, <c>XCUT-1</c>), it reaches the exchange and releases it
/// (<c>TRANSPORT-7</c>), the pipeline's per-attempt deadline rides the same token (the phase 6a hand-off), and a cancel after
/// delivery leaves the response readable (<c>ASYNC-20</c>).
/// </summary>
internal static class CancellationAssertions
{
    private const int LateCancelBytes = 1024 * 1024;

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define(
            "transport-3.cancel-is-terminal",
            ["TRANSPORT-3", "SEAM-13", "XCUT-1"],
            RequirementLevel.Must,
            Both,
            CancelIsTerminalAsync);
        yield return Define(
            "transport-7.cancel-releases-exchange",
            ["TRANSPORT-7"],
            RequirementLevel.Must,
            AsyncOnly,
            CancelReleasesExchangeAsync);
        yield return Define(
            "transport-7.attempt-timeout-aborts",
            ["TRANSPORT-7"],
            RequirementLevel.Must,
            AsyncOnly,
            AttemptTimeoutAbortsAsync);
        yield return Define(
            "async-20.late-cancel-leaves-response-open",
            ["ASYNC-20", "SEAM-16"],
            RequirementLevel.Must,
            AsyncOnly,
            LateCancelLeavesResponseOpenAsync);
    }

    // Starts a call against a server that never answers, waits until the server has the request, and cancels.
    private static async Task<(LoopbackServer Server, Exception? Failure, CancellationTokenSource Cancelled)> CancelledHangAsync(
        SuiteContext context,
        CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Hang());
        var transport = context.CreateTransport();
        var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var call = transport.SendAsync(Request.Get(server.Url("/hang").AbsoluteUri), cancelled.Token);
        await Bounded.WaitAsync(server.WaitForRequestAsync(0, cancellationToken), context.ReleaseTimeout, "the server to receive the request", cancellationToken).ConfigureAwait(false);

        await cancelled.CancelAsync().ConfigureAwait(false);
        var failure = await Outcome.OfAsync(call, context.ReleaseTimeout, "the call to end after its token was signalled", cancellationToken).ConfigureAwait(false);
        return (server, failure, cancelled);
    }

    // TRANSPORT-3, XCUT-1, SEAM-13: a cancelled call ends in a cancellation that carries the caller's own token (a caller that
    // filters on ex.CancellationToken must recognise it), the flag stays set, and it is never retryable.
    private static async Task CancelIsTerminalAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var (_, failure, cancelled) = await CancelledHangAsync(context, cancellationToken).ConfigureAwait(false);
        using var scope = cancelled;

        Check.True(failure is OperationCanceledException, "a cancelled call must end in an OperationCanceledException, not in a transport failure", "OperationCanceledException", Outcome.Name(failure));
        Check.True(
            Outcome.FindCancellationOf(failure, cancelled.Token) is not null,
            "the OperationCanceledException must carry the caller's own token (or one in its cause chain must), so a caller that filters on ex.CancellationToken still recognises its own cancellation (TRANSPORT-3, XCUT-1)",
            "a cancellation whose CancellationToken is the caller's token",
            failure is OperationCanceledException { CancellationToken.CanBeCanceled: true } ? "a cancellation carrying some other token" : "a cancellation carrying no token");
        Check.True(cancelled.Token.IsCancellationRequested, "the caller's cancellation flag must still be set after the call ended (XCUT-1)");
        Check.True(failure is not SdkException { IsRetryable: true }, "a cancellation must never be a retryable failure (XCUT-1)", "a non-retryable cancellation", Outcome.Name(failure));
    }

    // TRANSPORT-7: the cancel reaches the exchange, which the client releases: the server sees the connection go.
    private static async Task CancelReleasesExchangeAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var (server, failure, cancelled) = await CancelledHangAsync(context, cancellationToken).ConfigureAwait(false);
        using var scope = cancelled;

        Check.True(failure is OperationCanceledException, "the call must end cancelled before its exchange can be judged released", "OperationCanceledException", Outcome.Name(failure));
        await Check.ReleasedAsync(context, server, 0, probe: null, cancellationToken).ConfigureAwait(false);
        Check.NoFaults(server);
    }

    // TRANSPORT-7, phase 6a hand-off: a pipeline attempt deadline is the attempt token firing, and the transport honours it as a
    // timeout: the failure is an SDK exception that is, or whose causes or trail hold, a timeout (not any SDK failure).
    private static async Task AttemptTimeoutAbortsAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Hang());
        var transport = context.CreateAsyncTransport();
        using var pipeline = DexpacePipeline.CreateDefault(transport.Client);
        var options = new DexpaceClientOptions
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(300),
            Retry = new RetryOptions { MaxRetryAttempts = 0 },
        };

        var call = pipeline.SendAsync(Request.Get(server.Url("/hang").AbsoluteUri), options, cancellationToken).AsTask();
        var failure = await Outcome.OfAsync(call, context.ReleaseTimeout, "the attempt to end after its 300 ms deadline", cancellationToken).ConfigureAwait(false);

        Check.True(failure is SdkException, "an attempt that outlives its deadline must fail with an SDK exception, not hang or surface a bare cancellation", "an SdkException", Outcome.Name(failure));
        Check.True(
            Outcome.FindTimeout(failure) is not null,
            "the transport must honour the attempt-linked token as a timeout: the failure, its cause chain or its trail must hold a timeout, not an unrelated SDK failure (the phase 6a hand-off, XCUT-2)",
            "an OperationTimeoutException, ServiceRequestTimeoutException or TimeoutException on the failure, its causes or its trail",
            Outcome.Name(failure));
        Check.True(!cancellationToken.IsCancellationRequested, "the caller's own token must not be signalled by an attempt deadline (XCUT-2)");
        await Check.ReleasedAsync(context, server, 0, probe: null, cancellationToken).ConfigureAwait(false);
    }

    // ASYNC-20: cancelling after the response was delivered leaves it open and readable.
    private static async Task LateCancelLeavesResponseOpenAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Large(LateCancelBytes, seed: 20));
        var transport = context.CreateTransport();
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        using var response = await transport.ExpectResponseAsync(Request.Get(server.Url("/late").AbsoluteUri), "a GET to cancel after delivery", cancelled.Token).ConfigureAwait(false);
        await cancelled.CancelAsync().ConfigureAwait(false);
        var body = await response.ReadBodyAsync("the body after the call's token was cancelled", cancellationToken).ConfigureAwait(false);

        Check.True(
            System.Security.Cryptography.SHA256.HashData(body).AsSpan().SequenceEqual(LargeBody.Sha256(LateCancelBytes, 20)),
            "the body read after a late cancel must be the whole body",
            LateCancelBytes + " bytes",
            body.Length + " bytes");
    }
}
