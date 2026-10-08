// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// The capability an exception implements to tell the retry classifier that retrying the call may succeed (XCUT-6,
/// RETRY-2; design §6.1).
/// </summary>
/// <remarks>
/// Implement this on a custom exception, in a transport or a consumer's own code, to make it retryable without
/// touching the SDK: the classifier reads this interface, never a type name. A <see langword="true"/> value anywhere in a
/// failure's cause chain makes the failure retryable; a <see langword="false"/> value never makes it non-retryable
/// (an exception that wraps an I/O error stays retryable). The re-send gate and the attempt cap still apply.
/// </remarks>
public interface IRetryableError
{
    /// <summary>Gets a value indicating whether retrying the failed call may succeed.</summary>
    bool IsRetryable { get; }
}
