// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Core.ServerSentEvents;

public sealed partial class ServerSentEventStream
{
    /// <summary>Maps each event to a model, asynchronously: the facade's typed asynchronous view (SSE-33 to SSE-36).</summary>
    /// <remarks>
    /// <para>
    /// The mapper receives the event's name (<see langword="null"/> when absent) and its <c>data</c> lines joined with
    /// <c>"\n"</c> (the empty string when there are none), and returns an <see cref="SseMapResult{T}"/>:
    /// <see cref="SseMapResult.Value{T}(T)"/> yields a model, <see cref="SseMapResult.Skip"/> advances silently, and
    /// <see cref="SseMapResult.Done"/> releases the stream and completes without a model, so the sentinel itself is never
    /// a model and nothing after it is parsed or mapped. The sentinel is the <i>caller's</i>; core names none (SSE-37):
    /// </para>
    /// <code>
    /// await foreach (var chunk in events.MapAsync&lt;Chunk&gt;((name, data) =&gt;
    ///     data == "[DONE]" ? SseMapResult.Done : SseMapResult.Value(Parse(data))))
    /// {
    ///     Handle(chunk);
    /// }
    /// </code>
    /// <para>
    /// The adapter is lazy (SSE-35): nothing is read, opened or mapped until the first <c>MoveNextAsync</c>, and the
    /// mapper runs once per raw event pulled, only inside a pull. The mapper is a synchronous function, because the data
    /// is already in memory and an asynchronous mapper would invite I/O inside the pull. It shares the one view latch with
    /// the other three views (SSE-26). A mapper that throws releases the stream first and then propagates unchanged, with
    /// any release failure attached (SSE-36); a mapper that returns <c>default</c> is such a failure
    /// (<see cref="InvalidOperationException"/>), never a silent skip. The release rules of the other paths are the table
    /// on the type.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="mapper">Decides what each event is; called with the event name and the joined data.</param>
    /// <returns>A lazy, single-use sequence of the models.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The stream is closed (thrown by <c>GetAsyncEnumerator</c>).</exception>
    /// <exception cref="InvalidOperationException">A view of this stream was already taken (thrown by <c>GetAsyncEnumerator</c>).</exception>
    public IAsyncEnumerable<T> MapAsync<T>(Func<string?, string, SseMapResult<T>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return new SingleUseAsyncSequence<T>(token =>
        {
            ThrowIfViewUnavailable();
            return MapCoreAsync(mapper, token).GetAsyncEnumerator(token);
        });
    }

    /// <summary>Maps each event to a model, synchronously: the facade's typed blocking view (SSE-33 to SSE-36).</summary>
    /// <remarks>
    /// The blocking twin of <see cref="MapAsync{T}"/>, with the same mapper contract, laziness, single-use latch and
    /// release rules; the body opens through <see cref="Dexpace.Sdk.Core.Http.Response.ResponseBody.OpenRead"/> and each
    /// pull blocks in <see cref="Stream.Read(byte[], int, int)"/>.
    /// </remarks>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="mapper">Decides what each event is; called with the event name and the joined data.</param>
    /// <returns>A lazy, single-use sequence of the models.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mapper"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The stream is closed (thrown by <c>GetEnumerator</c>).</exception>
    /// <exception cref="InvalidOperationException">A view of this stream was already taken (thrown by <c>GetEnumerator</c>).</exception>
    public IEnumerable<T> Map<T>(Func<string?, string, SseMapResult<T>> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        return new SingleUseSequence<T>(() =>
        {
            ThrowIfViewUnavailable();
            return MapCore(mapper).GetEnumerator();
        });
    }

    // Walks the latch-free raw iterator. The mapper call sits in a helper-free try/catch with no yield inside it, so the
    // yield return is outside any catch-bearing try (CS1626).
    private async IAsyncEnumerable<T> MapCoreAsync<T>(
        Func<string?, string, SseMapResult<T>> mapper,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var raw in IterateRawAsync(cancellationToken).ConfigureAwait(false))
        {
            SseMapResult<T> result;
            try
            {
                result = Apply(mapper, raw);
            }
            catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
            {
                await ReleaseAsync(ex).ConfigureAwait(false);
                throw;
            }

            if (result.Kind == SseMapResultKind.Done)
            {
                await ReleaseAsync(primary: null).ConfigureAwait(false);
                yield break;
            }

            if (result.Kind == SseMapResultKind.Value)
            {
                yield return result.Value;
            }
        }
    }

    private IEnumerable<T> MapCore<T>(Func<string?, string, SseMapResult<T>> mapper)
    {
        foreach (var raw in IterateRaw())
        {
            SseMapResult<T> result;
            try
            {
                result = Apply(mapper, raw);
            }
            catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
            {
                Release(ex);
                throw;
            }

            if (result.Kind == SseMapResultKind.Done)
            {
                Release(primary: null);
                yield break;
            }

            if (result.Kind == SseMapResultKind.Value)
            {
                yield return result.Value;
            }
        }
    }

    // Joins the data with LF (SSE-33) and refuses a default verdict (P7b-16): Kind 0 is a mapper that forgot to return.
    private static SseMapResult<T> Apply<T>(Func<string?, string, SseMapResult<T>> mapper, ServerSentEvent raw)
    {
        var result = mapper(raw.Event, string.Join('\n', raw.Data));
        return result.Kind is SseMapResultKind.Value or SseMapResultKind.Skip or SseMapResultKind.Done
            ? result
            : throw new InvalidOperationException("The mapper returned a default SseMapResult; return Value, Skip or Done.");
    }
}
