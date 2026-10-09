// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Request;
using static Dexpace.Sdk.Conformance.AssertionBuilder;

namespace Dexpace.Sdk.Conformance;

/// <summary>Ownership and abandonment: a borrowed client survives (<c>TRANSPORT-15</c>, <c>XCUT-22</c>), an owned one is released, and an abandoned body unblocks its producer (<c>TRANSPORT-19</c>).</summary>
internal static class LifecycleAssertions
{
    internal static IEnumerable<ConformanceAssertion> Assertions()
    {
        yield return Define("transport-15.borrowed-survives", ["TRANSPORT-15", "XCUT-22"], RequirementLevel.Must, AsyncOnly, BorrowedSurvivesAsync);
        yield return Define("transport-15.owned-released", ["TRANSPORT-15"], RequirementLevel.Must, AsyncOnly, OwnedReleasedAsync);
        yield return Define("transport-19.abandoned-body-unblocks", ["TRANSPORT-19"], RequirementLevel.Should, AsyncOnly, AbandonedBodyUnblocksAsync);
    }

    // TRANSPORT-15, XCUT-22: disposing a transport built over a borrowed native client leaves that client working.
    private static async Task BorrowedSurvivesAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Ok("native", keepAlive: true));
        var borrowed = context.RequireBorrowed();

        await Check.GuardAsync(
            () => Bounded.DisposeAsync(borrowed.Transport, context.ReleaseTimeout, "disposal of a transport over a borrowed client", cancellationToken),
            "disposing the transport over a borrowed client",
            cancellationToken).ConfigureAwait(false);
        await Check.GuardAsync(
            () => borrowed.SendThroughNativeClient(server.Url("/native"), cancellationToken),
            "a request through the borrowed native client after the transport was disposed (TRANSPORT-15: the SDK closes only what it created)",
            cancellationToken).ConfigureAwait(false);

        Check.Equal(server.Requests.Count, 1, "the requests the server received through the native client");
        Check.NoFaults(server);
    }

    // TRANSPORT-15: disposing an owned transport releases its connections, including idle pooled ones.
    private static async Task OwnedReleasedAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(LoopbackResponse.Ok("pooled", keepAlive: true));
        var transport = context.CreateTransport();

        using (var response = await transport.ExpectResponseAsync(Request.Get(server.Url("/owned").AbsoluteUri), "a GET that leaves an idle connection", cancellationToken).ConfigureAwait(false))
        {
            _ = await response.ReadBodyAsync("the body", cancellationToken).ConfigureAwait(false);
        }

        await Check.GuardAsync(
            () => Bounded.DisposeAsync(transport, context.ReleaseTimeout, "disposal of an owned transport", cancellationToken),
            "disposing an owned transport",
            cancellationToken).ConfigureAwait(false);

        await Check.ReleasedAsync(context, server, 0, probe: null, cancellationToken).ConfigureAwait(false);
    }

    // TRANSPORT-19: cancelling a call whose body source is stuck mid-read unblocks that source, and leaves no file handle behind.
    private static async Task AbandonedBodyUnblocksAsync(SuiteContext context, CancellationToken cancellationToken)
    {
        var server = context.StartServer(_ => LoopbackResponse.Hang());
        var transport = context.CreateTransport();

        using (var parked = new ParkedReadStream())
        using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            var call = transport.SendAsync(Request.Post(server.Url("/parked").AbsoluteUri, RequestBody.FromStream(parked)), cancelled.Token);
            await Bounded.WaitAsync(parked.ReadStarted, context.ReleaseTimeout, "the transport to start reading the body source", cancellationToken).ConfigureAwait(false);

            await cancelled.CancelAsync().ConfigureAwait(false);
            await Outcome.OfAsync(call, context.ReleaseTimeout, "the call with a parked body source to end after cancellation (TRANSPORT-19)", cancellationToken).ConfigureAwait(false);

            Check.True(parked.ObservedCancellation || parked.IsDisposed, "the parked body source must be unblocked by the cancellation: it observed its token or was disposed", "an unblocked source", "a source still parked");
        }

        await FileHandleReleasedAsync(context, transport, cancellationToken).ConfigureAwait(false);
    }

    private static async Task FileHandleReleasedAsync(SuiteContext context, IFaceTransport transport, CancellationToken cancellationToken)
    {
        // A server of its own: the parked upload above never completes, so the fixture never recorded it, and this request would
        // otherwise be the first the server recorded, not the second.
        var server = context.StartServer(_ => LoopbackResponse.Hang());
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, new byte[64 * 1024], cancellationToken).ConfigureAwait(false);
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var call = transport.SendAsync(Request.Post(server.Url("/file").AbsoluteUri, RequestBody.FromFile(path)), cancelled.Token);
            await Bounded.WaitAsync(server.WaitForRequestAsync(0, cancellationToken), context.ReleaseTimeout, "the server to receive the file upload", cancellationToken).ConfigureAwait(false);

            await cancelled.CancelAsync().ConfigureAwait(false);
            await Outcome.OfAsync(call, context.ReleaseTimeout, "the file upload to end after cancellation", cancellationToken).ConfigureAwait(false);

            Check.True(CanOpenExclusively(path), "no file handle may be left open after a cancelled upload of a file body (TRANSPORT-19)");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool CanOpenExclusively(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
