// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.ServerSentEvents;

public sealed partial class ServerSentEventStream
{
    /// <summary>Enumerates the events synchronously: the facade's one blocking view (SSE-26, SSE-27, P7b-15).</summary>
    /// <remarks>
    /// <para>
    /// This is a genuinely synchronous walk: the body opens through
    /// <see cref="Dexpace.Sdk.Core.Http.Response.ResponseBody.OpenRead"/> and each pull blocks in
    /// <see cref="Stream.Read(byte[], int, int)"/>. It is not sync-over-async. <b>Do not call
    /// <c>ToBlockingEnumerable</c> on the facade instead:</b> that adapter blocks a thread on the asynchronous view, which
    /// is exactly the sync-over-async this method exists to avoid.
    /// </para>
    /// <para>
    /// The sequence is lazy and single-use. Nothing is checked or opened until <c>GetEnumerator()</c>; that call takes the
    /// one view latch shared with <see cref="GetAsyncEnumerator"/> and the typed views, so a second view throws
    /// <see cref="InvalidOperationException"/> there, and a view requested after close throws
    /// <see cref="ObjectDisposedException"/> there. Release and failure rules are those of the asynchronous view, see the
    /// table on the type.
    /// </para>
    /// </remarks>
    /// <returns>A lazy, single-use sequence of the events.</returns>
    public IEnumerable<ServerSentEvent> AsEnumerable() =>
        new SingleUseSequence<ServerSentEvent>(() =>
        {
            ThrowIfViewUnavailable();
            return IterateRaw().GetEnumerator();
        });

    // The raw blocking iterator, WITHOUT the view latch (the typed adapter reuses it), with the same shape as
    // IterateRawAsync: open and read are the catch-bearing helpers, this body has only try/finally.
    private IEnumerable<ServerSentEvent> IterateRaw()
    {
        if (Volatile.Read(ref _closed) != 0)
        {
            yield break;
        }

        var reader = Open();
        try
        {
            while (Volatile.Read(ref _closed) == 0)
            {
                var next = ReadOne(reader);
                if (next is null)
                {
                    break;
                }

                yield return next;
            }
        }
        finally
        {
            Release(primary: null);
        }
    }

    // The blocking views have no caller token: a close is recognised by the flag alone, and an in-flight read is torn
    // down by the disposal itself (P7b-13).
    private ServerSentEventReader Open()
    {
        try
        {
            var stream = _response.Body.OpenRead(_closing.Token);
            RegisterOpened(stream);
            return new ServerSentEventReader(stream, _maxLineBytes);
        }
        catch (Exception ex) when (IsClosedInFlight(ex, default))
        {
            throw ClosedWhileReading(ex);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            Release(ex);
            throw;
        }
    }

    private ServerSentEvent? ReadOne(ServerSentEventReader reader)
    {
        try
        {
            return reader.ReadNext();
        }
        catch (Exception ex) when (IsClosedInFlight(ex, default))
        {
            throw ClosedWhileReading(ex);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            Release(ex);
            throw;
        }
    }
}
