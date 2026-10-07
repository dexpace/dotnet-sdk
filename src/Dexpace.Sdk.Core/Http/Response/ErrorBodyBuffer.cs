// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.IO;

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// The one error-body capture in core (RECOV-16, P4b-16): drains at most <see cref="Response.MaxBufferedErrorBytes"/>
/// of the body into a replayable copy and releases the original.
/// </summary>
/// <remarks>
/// The truncation at the cap is hard and markerless. After a successful drain the original is disposed with a plain
/// <c>Dispose</c>, so a failure there propagates (the behaviour <see cref="Response.EnsureSuccessAsync"/> always had).
/// After a failed drain the original is disposed through <see cref="Disposal"/> with the drain's exception as primary:
/// a dispose failure lands on the drain exception's trail and never replaces it.
/// </remarks>
internal static class ErrorBodyBuffer
{
    /// <summary>Buffers the error body of <paramref name="response"/> and disposes it.</summary>
    /// <param name="response">The error response; disposed before this returns, whether or not the drain completed.</param>
    /// <param name="cancellationToken">A token that stops the drain.</param>
    /// <returns>A new response over a replayable copy of the (possibly truncated) body.</returns>
    internal static Response Capture(Response response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var contentType = response.Body.ContentType;
        byte[] bytes;
        try
        {
            using var stream = response.Body.OpenRead(cancellationToken);
            var writer = new ArrayBufferWriter<byte>();
            StreamCopy.DrainUpTo(stream, writer, Response.MaxBufferedErrorBytes, cancellationToken);
            bytes = writer.WrittenSpan.ToArray();
        }
        catch (Exception ex)
        {
            Disposal.DisposeQuietly(response, ex);
            throw;
        }

        response.Dispose();
        return response.WithBody(ResponseBody.FromReplayableBytes(bytes, contentType));
    }

    /// <summary>Buffers the error body of <paramref name="response"/> and disposes it, asynchronously.</summary>
    /// <param name="response">The error response; disposed before this returns, whether or not the drain completed.</param>
    /// <param name="cancellationToken">A token that stops the drain.</param>
    /// <returns>A new response over a replayable copy of the (possibly truncated) body.</returns>
    internal static async ValueTask<Response> CaptureAsync(Response response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var contentType = response.Body.ContentType;
        byte[] bytes;
        try
        {
            var stream = await response.Body.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            await using var streamScope = stream.ConfigureAwait(false);
            var writer = new ArrayBufferWriter<byte>();
            await StreamCopy.DrainUpToAsync(stream, writer, Response.MaxBufferedErrorBytes, cancellationToken)
                .ConfigureAwait(false);
            bytes = writer.WrittenSpan.ToArray();
        }
        catch (Exception ex)
        {
            await Disposal.DisposeQuietlyAsync(response, ex).ConfigureAwait(false);
            throw;
        }

        await response.DisposeAsync().ConfigureAwait(false);
        return response.WithBody(ResponseBody.FromReplayableBytes(bytes, contentType));
    }
}
