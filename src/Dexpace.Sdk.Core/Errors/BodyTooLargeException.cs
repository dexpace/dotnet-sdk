// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// A body was refused because materialising it in memory would exceed a size limit (IO-9, P3a-12): the response-side
/// readers refuse more than <c>ResponseBody.DefaultMaxMaterializedBytes</c>, and <c>RequestBody.ToReplayable</c> refuses
/// more than the host's array limit.
/// </summary>
/// <remarks>
/// The message names the limit and, when known, the declared length, and points at reading the body as a stream with
/// <c>OpenRead</c> or <c>OpenReadAsync</c>. It never echoes body content. This type is distinct from
/// <see cref="StreamConsumedException"/>: nothing was consumed twice, the body was simply too large to buffer.
/// </remarks>
public sealed class BodyTooLargeException : StreamingException
{
    /// <summary>Initializes a new instance.</summary>
    public BodyTooLargeException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public BodyTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public BodyTooLargeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
