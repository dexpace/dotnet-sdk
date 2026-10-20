// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Resilience;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The request was not sent or no response was received (DNS failure, connection refused, TLS handshake failure, a connection that ended before any
/// response byte).
/// </summary>
/// <remarks>
/// <para>
/// Safe to retry on idempotent methods. It derives from <see cref="IOException"/>, the platform's I/O type (TRANSPORT-20, XCUT-4: a transport error is
/// catchable as a generic I/O error), and chains the native exception as its <see cref="Exception.InnerException"/>. It is not an
/// <see cref="SdkException"/>: a suppressed secondary is attached through <see cref="ExceptionTrail.AddSuppressed"/>, read through
/// <see cref="ExceptionTrail.GetSuppressed"/> and rendered by <see cref="ToString"/> as an <see cref="SdkException"/>'s is.
/// </para>
/// <para><b>Breaking (phase 11):</b> was an <see cref="SdkException"/>; <c>catch (SdkException)</c> no longer catches it.</para>
/// </remarks>
public class ServiceRequestException : IOException, IRetryableError
{
    /// <summary>Gets <see langword="true"/>: a failure before a response was received is always retryable (XCUT-4, RETRY-4).</summary>
    public bool IsRetryable => true;

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

    /// <summary>Renders the exception, followed by one block per suppressed exception, as <see cref="SdkException.ToString"/> does.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => SuppressedTrailRenderer.Render(this, base.ToString());
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
    /// <param name="response">The received error response; its status must be 400 to 599.</param>
    /// <param name="message">An optional message; defaults to the status line.</param>
    /// <remarks><b>Breaking (phase 11):</b> a <see langword="null"/> response and a non-error status used to be accepted, the first as a <see cref="NullReferenceException"/>.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The status of <paramref name="response"/> is not an error status (400 to 599), XCUT-8.</exception>
    public HttpResponseException(Response response, string? message = null)
        : base(MessageFor(Validated(response), message))
    {
        Response = response;
        Status = response.Status;
        _isRetryable = RetryFacts.IsRetryableStatus(response.Status.Code);
    }

    // XCUT-8: the one public mapping route refuses a response that is not an error, so no "successful exception" is built; the guard runs inside the base
    // call's argument, before anything reads the response. The message is deliberately free of the URL (XCUT-19).
    private static Response Validated(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!response.Status.IsError)
        {
            throw new ArgumentException(
                $"The status {response.Status.Code} is not an error status (400..599); only an error response maps to an HttpResponseException.",
                nameof(response));
        }

        return response;
    }

    private static string MessageFor(Response response, string? message) =>
        message ?? $"The server returned an error response: {response.Status}.";

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

    /// <summary>
    /// Deserializes the error response body as <typeparamref name="T"/> using <paramref name="serde"/>, synchronously.
    /// </summary>
    /// <typeparam name="T">The error model type.</typeparam>
    /// <param name="serde">The serializer.</param>
    /// <returns>The deserialized error model (possibly <see langword="null"/>: an error body is read opportunistically).</returns>
    /// <remarks>
    /// It has no <see cref="CancellationToken"/> parameter, because the seam's synchronous stream decode takes none. The
    /// exception's response is not disposed.
    /// </remarks>
    /// <exception cref="ResponseNotReadException">The error body has already been consumed.</exception>
    /// <exception cref="DeserializationException">Deserialization failed.</exception>
    public T? GetError<T>(ISerde serde)
    {
        ArgumentNullException.ThrowIfNull(serde);
        try
        {
            using var stream = Response.Body.OpenRead();
            return serde.Deserialize<T>(stream);
        }
        catch (StreamConsumedException ex)
        {
            throw new ResponseNotReadException(
                "The error response body has already been consumed and cannot be deserialized.", ex);
        }
    }
}
