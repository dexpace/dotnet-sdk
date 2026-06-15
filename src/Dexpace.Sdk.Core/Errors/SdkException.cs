// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The base type for every exception the SDK raises.
/// </summary>
/// <remarks>
/// The hierarchy distinguishes three transport failure shapes —
/// <see cref="ServiceRequestException"/> (request never reached the server, safe to retry on
/// idempotent methods), <see cref="ServiceResponseException"/> (request sent but the response
/// could not be read), and <see cref="HttpResponseException"/> (a 4xx/5xx received intact) —
/// alongside body/stream lifecycle, serialization, and pipeline failures.
/// </remarks>
public class SdkException : Exception
{
    /// <summary>Initializes a new instance.</summary>
    public SdkException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public SdkException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public SdkException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
