// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.ServerSentEvents;

public sealed partial class ServerSentEventStream
{
    /// <summary>Enumerates the events asynchronously: the facade's one asynchronous view (SSE-24 to SSE-27).</summary>
    /// <remarks>
    /// <para>
    /// Call it once, normally through <c>await foreach</c>. The check that this is the only view, and that the stream is
    /// not closed, runs synchronously, so a second view or a view after close throws at this call and not at the first
    /// <c>MoveNextAsync</c> (SSE-26, SSE-27, P7b-14). The body stream opens lazily at the first pull through
    /// <see cref="Dexpace.Sdk.Core.Http.Response.ResponseBody.OpenReadAsync"/> (P7b-10).
    /// </para>
    /// <para>
    /// The enumerator releases the stream when the events end (SSE-24) and when it is disposed early (SSE-25). Neither can
    /// know whether the consumer has an exception in flight, so a release failure there is reported out of band and
    /// swallowed; a failure while reading releases first and rethrows the same exception, with any release failure
    /// attached (SSE-29). See the table on the type for every path.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">A token that cancels the pending read; it is never rewritten to an I/O error.</param>
    /// <returns>An enumerator over the events.</returns>
    /// <exception cref="ObjectDisposedException">The stream is closed.</exception>
    /// <exception cref="InvalidOperationException">A view of this stream was already taken.</exception>
    public IAsyncEnumerator<ServerSentEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        ThrowIfViewUnavailable();
        return IterateRawAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

    // The raw iterator, WITHOUT the view latch: the typed adapter reuses it. Both failure-bearing steps are helpers with
    // the catch clauses, because a yield return cannot sit inside a try that has a catch (CS1626); this body has only
    // try/finally, and its finally never throws (design fact 5).
    private async IAsyncEnumerable<ServerSentEvent> IterateRawAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            yield break;
        }

        var (linked, reader) = await OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (Volatile.Read(ref _closed) == 0)
            {
                var next = await ReadOneAsync(reader, linked.Token, cancellationToken).ConfigureAwait(false);
                if (next is null)
                {
                    break;
                }

                yield return next;
            }
        }
        finally
        {
            linked.Dispose();
            await ReleaseAsync(ReleaseKind.Quiet, null).ConfigureAwait(false);
        }
    }

    // Opens the body and builds the reader. A failure here is a failure path: the release attaches to it (P7b-12) and a
    // close that raced the open is an IOException (P7b-13), the same two clauses as ReadOneAsync.
    private async ValueTask<(CancellationTokenSource Linked, ServerSentEventReader Reader)> OpenAsync(CancellationToken callerToken)
    {
        CancellationTokenSource? linked = null;
        try
        {
            linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, _closing.Token);
            var stream = await _response.Body.OpenReadAsync(linked.Token).ConfigureAwait(false);
            RegisterOpened(stream);
            return (linked, new ServerSentEventReader(stream, _maxLineBytes));
        }
        catch (Exception ex) when (IsClosedInFlight(ex, callerToken))
        {
            linked?.Dispose();
            throw ClosedWhileReading(ex);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            linked?.Dispose();
            await ReleaseAsync(ReleaseKind.Attach, ex).ConfigureAwait(false);
            throw;
        }
    }

    // One pull. The release on a failure runs before the exception reaches the caller (SSE-29), the exception is the same
    // object (throw;), and a fatal exception is neither wrapped nor swallowed.
    private async ValueTask<ServerSentEvent?> ReadOneAsync(
        ServerSentEventReader reader,
        CancellationToken linkedToken,
        CancellationToken callerToken)
    {
        try
        {
            return await reader.ReadNextAsync(linkedToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsClosedInFlight(ex, callerToken))
        {
            throw ClosedWhileReading(ex);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            await ReleaseAsync(ReleaseKind.Attach, ex).ConfigureAwait(false);
            throw;
        }
    }
}
