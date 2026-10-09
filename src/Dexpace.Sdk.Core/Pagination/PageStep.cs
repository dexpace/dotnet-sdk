// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.Pagination;

/// <summary>
/// The one fetch-and-parse step behind both engines: send one page, read its body once, parse it, snapshot the items,
/// close the response (PAGE-4, PAGE-11, PAGE-13, PAGE-15, PAGE-27, PAGE-28, PAGE-32, PAGE-33; design P7c-4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a step outside the iterators.</b> C# forbids <c>try</c>/<c>catch</c> around a <c>yield return</c>, and the step
/// must close the response on every path <em>before</em> its page is yielded, so no response is live at any
/// <c>yield</c> (design section 10 entry 17, the .NET answer to PAGE-3 and PAGE-12). The two iterator shells
/// (<c>StrategyPageable</c>, <c>StrategyBlockingPageable</c>) stay a dozen lines each.
/// </para>
/// <para>
/// <b>One body for two engines.</b> With <c>async: false</c> every <c>await</c> is guarded by the flag, so the state
/// machine completes synchronously and the blocking shell reads the returned task through
/// <see cref="SyncPath.GetCompletedResult{T}"/> (the pattern of the pipeline's <c>SendCoreAsync</c>). The blocking read
/// buffers the body through the stream under <c>ReadAsBytes</c>'s 64 MiB cap (but does not close the body early); the async read streams.
/// </para>
/// </remarks>
internal static class PageStep
{
    /// <summary>Fetches and parses the page at <paramref name="current"/>.</summary>
    /// <typeparam name="TPage">The envelope type.</typeparam>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="walk">The walk's constants.</param>
    /// <param name="current">The request to send; the page records it as <c>Page.Request</c> (P7c-7).</param>
    /// <param name="async">Whether to use the async seam and the async body read; otherwise nothing awaits.</param>
    /// <param name="cancellationToken">The token for the send and the read.</param>
    /// <returns>The page and the next request. Exceptions surface as themselves (PAGE-28).</returns>
    internal static async ValueTask<FetchedStep<T>> FetchAsync<TPage, T>(
        PageWalk<TPage, T> walk,
        Request current,
        bool async,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(walk, current, async, cancellationToken).ConfigureAwait(false);
        var step = await ParseAsync(walk, current, response, async, cancellationToken).ConfigureAwait(false);

        // The success-path close is unguarded: a failure to release propagates from the walk (PAGE-15, PAGE-32).
        if (async)
        {
            await response.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            response.Dispose();
        }

        return step;
    }

    // Sends the request; rejects a null response (PAGE-28) and discards one delivered after the token was cancelled
    // (PAGE-33, P7c-14).
    private static async ValueTask<Response> SendAsync<TPage, T>(
        PageWalk<TPage, T> walk,
        Request current,
        bool async,
        CancellationToken cancellationToken)
    {
        var response = async
            ? await walk.AsyncClient!.ExecuteAsync(current, walk.Options, cancellationToken).ConfigureAwait(false)
            : walk.BlockingClient!.Execute(current, walk.Options, cancellationToken);
        if (response is null)
        {
            var client = async ? walk.AsyncClient!.GetType() : walk.BlockingClient!.GetType();
            throw new InvalidOperationException($"The client {client.Name} returned a null response (SEAM-16).");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            await CloseQuietlyAsync(response, primary: null, async).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }

        return response;
    }

    // Reads, parses and snapshots. Any non-fatal failure closes the response with the failure as the primary exception,
    // so a failing release lands on its ExceptionTrail instead of replacing it (PAGE-13).
    private static async ValueTask<FetchedStep<T>> ParseAsync<TPage, T>(
        PageWalk<TPage, T> walk,
        Request current,
        Response response,
        bool async,
        CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await ReadEnvelopeAsync<TPage>(response, walk.Serde, async, cancellationToken).ConfigureAwait(false)
                ?? throw new DeserializationException(
                    $"The serde returned null for page type '{typeof(TPage).FullName}'; a page envelope must be non-null.");
            var info = walk.Strategy.Parse(envelope, response, walk.First)
                ?? throw new InvalidOperationException(
                    $"The page strategy {walk.Strategy.GetType().Name} returned null; return a PageInfo whose NextRequest is " +
                    "null to end the stream (PAGE-4).");

            // The items are copied so a strategy that hands back a live view of its envelope cannot change a page
            // after it was yielded (PAGE-2, PAGE-11).
            var page = new Page<T>(Array.AsReadOnly(info.Items.ToArray()), response.Status, response.Headers, current);
            return new FetchedStep<T>(page, info.NextRequest);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            await CloseQuietlyAsync(response, ex, async).ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask<TPage?> ReadEnvelopeAsync<TPage>(
        Response response,
        Serialization.ISerde serde,
        bool async,
        CancellationToken cancellationToken)
    {
        if (!async)
        {
            // ReadAsBytes would also close the body (BODY-16), so a release failure would surface from the read, before the
            // strategy ran, and could never be a suppressed secondary of a parse failure (PAGE-13). Read the stream under
            // the same 64 MiB cap and leave the body to the response's own close.
            using var source = response.Body.OpenRead(cancellationToken);
            var bytes = BodyMaterializer.ReadAll(source, response.Body.ContentLength, ResponseBody.DefaultMaxMaterializedBytes, cancellationToken);
            return serde.Deserialize<TPage>(bytes);
        }

        var stream = await response.Body.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        await using var streamScope = stream.ConfigureAwait(false);
        return await serde.DeserializeAsync<TPage>(stream, cancellationToken).ConfigureAwait(false);
    }

    private static ValueTask CloseQuietlyAsync(Response response, Exception? primary, bool async)
    {
        if (async)
        {
            return Disposal.DisposeQuietlyAsync(response, primary);
        }

        Disposal.DisposeQuietly(response, primary);
        return ValueTask.CompletedTask;
    }
}
