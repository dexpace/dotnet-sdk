// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>The base type for request/response body and stream lifecycle violations.</summary>
public class StreamingException : SdkException
{
    /// <summary>Initializes a new instance.</summary>
    public StreamingException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public StreamingException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public StreamingException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A single-use body or stream was consumed more than once.</summary>
public sealed class StreamConsumedException : StreamingException
{
    /// <summary>Initializes a new instance.</summary>
    public StreamConsumedException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public StreamConsumedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public StreamConsumedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A read or write was attempted on a body whose stream has already been closed.</summary>
public sealed class StreamClosedException : StreamingException
{
    /// <summary>Initializes a new instance.</summary>
    public StreamClosedException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public StreamClosedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public StreamClosedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A buffered accessor was used before the response body had been read into memory.</summary>
public sealed class ResponseNotReadException : StreamingException
{
    /// <summary>Initializes a new instance.</summary>
    public ResponseNotReadException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public ResponseNotReadException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public ResponseNotReadException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>A pipeline policy aborted the exchange before a response was produced.</summary>
public sealed class PipelineAbortedException : SdkException
{
    /// <summary>Initializes a new instance.</summary>
    public PipelineAbortedException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public PipelineAbortedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public PipelineAbortedException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
