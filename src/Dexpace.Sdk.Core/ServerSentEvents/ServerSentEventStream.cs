// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.ServerSentEvents;

/// <summary>
/// Owns one <see cref="Response"/> and streams its server-sent events, releasing the response exactly once on every
/// path (SSE-23 to SSE-32).
/// </summary>
/// <remarks>
/// <para>
/// <b>Shape.</b> Create it with <see cref="FromResponse"/> and enumerate it once, with <c>await foreach</c>
/// (<see cref="GetAsyncEnumerator"/>), with a blocking <c>foreach</c> (<see cref="AsEnumerable"/>), or through the typed
/// adapter (<see cref="MapAsync{T}"/>, <see cref="Map{T}"/>). The four views share one single-use latch: asking for a
/// second one throws <see cref="InvalidOperationException"/> at the call, and asking for one after the stream is closed
/// throws <see cref="ObjectDisposedException"/> (SSE-26, SSE-27). The facade deliberately implements
/// <see cref="IAsyncEnumerable{T}"/> and not <see cref="IEnumerable{T}"/> (P7b-15): a type with both makes every LINQ call
/// on it ambiguous once <c>System.Linq</c> is imported (it carries <c>System.Linq.AsyncEnumerable</c> in .NET 10), and
/// would let <c>foreach</c> versus <c>await foreach</c> silently choose the path.
/// </para>
/// <para>
/// <b>Release (SSE-23, SSE-28).</b> The first of any of these releases the response and every later one is a no-op:
/// the stream ending, the enumerator being disposed early, a failure, the typed adapter's <c>Done</c>, or an explicit
/// <see cref="Dispose"/> or <see cref="DisposeAsync"/>. The opened body stream is disposed first and the response second
/// (P7b-11). What a release failure does depends on the path (SSE-29, SSE-30, P7b-12):
/// </para>
/// <list type="table">
/// <listheader><term>Path</term><description>A failure while releasing</description></listheader>
/// <item><term>The stream ends (SSE-24); the typed adapter's <c>Done</c> (SSE-34)</term><description>Reported out of band and swallowed.</description></item>
/// <item><term>The enumerator is disposed early: <c>break</c>, an exception in the loop body, <c>FirstAsync</c> (SSE-25)</term><description>Reported out of band and swallowed, because a throw there would replace the consumer's own exception.</description></item>
/// <item><term>A read, a cap or a cancellation fails mid-stream (SSE-29); the mapper throws (SSE-36)</term><description>Attached to that exception through <see cref="ExceptionTrail"/>, which is then rethrown unchanged.</description></item>
/// <item><term>Explicit <see cref="Dispose"/> or <see cref="DisposeAsync"/> (SSE-30)</term><description>Propagates; the stream's failure is primary and the response's is attached to it.</description></item>
/// <item><term>Any call after the first release (SSE-28)</term><description>Nothing happens.</description></item>
/// </list>
/// <para>
/// "Reported out of band" is the SDK's single channel for a failure it must not throw: an <c>exception</c> event on
/// <see cref="System.Diagnostics.Activity.Current"/> tagged <c>dexpace.dispose.suppressed</c> and, when
/// <see cref="FromResponse"/> was given a logger, the <c>dexpace.dispose.suppressed</c> warning (event id 130). With no
/// logger and no activity listener the failure is reported to nobody (residual R3).
/// </para>
/// <para>
/// <b>Close from another thread (SSE-31).</b> <see cref="Dispose"/> and <see cref="DisposeAsync"/> are safe to call from
/// a thread other than the one iterating, to cancel a long-lived stream. A close observed between pulls ends the iteration
/// cleanly; a close that tears the stream down while a read is in flight surfaces from that read as an
/// <see cref="IOException"/> whose inner exception is the failure the teardown caused, and the response is still released
/// once. The closing call owns that release: it takes it before it cancels, so the read it tears down can never release
/// the response ahead of it, a release failure propagates from the closing call (SSE-30) and the response is released by
/// the time that call returns (unless the stream had already ended and released itself, SSE-28). The facade cancels an
/// internal token the reads observe, so a cooperative stream unblocks at once and a transport's stream unblocks when its
/// disposal tears it down (residual R2: a stream that honours neither stays blocked until data or end of stream
/// arrives). A cancellation by the caller's own token is never rewritten (XCUT-1). Beyond that one call the type is
/// single-threaded, like the reader (SSE-18): two threads pulling one stream is undefined.
/// </para>
/// <para>
/// <b>Bounding an untrusted stream.</b> The line cap bounds a line, not an event (residual R1, P7b-5): bound a stream from
/// an untrusted server with the enumeration token or <c>OverallTimeout</c>. <c>AttemptTimeout</c> covers only the time to
/// the response headers, so a long-lived stream is not cut by it.
/// </para>
/// </remarks>
[SuppressMessage("Naming", "CA1711", Justification = "Specification type name (design E): the facade is a stream of events and deliberately does not derive from System.IO.Stream.")]
public sealed partial class ServerSentEventStream : IAsyncEnumerable<ServerSentEvent>, IAsyncDisposable, IDisposable
{
    private readonly Response _response;
    private readonly int _maxLineBytes;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _closing = new();
    private Stream? _opened;
    private int _closed;
    private int _released;
    private int _viewTaken;

