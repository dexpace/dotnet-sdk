// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>What a call after dispose does, when the subject declares it (<c>SEAM-15</c>, a MAY).</summary>
internal static class DisposeAssertions
{
    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("seam-15.after-dispose", ["SEAM-15"], RequirementLevel.May, Both, AfterDisposeAsync);
    }

    // SEAM-15: with ThrowsObjectDisposedException declared, a call after dispose throws it, directly or through the task: on the
    // owned transport and, where the subject can borrow a native client, on the borrowed one too.
    private static async Task AfterDisposeAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        if (context.Subject.AfterDispose == AfterDisposeBehavior.Unspecified)
        {
            throw new ConformanceVacuousException("the subject declares no post-dispose behaviour (SEAM-15 is a MAY)");
        }

        var transport = context.CreateTransport();
        await AssertThrowsAfterDisposeAsync(context, transport, "the owned transport", cancellationToken).ConfigureAwait(false);

        if (context.Face == TransportFace.Async && context.Subject.CreateBorrowed is not null)
        {
            var borrowed = context.RequireBorrowed();
            await AssertThrowsAfterDisposeAsync(context, context.Adopt(borrowed.Transport), "the transport over a borrowed client", cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AssertThrowsAfterDisposeAsync(SuiteContext context, IFaceTransport transport, string what, CancellationToken cancellationToken)
    {
        await Check.GuardAsync(() => Bounded.DisposeAsync(transport, context.ReleaseTimeout, $"disposal of {what}", cancellationToken), $"disposing {what}", cancellationToken).ConfigureAwait(false);

        var failure = await Outcome.OfAsync(transport.SendAsync(Request.Get("http://127.0.0.1:1/"), cancellationToken), context.ReleaseTimeout, $"a call on {what} after it was disposed to end", cancellationToken).ConfigureAwait(false);

        Check.True(
            Outcome.Find<ObjectDisposedException>(failure) is not null,
            $"a call on {what} after dispose must throw ObjectDisposedException, as the subject declares (SEAM-15)",
            "ObjectDisposedException",
            Outcome.Name(failure));
    }
}
