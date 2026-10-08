// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Resilience;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The request never reached the server (DNS failure, connection refused, TLS handshake failure).
/// </summary>
/// <remarks>Safe to retry on idempotent methods.</remarks>
public class ServiceRequestException : SdkException
{
    /// <summary>Gets <see langword="true"/>: a failure before a response was received is always retryable (XCUT-4, RETRY-4).</summary>
    public sealed override bool IsRetryable => true;

    /// <summary>Initializes a new instance.</summary>
    public ServiceRequestException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ServiceRequestException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ServiceRequestException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A timeout occurred before the request could be dispatched to the server.
/// </summary>
public sealed class ServiceRequestTimeoutException : ServiceRequestException
{
    /// <summary>Initializes a new instance.</summary>
    public ServiceRequestTimeoutException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ServiceRequestTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ServiceRequestTimeoutException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The request was sent but the response could not be read (connection dropped mid-response,
/// decode failure on a chunked stream).
/// </summary>
public class ServiceResponseException : SdkException
{
    /// <summary>Gets <see langword="true"/>: a failure to read a received response is always retryable (XCUT-4, RETRY-4).</summary>
    public sealed override bool IsRetryable => true;

    /// <summary>Initializes a new instance.</summary>
    public ServiceResponseException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ServiceResponseException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ServiceResponseException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// An intact 4xx or 5xx response was received. Carries the <see cref="Response"/> so callers can
/// inspect status, headers, and body.
/// </summary>
public class HttpResponseException : SdkException
{
    private readonly bool _isRetryable;

    /// <summary>Initializes a new instance carrying the offending response.</summary>
    /// <param name="response">The received error response.</param>
    /// <param name="message">An optional message; defaults to the status line.</param>
    public HttpResponseException(Response response, string? message = null)
        : base(message ?? $"The server returned an error response: {response.Status}.")
    {
        Response = response;
        Status = response.Status;
        _isRetryable = RetryFacts.IsRetryableStatus(response.Status.Code);
    }

    /// <summary>
    /// Gets whether the status is retryable by the single classifier (408, 429, 500 to 599 except 501 and 505), computed
    /// once at construction (XCUT-5, RETRY-3). The retry engines decide by the configured status set instead (RETRY-37).
    /// </summary>
    public sealed override bool IsRetryable => _isRetryable;

    /// <summary>The received error response.</summary>
    public Response Response { get; }

    /// <summary>The status code of the error response.</summary>
    public Status Status { get; }

    /// <summary>
    /// Deserializes the error response body as <typeparamref name="T"/> using <paramref name="serde"/>.
    /// </summary>
    /// <typeparam name="T">The error model type.</typeparam>
    /// <param name="serde">The serializer.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The deserialized error model (possibly <see langword="null"/>).</returns>
    /// <exception cref="ResponseNotReadException">The error body has already been consumed.</exception>
    /// <exception cref="DeserializationException">Deserialization failed.</exception>
    public async ValueTask<T?> GetErrorAsync<T>(ISerde serde, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serde);
        try
        {
            var stream = await Response.Body.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            await using var streamScope = stream.ConfigureAwait(false);
            return await serde.DeserializeAsync<T>(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (StreamConsumedException ex)
        {
            throw new ResponseNotReadException(
                "The error response body has already been consumed and cannot be deserialized.", ex);
        }
    }
}
