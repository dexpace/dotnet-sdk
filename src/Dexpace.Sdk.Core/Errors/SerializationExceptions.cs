// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>A value could not be serialized into a request payload.</summary>
public sealed class SerializationException : SdkException
{
    /// <summary>Initializes a new instance.</summary>
    public SerializationException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public SerializationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public SerializationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A response payload could not be deserialized into the requested type.</summary>
public sealed class DeserializationException : SdkException
{
    /// <summary>Initializes a new instance.</summary>
    public DeserializationException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public DeserializationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public DeserializationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
