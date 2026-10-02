// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>A value could not be serialized into a request payload.</summary>
/// <remarks>
/// <para>
/// <b>Breaking:</b> was <c>sealed</c> and derived directly from <c>SdkException</c>; unsealed and re-parented under
/// <see cref="SerdeException"/> so codegen and adapters can add more specific subtypes (SEAM-23). This departs from
/// styleguide 8.6 and 6.2 on purpose.
/// </para>
/// </remarks>
public class SerializationException : SerdeException
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
/// <remarks>
/// <para>
/// <b>Breaking:</b> was <c>sealed</c> and derived directly from <c>SdkException</c>; unsealed and re-parented under
/// <see cref="SerdeException"/> so codegen and adapters can add more specific subtypes (SEAM-23). This departs from
/// styleguide 8.6 and 6.2 on purpose.
/// </para>
/// </remarks>
public class DeserializationException : SerdeException
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
