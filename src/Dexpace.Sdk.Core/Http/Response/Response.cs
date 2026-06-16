// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Http.Response;

/// <summary>
/// An immutable HTTP response returned by a transport.
/// </summary>
/// <remarks>
/// The <see cref="Body"/> is not pre-buffered — callers own its lifecycle and must dispose the
/// response (which disposes the body) to release the underlying connection. The metadata
/// (<see cref="Status"/>, <see cref="Headers"/>, <see cref="Protocol"/>) is immutable and safe to
/// share, but the body carries single-use read state.
/// </remarks>
public sealed class Response : IAsyncDisposable, IDisposable
{
    /// <summary>Creates a response.</summary>
    /// <param name="status">The status code.</param>
    /// <param name="headers">The response headers (defaults to empty).</param>
    /// <param name="body">The response body (defaults to an empty buffered body).</param>
    /// <param name="protocol">The negotiated protocol version (defaults to HTTP/1.1).</param>
    public Response(
        Status status,
        Headers? headers = null,
        ResponseBody? body = null,
        Protocol protocol = Protocol.Http11)
    {
        Status = status;
        Headers = headers ?? Headers.Empty;
        Body = body ?? ResponseBody.FromBytes(ReadOnlyMemory<byte>.Empty);
        Protocol = protocol;
    }

    /// <summary>The response status code.</summary>
    public Status Status { get; }

    /// <summary>The response headers; may be empty but never <see langword="null"/>.</summary>
    public Headers Headers { get; }

    /// <summary>The response body; never <see langword="null"/> (empty bodies are buffered).</summary>
    public ResponseBody Body { get; }

    /// <summary>The negotiated protocol version.</summary>
    public Protocol Protocol { get; }

    /// <summary>Shorthand for <c>Status.IsSuccess</c>.</summary>
    public bool IsSuccess => Status.IsSuccess;

    /// <summary>
    /// Throws <see cref="HttpResponseException"/> if the status is not in the 2xx success range.
    /// </summary>
    /// <remarks>
    /// When the status is an error, the response body is drained up to
    /// <see cref="MaxBufferedErrorBytes"/> into an in-memory buffer and attached to the
    /// thrown exception so that <see cref="HttpResponseException.GetErrorAsync{T}"/> can read
    /// it. The cap guards against oversized error pages consuming unbounded memory.
    /// </remarks>
    /// <param name="cancellationToken">A token that can cancel the body-drain operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the check has been performed.</returns>
    /// <exception cref="HttpResponseException">
    /// The response status is not in the 2xx range. The exception carries a buffered copy of
    /// the error body (up to <see cref="MaxBufferedErrorBytes"/> bytes).
    /// </exception>
    public async ValueTask EnsureSuccessAsync(CancellationToken cancellationToken = default)
    {
        if (IsSuccess)
        {
            return;
        }

        // Drain and cap the body so the caller can read it from the exception.
        var rawBytes = await DrainCappedAsync(Body, MaxBufferedErrorBytes, cancellationToken)
            .ConfigureAwait(false);

        var bufferedBody = ResponseBody.FromBytes(rawBytes, Body.ContentType);
        var bufferedResponse = new Response(Status, Headers, bufferedBody, Protocol);
        throw new HttpResponseException(bufferedResponse);
    }

    /// <summary>
    /// The maximum number of bytes buffered from an error response body by
    /// <see cref="EnsureSuccessAsync"/>. Larger bodies are silently truncated to this limit.
    /// </summary>
    public const int MaxBufferedErrorBytes = 1024 * 1024; // 1 MiB

    private static async Task<byte[]> DrainCappedAsync(
        ResponseBody body,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await body.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();

        var remaining = maxBytes;
        var chunk = new byte[Math.Min(81920, maxBytes)];

        while (remaining > 0)
        {
            var toRead = Math.Min(chunk.Length, remaining);
            var read = await stream.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
            remaining -= read;
        }

        return buffer.ToArray();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Body.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Body.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
