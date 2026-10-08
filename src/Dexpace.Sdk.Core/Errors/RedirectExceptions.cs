// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The base type for redirects the SDK refuses to follow and cannot return (REDIR-6, REDIR-15).
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="ServiceRequestException"/>: that base means "never sent, retry-safe", and these are
/// refusals made after a response arrived. They carry no URL property: a structured logger that destructures exception
/// properties would otherwise publish a raw query token (XCUT-19), so the message carries the redacted URLs instead.
/// </remarks>
public class RedirectException : SdkException
{
    /// <summary>Initializes a new instance.</summary>
    public RedirectException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public RedirectException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public RedirectException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A redirect from <c>https</c> to <c>http</c> was refused because
/// <see cref="Configuration.RedirectOptions.AllowHttpsToHttpDowngrade"/> is off (REDIR-15).
/// </summary>
/// <remarks>
/// <para>Raised after a response arrived, so it is not a <see cref="ServiceRequestException"/> and is never retried.</para>
/// <para><b>Breaking:</b> the 3xx response used to be returned to the caller; it is now disposed and this is thrown.</para>
/// </remarks>
public sealed class RedirectSchemeDowngradeException : RedirectException
{
    /// <summary>Initializes a new instance.</summary>
    public RedirectSchemeDowngradeException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public RedirectSchemeDowngradeException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public RedirectSchemeDowngradeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A method-preserving redirect (301, 302, 307, 308) was refused because the request body cannot be sent again (REDIR-6).
/// </summary>
/// <remarks>
/// <para>
/// Buffer the body with <see cref="Http.Request.RequestBody.ToReplayableAsync(CancellationToken)"/> before the first send.
/// Raised after a response arrived, so it is not a <see cref="ServiceRequestException"/> and is never retried.
/// </para>
/// <para><b>Breaking:</b> the 3xx response used to be returned to the caller; it is now disposed and this is thrown.</para>
/// </remarks>
public sealed class RedirectBodyNotReplayableException : RedirectException
{
    /// <summary>Initializes a new instance.</summary>
    public RedirectBodyNotReplayableException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public RedirectBodyNotReplayableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cause.</param>
    public RedirectBodyNotReplayableException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
