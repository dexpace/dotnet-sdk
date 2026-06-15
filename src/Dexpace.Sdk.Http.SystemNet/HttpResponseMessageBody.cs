// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// A <see cref="ResponseBody"/> backed by an <see cref="HttpResponseMessage"/>'s content stream.
/// Disposing the body disposes the underlying <see cref="HttpResponseMessage"/>, releasing the
/// connection back to the pool.
/// </summary>
internal sealed class HttpResponseMessageBody : ResponseBody
{
    private readonly HttpResponseMessage _message;
    private int _consumed;

    public HttpResponseMessageBody(HttpResponseMessage message)
    {
        _message = message;
        var mediaType = message.Content.Headers.ContentType?.ToString();
        ContentType = mediaType is not null ? MediaType.Parse(mediaType) : null;
        ContentLength = message.Content.Headers.ContentLength ?? -1;
    }

    public override MediaType? ContentType { get; }

    public override long ContentLength { get; }

    public override async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _consumed, 1) != 0)
        {
            throw new StreamConsumedException("This response body has already been read.");
        }

        return await _message.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _message.Dispose();
        base.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        _message.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
