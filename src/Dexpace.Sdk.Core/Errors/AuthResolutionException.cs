// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Auth;

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// No configured authentication scheme satisfies the requirements of a call (AUTH-6).
/// </summary>
/// <remarks>
/// Raised before anything is sent. It is not a <see cref="ServiceRequestException"/> and is never retryable (P6c-41).
/// The message lists scheme names only, never a scope or a parameter.
/// </remarks>
public sealed class AuthResolutionException : SdkException
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="required">The schemes the selected descriptor accepts, in preference order.</param>
    /// <param name="available">The schemes the policy can serve.</param>
    public AuthResolutionException(IReadOnlyList<AuthScheme> required, IReadOnlyList<AuthScheme> available)
        : base(BuildMessage(required, available))
    {
        Required = [.. required];
        Available = [.. available];
    }

    /// <summary>The schemes the selected descriptor accepts, in preference order.</summary>
    public IReadOnlyList<AuthScheme> Required { get; }

    /// <summary>The schemes the policy can serve, in enum order.</summary>
    public IReadOnlyList<AuthScheme> Available { get; }

    private static string BuildMessage(IReadOnlyList<AuthScheme> required, IReadOnlyList<AuthScheme> available)
    {
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(available);
        return $"No configured authentication scheme satisfies the operation: required [{string.Join(", ", required)}], " +
            $"available [{string.Join(", ", available)}].";
    }
}
