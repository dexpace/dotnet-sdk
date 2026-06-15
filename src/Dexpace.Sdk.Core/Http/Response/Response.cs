// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

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
