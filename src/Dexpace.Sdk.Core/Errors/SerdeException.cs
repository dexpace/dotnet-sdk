// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The root of every serde failure: a value could not be encoded (<see cref="SerializationException"/>) or a payload
/// could not be decoded (<see cref="DeserializationException"/>).
/// </summary>
/// <remarks>
/// Abstract, so every thrown serde failure is one of the two subtypes (or a subtype of one) and a handler that
/// distinguishes encode from decode never meets a third, unclassified kind (SEAM-23). Both subtypes are unsealed, so
/// codegen and adapters can add more specific ones; this departs from styleguide 8.6 and 6.2 on purpose.
/// </remarks>
public abstract class SerdeException : SdkException
{
    /// <summary>Initializes a new instance.</summary>
    protected SerdeException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    protected SerdeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    protected SerdeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
