// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>The page envelope the pagination suites deserialize: <c>"1,2,3|next"</c> on the wire (see <see cref="EnvelopeSerde"/>).</summary>
/// <param name="Items">The items on the page.</param>
/// <param name="Next">The continuation token, or <see langword="null"/>.</param>
internal sealed record Envelope(IReadOnlyList<int> Items, string? Next);

/// <summary>Builders for scripted pages: bodies a <see cref="EnvelopeSerde"/> can read and responders for <c>ScriptedTransport</c>.</summary>
internal static class PageFixtures
{
    /// <summary>The UTF-8 body of a page: the items comma-separated, a bar, the continuation token (empty for none).</summary>
    /// <param name="items">The items.</param>
    /// <param name="next">The continuation token, or <see langword="null"/>.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Body(IReadOnlyList<int> items, string? next) =>
        Encoding.UTF8.GetBytes(string.Join(',', items) + "|" + next);

    /// <summary>
    /// A <c>ScriptedTransport</c> entry answering <c>200</c> with a valid envelope, carrying the request the engine
    /// actually sent (so <c>response.Request</c> is what PAGE-17 and PAGE-19 read).
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="next">The continuation token, or <see langword="null"/>.</param>
    /// <param name="headers">Response headers, or none.</param>
    /// <returns>The responder.</returns>
    internal static Func<Request, Response> Respond(IReadOnlyList<int> items, string? next = null, Headers? headers = null) =>
        request => TestResponses.Create(Status.Ok, request, headers, ResponseBody.FromBytes(Body(items, next)));

    /// <summary>A responder serving <paramref name="body"/> (so the test can count disposals) as a valid <c>200</c>.</summary>
    /// <param name="body">The body.</param>
    /// <param name="headers">Response headers, or none.</param>
    /// <returns>The responder.</returns>
    internal static Func<Request, Response> Respond(ResponseBody body, Headers? headers = null) =>
        request => TestResponses.Create(Status.Ok, request, headers, body);
}

/// <summary>
/// A response body serving the bytes of a page envelope that counts its disposals and can fail its release. The
/// stock <c>TrackingResponseBody</c> serves <c>[1,2,3,4]</c>, which is not an envelope.
/// </summary>
/// <param name="bytes">The envelope bytes (<see cref="PageFixtures.Body"/>).</param>
/// <param name="disposeFailure">An exception to throw from the release, or <see langword="null"/>.</param>
/// <param name="onDispose">Called when the release runs, before it fails; lets a test order closes against yields.</param>
internal sealed class EnvelopeBody(byte[] bytes, Exception? disposeFailure = null, Action? onDispose = null) : ResponseBody
{
    private int _disposes;

    /// <summary>Creates a body with the given items and continuation token.</summary>
    /// <param name="items">The items.</param>
    /// <param name="next">The continuation token, or <see langword="null"/>.</param>
    /// <param name="disposeFailure">An exception to throw from the release, or <see langword="null"/>.</param>
    /// <param name="onDispose">Called when the release runs, before it fails.</param>
    /// <returns>The body.</returns>
    internal static EnvelopeBody Of(IReadOnlyList<int> items, string? next = null, Exception? disposeFailure = null, Action? onDispose = null) =>
        new(PageFixtures.Body(items, next), disposeFailure, onDispose);

    /// <summary>How many times the release ran (the base latch makes it at most one).</summary>
    internal int DisposeCount => Volatile.Read(ref _disposes);

    /// <inheritdoc/>
    public override MediaType? ContentType => null;

    /// <inheritdoc/>
    public override long ContentLength => bytes.Length;

    /// <inheritdoc/>
    public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));

    /// <inheritdoc/>
    public override Stream OpenRead(CancellationToken cancellationToken = default) => new MemoryStream(bytes, writable: false);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        Interlocked.Increment(ref _disposes);
        onDispose?.Invoke();
        if (disposeFailure is not null)
        {
            throw disposeFailure;
        }

        base.Dispose(disposing);
    }
}
