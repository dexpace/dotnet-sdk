// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>Close: idempotent, non-blocking and interrupt-safe (<c>TRANSPORT-16</c>, <c>ASYNC-15</c>).</summary>
internal static class CloseAssertions
{
    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define(
            "transport-16.close-idempotent-nonblocking",
            ["TRANSPORT-16", "ASYNC-15"],
            RequirementLevel.Must,
            Both,
            CloseIdempotentNonBlockingAsync);
    }

    // TRANSPORT-16: three disposals of an idle transport throw nothing; a disposal with a call parked on the server returns
    // within the release bound instead of waiting for the call; and the caller's cancellation flag survives it all.
    private static async Task CloseIdempotentNonBlockingAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var idle = context.CreateTransport();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await Check.GuardAsync(
                () => Bounded.DisposeAsync(idle, context.ReleaseTimeout, $"disposal number {attempt} of an idle transport", cancellationToken),
                $"disposal number {attempt} of an idle transport (TRANSPORT-16: close is idempotent)",
                cancellationToken).ConfigureAwait(false);
        }

        var server = context.StartServer(LoopbackResponse.Hang());
        var busy = context.CreateTransport();
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var call = busy.SendAsync(Request.Get(server.Url("/hang").AbsoluteUri), cancelled.Token);
        await Bounded.WaitAsync(server.WaitForRequestAsync(0, cancellationToken), context.ReleaseTimeout, "the server to receive the request", cancellationToken).ConfigureAwait(false);

        await Check.GuardAsync(
            () => Bounded.DisposeAsync(busy, context.ReleaseTimeout, "disposal of a transport with a call in flight (TRANSPORT-16: close does not wait)", cancellationToken),
            "disposal of a transport with a call in flight",
            cancellationToken).ConfigureAwait(false);
        await cancelled.CancelAsync().ConfigureAwait(false);
        await Outcome.OfAsync(call, context.ReleaseTimeout, "the call that was in flight when the transport was disposed to end once its token was signalled", cancellationToken).ConfigureAwait(false);

        Check.True(cancelled.Token.IsCancellationRequested, "the cancellation flag must still be set after close and cancellation (TRANSPORT-16)");
    }
}