    private ServerSentEventStream(Response response, int maxLineBytes, ILogger? logger)
    {
        _response = response;
        _maxLineBytes = maxLineBytes;
        _logger = logger;
    }

    /// <summary>What a call to <see cref="Dispose"/> or <see cref="DisposeAsync"/> found, decided once by <see cref="BeginClose"/>.</summary>
    private enum CloseRole
    {
        /// <summary>Another close began first: nothing to do (SSE-28).</summary>
        Later = 0,

        /// <summary>The first close, but an automatic release already took the response: nothing to release.</summary>
        Closer = 1,

        /// <summary>The first close, and it took the response's release before cancelling: it releases and propagates (SSE-30).</summary>
        Releaser = 2,
    }

    /// <summary>
    /// Binds a stream to <paramref name="response"/>, which it owns from this call on, even when this call throws (SSE-32,
    /// P7b-9).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ownership transfers on the call, success or failure.</b> The common shape is
    /// <c>await using var events = ServerSentEventStream.FromResponse(await pipeline.SendAsync(request, ct));</c>; if a
    /// failure here left the response with the caller, that line would leak it. So when the response is rejected (or
    /// <paramref name="maxLineBytes"/> is invalid) the response is disposed before the exception is thrown, and a failure
    /// of that dispose is attached to the exception rather than replacing it.
    /// </para>
    /// <para>
    /// A response with no body to stream is rejected loudly: status 204, 205 or 304; a <c>HEAD</c> request; a
    /// <c>Content-Length</c> of zero; or an absent body. The message names the reason and never the URL. There is no
    /// status or <c>Content-Type</c> check: the default pipeline's error mapping already throws on a 4xx or 5xx, servers
    /// mislabel event streams, and conventions stay out of core (SSE-37, P7b-18). Set <c>Accept: text/event-stream</c> on
    /// the request yourself.
    /// </para>
    /// <para>
    /// Nothing is read here: the body stream is opened lazily at the first pull (P7b-10), so a stream that is never
    /// enumerated only releases its response.
    /// </para>
    /// </remarks>
    /// <param name="response">The response whose body is an event stream; the stream takes ownership of it.</param>
    /// <param name="maxLineBytes">The most content bytes one line may hold; positive (SSE-19).</param>
    /// <param name="logger">
    /// An optional logger for the <c>dexpace.dispose.suppressed</c> warning (event id 130) when a release failure is
    /// swallowed on a clean terminal path (SSE-30, P7b-19); a <see cref="Response"/> carries none.
    /// </param>
    /// <returns>The stream, owning <paramref name="response"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>; nothing is disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLineBytes"/> is not positive; the response is disposed.</exception>
    /// <exception cref="ArgumentException">The response has no body to stream; the response is disposed.</exception>
    public static ServerSentEventStream FromResponse(
        Response response,
        int maxLineBytes = ServerSentEventReader.DefaultMaxLineBytes,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        var rejection = Reject(response, maxLineBytes);
        if (rejection is not null)
        {
            Disposal.DisposeQuietly(response, rejection, logger);
            throw rejection;
        }

        return new ServerSentEventStream(response, maxLineBytes, logger);
    }

    /// <summary>
    /// Releases the stream: cancels reads in flight, then disposes the opened body stream and the response, once
    /// (SSE-25, SSE-28, SSE-30, SSE-31).
    /// </summary>
    /// <remarks>
    /// Safe from any thread, and idempotent: only the first call (or the first automatic release) touches the response. A
    /// failure while releasing <b>propagates</b> from the call that released: the body stream's failure is primary and the
    /// response's is attached to it. Prefer <see cref="DisposeAsync"/> in asynchronous code.
    /// </remarks>
    public void Dispose()
    {
        var role = BeginClose();
        try
        {
            if (role == CloseRole.Releaser)
            {
                ReleasePropagating(Volatile.Read(ref _opened));
            }
        }
        finally
        {
            EndClose(role);
        }
    }

    /// <summary>The asynchronous twin of <see cref="Dispose"/>, with the same once-only and propagate rules.</summary>
    /// <returns>A task that completes when the release has finished.</returns>
    public async ValueTask DisposeAsync()
    {
        var role = BeginClose();
        try
        {
            if (role == CloseRole.Releaser)
            {
                await ReleasePropagatingAsync(Volatile.Read(ref _opened)).ConfigureAwait(false);
            }
        }
        finally
        {
            EndClose(role);
        }
    }

    private static Exception? Reject(Response response, int maxLineBytes)
    {
        if (maxLineBytes <= 0)
        {
            return new ArgumentOutOfRangeException(nameof(maxLineBytes), maxLineBytes, "The line cap must be positive.");
        }

        var reason = BodylessReason(response);
        return reason is null ? null : new ArgumentException(reason, nameof(response));
    }

