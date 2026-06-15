// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// Adapts a <see cref="RequestBody"/> to <see cref="HttpContent"/> so it can be streamed by
/// <see cref="System.Net.Http.HttpClient"/> without first buffering into memory.
/// </summary>
internal sealed class RequestBodyContent : HttpContent
{
    private readonly RequestBody _body;

    public RequestBodyContent(RequestBody body)
    {
        _body = body;
        if (body.ContentType is { } contentType)
        {
            Headers.TryAddWithoutValidation("Content-Type", contentType.ToString());
        }
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        _body.WriteToAsync(stream);

    protected override Task SerializeToStreamAsync(
        Stream stream,
        TransportContext? context,
        CancellationToken cancellationToken) =>
        _body.WriteToAsync(stream, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        var declared = _body.ContentLength;
        if (declared >= 0)
        {
            length = declared;
            return true;
        }

        length = 0;
        return false;
    }
}
