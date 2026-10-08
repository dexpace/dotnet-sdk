// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The call exceeded <c>DexpaceClientOptions.OverallTimeout</c> (XCUT-1).
/// </summary>
/// <remarks>
/// <para>
/// This is the exception a caller who never cancelled sees when the overall deadline expires; a call the caller cancelled
/// still surfaces <see cref="OperationCanceledException"/>. It is not retryable (<see cref="SdkException.IsRetryable"/> is
/// <see langword="false"/>): the policy that throws it is the outermost, and the deadline covers every retry and redirect
/// hop. Every earlier attempt's failure is on <see cref="SdkException.Suppressed"/> (RETRY-34). The inner exception is the
/// cancellation the deadline caused.
/// </para>
/// </remarks>
public sealed class OperationTimeoutException : SdkException
{
    /// <summary>Initializes a new instance.</summary>
    public OperationTimeoutException()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The error message.</param>
    public OperationTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The cancellation the deadline caused.</param>
    public OperationTimeoutException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
