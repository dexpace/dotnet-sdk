// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Globalization;
using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Redirects and deadlines: the transport never follows a redirect itself (<c>TRANSPORT-1</c>); a per-call timeout is a
/// retryable timeout and is not a cancellation (<c>TRANSPORT-4</c>, <c>XCUT-2</c>), applies to its own call only
/// (<c>TRANSPORT-5</c>), is clamped and never truncated away (<c>TRANSPORT-6</c>); a cancel from inside the native client is
/// terminal (<c>TRANSPORT-8</c>); and no response is orphaned in a cancel race (<c>TRANSPORT-9</c>, <c>SEAM-30</c>). The rows
/// belong to phase 8b; the assertions are 8a's.
/// </summary>
internal static class RedirectAndTimeoutAssertions
{
    private static readonly TimeSpan s_deadline = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan s_laterThanDeadline = TimeSpan.FromMilliseconds(600);
    private static readonly int[] s_raceOffsetsMilliseconds = [0, 1, 2, 4, 8];

    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-1.redirect-not-followed", ["TRANSPORT-1"], RequirementLevel.Must, Both, RedirectNotFollowedAsync);
        yield return Define("transport-4.timeout-is-retryable", ["TRANSPORT-4", "XCUT-2"], RequirementLevel.Must, Both, TimeoutIsRetryableAsync);
        yield return Define("transport-5.per-call-timeout", ["TRANSPORT-5"], RequirementLevel.Must, AsyncOnly, PerCallTimeoutAsync);
        yield return Define("transport-6.sub-resolution-timeout", ["TRANSPORT-6"], RequirementLevel.Should, Both, SubResolutionTimeoutAsync);
        yield return Define("transport-8.internal-cancel-is-terminal", ["TRANSPORT-8"], RequirementLevel.Must, AsyncOnly, InternalCancelIsTerminalAsync);
        yield return Define("transport-9.settle-race-releases", ["TRANSPORT-9", "SEAM-30"], RequirementLevel.Must, AsyncOnly, SettleRaceReleasesAsync);
    }

    // TRANSPORT-1: a 3xx is returned as it is; the transport never contacts the Location.
    private static async Task RedirectNotFollowedAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var target = context.StartServer(LoopbackResponse.Ok("target"));
        var origin = context.StartServer(LoopbackResponse.Redirect(302, target.Url("/target").AbsoluteUri));
        var transport = context.CreateTransport();

        using var response = await transport.ExpectResponseAsync(Request.Get(origin.Url("/start").AbsoluteUri), "a GET answered with a 302", cancellationToken).ConfigureAwait(false);

        Check.Equal(response.Status.Code, 302, "the status the transport delivered (it must not follow the redirect)");
        Check.Equal(response.Headers.Get("Location"), target.Url("/target").AbsoluteUri, "the Location header of the delivered 3xx");
        Check.Equal(target.Requests.Count, 0, "the requests the redirect target received");
        Check.NoFaults(origin);
        Check.NoFaults(target);
    }

    // Sends a call that a server never answers, with a per-call timeout, and requires the retryable timeout the clause names.
    private static async Task ExpectTimeoutAsync(SuiteContext context, IFaceTransport transport, Uri url, TimeSpan timeout, string what, CancellationToken cancellationToken)
    {
        var options = new RequestOptions { Timeout = timeout };
        var failure = await Outcome.OfAsync(transport.SendAsync(Request.Get(url.AbsoluteUri), options, cancellationToken), context.TimeoutBound, $"{what} to be cut by its {Bounded.Format(timeout)} timeout", cancellationToken).ConfigureAwait(false);

        Check.True(failure is ServiceRequestTimeoutException, $"{what} must fail with a ServiceRequestTimeoutException when its per-call timeout elapses, never a cancellation and never a hang", "ServiceRequestTimeoutException", Outcome.Name(failure));
        Check.True(failure is SdkException { IsRetryable: true }, $"{what}: a timeout is retryable (XCUT-2)", "IsRetryable == true", "false");
        Check.True(failure is not OperationCanceledException && !cancellationToken.IsCancellationRequested, $"{what}: the caller's token must not be signalled by a timeout");
    }

    // TRANSPORT-4, XCUT-2.
    private static async Task TimeoutIsRetryableAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Hang());
        var transport = context.CreateTransport();

        await ExpectTimeoutAsync(context, transport, server.Url("/hang"), s_deadline, "a call to a server that never answers", cancellationToken).ConfigureAwait(false);
    }

    // TRANSPORT-5: the bound belongs to its call: a later call neither inherits an earlier call's timeout nor loses its own.
    private static async Task PerCallTimeoutAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateTransport();
        var hanging = context.StartServer(LoopbackResponse.Hang());
        await ExpectTimeoutAsync(context, transport, hanging.Url("/first"), s_deadline, "the first call (200 ms timeout)", cancellationToken).ConfigureAwait(false);

        foreach (var (name, timeout) in new (string, TimeSpan?)[] { ("the second call (5 s timeout)", TimeSpan.FromSeconds(5)), ("the third call (no timeout)", null) })
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var server = context.StartServer(LoopbackResponse.Gated(gate.Task, LoopbackResponse.Ok("answered")));
            var call = transport.SendAsync(Request.Get(server.Url("/later").AbsoluteUri), new RequestOptions { Timeout = timeout }, cancellationToken);
            await Bounded.WaitAsync(server.WaitForRequestAsync(0, cancellationToken), context.ReleaseTimeout, "the server to receive the request", cancellationToken).ConfigureAwait(false);

            // Longer than the first call's 200 ms: a bound that leaked from the first call would have cut this one by now.
            await TimeProvider.System.DelayAsync(s_laterThanDeadline, cancellationToken).ConfigureAwait(false);
            gate.SetResult();
            var failure = await Outcome.OfAsync(call, context.TimeoutBound, $"{name} to be answered", cancellationToken).ConfigureAwait(false);

            Check.True(failure is null, $"{name} must be answered, not cut by the first call's 200 ms timeout (TRANSPORT-5: a per-call timeout applies to one call only)", "a response", Outcome.Name(failure));
        }
    }

    // TRANSPORT-6: a positive timeout below the timer's resolution is raised to it, never truncated to "no timeout".
    private static async Task SubResolutionTimeoutAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Hang());
        var transport = context.CreateTransport();

        await ExpectTimeoutAsync(context, transport, server.Url("/hang"), TimeSpan.FromTicks(1000), "a call with a 100 microsecond timeout", cancellationToken).ConfigureAwait(false);
    }

    // TRANSPORT-8: a cancel from the native side with the caller's token unsignalled is terminal, and a real timeout still is not.
    private static async Task InternalCancelIsTerminalAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Hang());
        var internalCancel = context.RequireInternalCancel();
        var transport = context.Adopt(internalCancel.Transport);
        var call = transport.SendAsync(Request.Get(server.Url("/hang").AbsoluteUri), cancellationToken);
        await Bounded.WaitAsync(server.WaitForRequestAsync(0, cancellationToken), context.ReleaseTimeout, "the server to receive the request", cancellationToken).ConfigureAwait(false);

        internalCancel.CancelInFlight();
        var failure = await Outcome.OfAsync(call, context.TimeoutBound, "the call to end after the native client cancelled it", cancellationToken).ConfigureAwait(false);

        Check.True(failure is not null, "a call cancelled from the native side must fail", "a failed call", "a completed call");
        Check.True(failure is not ServiceRequestTimeoutException && failure is not SdkException { IsRetryable: true }, "an internal cancellation must not be reported as a retryable timeout (TRANSPORT-8)", "a terminal cancellation or a non-retryable failure", Outcome.Name(failure));
        Check.True(!cancellationToken.IsCancellationRequested, "the caller's token must not have been signalled by the native cancellation");

        var second = context.RequireInternalCancel();
        var timeouts = context.StartServer(LoopbackResponse.Hang());
        await ExpectTimeoutAsync(context, context.Adopt(second.Transport), timeouts.Url("/hang"), s_deadline, "a real timeout on the same kind of transport", cancellationToken).ConfigureAwait(false);
    }

    // TRANSPORT-9, SEAM-30: whichever of "response" and "cancel" wins, no response is left holding a connection.
    private static async Task SettleRaceReleasesAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var transport = context.CreateTransport();
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var offset = s_raceOffsetsMilliseconds[iteration % s_raceOffsetsMilliseconds.Length];
            var server = context.StartServer(request => request.Target == Check.ProbePath
                ? LoopbackResponse.Ok("probe", keepAlive: true)
                : LoopbackResponse.Streamed([new("Content-Type", "application/octet-stream")], OneChunk(), keepAlive: true));
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var call = transport.SendAsync(Request.Get(server.Url("/race").AbsoluteUri), cancelled.Token);
            await Bounded.WaitAsync(server.WaitForRequestAsync(0, cancellationToken), context.ReleaseTimeout, $"the server to receive request {iteration.ToString(CultureInfo.InvariantCulture)}", cancellationToken).ConfigureAwait(false);
            if (offset > 0)
            {
                await TimeProvider.System.DelayAsync(TimeSpan.FromMilliseconds(offset), cancellationToken).ConfigureAwait(false);
            }

            await cancelled.CancelAsync().ConfigureAwait(false);
            var failure = await Outcome.OfAsync(call, context.ReleaseTimeout, $"call {iteration.ToString(CultureInfo.InvariantCulture)} to settle after its token was signalled", cancellationToken).ConfigureAwait(false);

            Check.True(failure is null || failure is OperationCanceledException, $"call {iteration.ToString(CultureInfo.InvariantCulture)} must end delivered or cancelled", "a response or an OperationCanceledException", Outcome.Name(failure));
            await Check.ReleasedAsync(context, server, 0, transport, cancellationToken).ConfigureAwait(false);
            await server.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<byte[]> OneChunk()
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield return "data"u8.ToArray();
    }
}
