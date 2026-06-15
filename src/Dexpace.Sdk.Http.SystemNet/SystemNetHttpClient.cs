// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet;

/// <summary>
/// A transport that adapts <c>System.Net.Http.HttpClient</c> to the SDK's
/// <see cref="IAsyncHttpClient"/> and <see cref="IHttpClient"/> SPIs.
/// </summary>
/// <remarks>
/// <para>
/// The response body is delivered as a live stream (<see cref="HttpCompletionOption.ResponseHeadersRead"/>)
/// rather than buffered, so callers must dispose the returned <see cref="Response"/> to release the
/// connection.
/// </para>
/// <para>
/// <b>Ownership.</b> When constructed with a caller-supplied <c>HttpClient</c> the underlying client
/// is <em>not</em> disposed by this adapter; when constructed with the parameterless constructor the
/// adapter owns and disposes an internally created client.
/// </para>
/// </remarks>
public sealed class SystemNetHttpClient : IAsyncHttpClient, IHttpClient
{
    private readonly SystemHttpClient _client;
    private readonly bool _ownsClient;

    /// <summary>Creates a transport backed by an internally owned <c>HttpClient</c>.</summary>
    public SystemNetHttpClient()
        : this(new SystemHttpClient(), ownsClient: true)
    {
    }

    /// <summary>
    /// Creates a transport backed by a caller-supplied <c>HttpClient</c>. The supplied client is not
    /// disposed when this adapter is disposed.
    /// </summary>
    /// <param name="client">The HTTP client to wrap.</param>
    public SystemNetHttpClient(SystemHttpClient client)
        : this(client, ownsClient: false)
    {
    }

    private SystemNetHttpClient(SystemHttpClient client, bool ownsClient)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _ownsClient = ownsClient;
    }

    /// <inheritdoc/>
    public async Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var message = ToHttpRequestMessage(request);

        HttpResponseMessage response;
        try
        {
            response = await _client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Cancellation not requested by the caller ⇒ the client's internal timeout fired.
            throw new ServiceRequestTimeoutException("The request timed out before a response was received.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ServiceRequestException("The request could not be sent to the server.", ex);
        }

        return ToResponse(response);
    }

    /// <inheritdoc/>
    public Response Execute(Request request) =>
        ExecuteAsync(request).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static HttpRequestMessage ToHttpRequestMessage(Request request)
    {
        var message = new HttpRequestMessage(new HttpMethod(request.Method.Name), request.Url);
        if (request.Body is { } body)
        {
            message.Content = new RequestBodyContent(body);
        }

        foreach (var (name, values) in request.Headers)
        {
            foreach (var value in values)
            {
                // Content-Type is owned by the content (set in RequestBodyContent); skip it here.
                if (string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!message.Headers.TryAddWithoutValidation(name, value))
                {
                    message.Content?.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }

        return message;
    }

    private static Response ToResponse(HttpResponseMessage message)
    {
        var headersBuilder = new Headers.Builder();
        foreach (var (name, values) in message.Headers)
        {
            foreach (var value in values)
            {
                headersBuilder.Add(name, value);
            }
        }

        foreach (var (name, values) in message.Content.Headers)
        {
            foreach (var value in values)
            {
                headersBuilder.Add(name, value);
            }
        }

        return new Response(
            Status.FromCode((int)message.StatusCode),
            headersBuilder.Build(),
            new HttpResponseMessageBody(message),
            MapProtocol(message.Version));
    }

    private static Protocol MapProtocol(Version version) => version switch
    {
        { Major: 1, Minor: 0 } => Protocol.Http10,
        { Major: 1, Minor: 1 } => Protocol.Http11,
        { Major: 2 } => Protocol.Http2,
        { Major: 3 } => Protocol.Quic,
        _ => Protocol.Http11,
    };
}
