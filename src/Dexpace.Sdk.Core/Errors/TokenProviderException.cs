// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// A token provider returned a default or an already-expired token (AUTH-35).
/// </summary>
/// <remarks>
/// Never retryable and never contains the token.
/// </remarks>
public sealed class TokenProviderException : SdkException
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="message">A description that does not contain the token.</param>
    public TokenProviderException(string message)
        : base(message)
    {
    }
}
