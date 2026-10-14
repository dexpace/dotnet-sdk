// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// An error type the SDK has never heard of, declaring through <see cref="IRetryableError"/> whether it may be retried. It
/// derives from <see cref="Exception"/> alone (no I/O family, no SDK base), so the only thing that can make a retry layer retry it
/// is the capability, not a concrete-type match (<c>XCUT-6</c>).
/// </summary>
internal sealed class ProbeRetryableError(bool retryable) : Exception("a failure from a transport the SDK has never heard of"), IRetryableError
{
    /// <inheritdoc />
    public bool IsRetryable { get; } = retryable;
}
