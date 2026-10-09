// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Net.Http;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>
/// A small pooling transport over a real <c>HttpClient</c> whose response body streams (<c>ResponseHeadersRead</c>), so a
/// connection goes back to the pool when the body stream is disposed and stays held while it is not: the shape of transport the
/// release probe exists for. Disposing it disposes the client, which closes the pool.
/// </summary>
internal sealed class PooledStreamingTransport : IAsyncHttpClient
{
    private readonly HttpClient _client = new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });

    public async Task<Response> ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, request.Url);
        var native = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var stream = await native.Content.ReadAsStreamAsync(cancellationToken);
        return new Response(request, Status.FromCode((int)native.StatusCode), Dexpace.Sdk.Core.Http.Common.Protocol.Http11, null, ResponseBody.FromStream(stream));
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