    private static string? BodylessReason(Response response)
    {
        if (response.Status.Code is 204 or 205 or 304)
        {
            return "The response status (204, 205 or 304) carries no body, so there is no event stream to read.";
        }

        if (response.Request.Method == Method.Head)
        {
            return "The response answers a HEAD request, which has no body, so there is no event stream to read.";
        }

        if (response.Body.ContentLength == 0)
        {
            return "The response body is empty (Content-Length is 0), so there is no event stream to read.";
        }

        return response.Body.IsEmptyReplayable
            ? "The response has no body, so there is no event stream to read."
            : null;
    }

    // Marks the stream closed before anything else (P7b-13), so a read torn down by the release below is recognised as a
    // close and not as a stream failure. Only the first closer cancels the internal token and, later, disposes it.
    //
    // The first closer also takes the release latch HERE, before it cancels. Cancelling unblocks a parked read, which fails
    // and unwinds into the iterator's own finally, and for a stream whose cancelled read completes synchronously that
    // finally runs inline inside Cancel(), ahead of the closer's release. If the latch were still free it would go to that
    // quiet release, which swallows a failure, so an explicit close would neither propagate it (SSE-30, P7b-12) nor, when
    // the iterator's release is asynchronous, have released the response by the time it returned. Holding the latch first
    // makes the iterator's release the latched no-op P7b-13 describes. RegisterOpened closes the other side: a stream
    // that opens after this point sees the latch taken and disposes itself.
    private CloseRole BeginClose()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return CloseRole.Later;
        }

        var role = Interlocked.Exchange(ref _released, 1) == 0 ? CloseRole.Releaser : CloseRole.Closer;
        try
        {
            _closing.Cancel();
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // A callback registered on the token threw. The release below still tears the read down, which is what the
            // cancellation was for, and a throw here would skip it.
        }

        return role;
    }

    private void EndClose(CloseRole role)
    {
        if (role != CloseRole.Later)
        {
            _closing.Dispose();
        }
    }

    /// <summary>
    /// Takes the one view latch: closed first (SSE-27, <see cref="ObjectDisposedException"/>), then the single-use check
    /// (SSE-26, <see cref="InvalidOperationException"/>). Synchronous, so the throw is at the call (P7b-14).
    /// </summary>
    internal void ThrowIfViewUnavailable()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
        if (Interlocked.Exchange(ref _viewTaken, 1) != 0)
        {
            throw new InvalidOperationException(
                "This event stream can be enumerated once, through one of its four views; a second walk would resume mid-stream.");
        }
    }

    // The iterator's automatic release (P7b-12). With no primary it is a clean terminal path (SSE-24, SSE-25, SSE-34): a
    // failure is reported out of band and swallowed. With a primary it is a failure path (SSE-29, SSE-36): a failure is
    // attached to that exception. An explicit close is neither: it takes the latch in BeginClose and releases through
    // ReleasePropagating.
    private void Release(Exception? primary)
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        var opened = Volatile.Read(ref _opened);
        Disposal.DisposeQuietly(opened, primary, _logger);
        Disposal.DisposeQuietly(_response, primary, _logger);
    }

    private async ValueTask ReleaseAsync(Exception? primary)
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return;
        }

        var opened = Volatile.Read(ref _opened);
        await Disposal.DisposeQuietlyAsync(opened, primary, _logger).ConfigureAwait(false);
        await Disposal.DisposeQuietlyAsync(_response, primary, _logger).ConfigureAwait(false);
    }

    // Both disposals always run. The stream's failure is primary and the response's is attached to it; the first failure
    // is thrown with its stack intact (P7b-11, P7b-12).
    private void ReleasePropagating(Stream? opened)
    {
        Exception? failure = null;
        try
        {
            opened?.Dispose();
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            failure = ex;
        }

        try
        {
            _response.Dispose();
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            Chain(ref failure, ex);
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Throw(failure);
        }
    }

    private async ValueTask ReleasePropagatingAsync(Stream? opened)
    {
        Exception? failure = null;
        try
        {
            if (opened is not null)
            {
                await opened.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            failure = ex;
        }

        try
        {
            await _response.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            Chain(ref failure, ex);
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Throw(failure);
        }
    }

    private static void Chain(ref Exception? primary, Exception secondary)
    {
        if (primary is null)
        {
            primary = secondary;
        }
        else
        {
            ExceptionTrail.AddSuppressed(primary, secondary);
        }
    }

    /// <summary>
    /// Records the stream a view opened so a release can dispose it. A close that raced the open has already run its
    /// release without seeing it, so the late stream is disposed here and the open is reported as closed.
    /// </summary>
    private void RegisterOpened(Stream stream)
    {
        Interlocked.Exchange(ref _opened, stream);
        if (Volatile.Read(ref _released) != 0)
        {
            Disposal.DisposeQuietly(stream, null, _logger);
            throw new ObjectDisposedException(nameof(ServerSentEventStream));
        }
    }

    private bool IsClosedInFlight(Exception ex, CancellationToken callerToken) =>
        Volatile.Read(ref _closed) != 0
        && !ExceptionFacts.IsFatal(ex)
        && !(ex is OperationCanceledException && callerToken.IsCancellationRequested);

    private static IOException ClosedWhileReading(Exception inner) =>
        new("The event stream was closed while a read was in flight.", inner);
}
