// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Errors;

/// <summary>
/// An auth policy refused to attach a credential to a request that is not going over <c>https</c> (AUTH-28, XCUT-16).
/// </summary>
/// <remarks>
/// Raised before any credential is resolved and before anything is sent. It is not a
/// <see cref="ServiceRequestException"/> and is never retryable, so no retry policy re-drives it (P6c-41). There is no
/// loopback exemption.
/// <para>
/// <b>Breaking:</b> the refusal was a plain <see cref="SdkException"/>; this type is assignable to it.
/// </para>
/// </remarks>
public sealed class HttpsRequiredException : SdkException
{
    /// <summary>Initializes the exception.</summary>
    /// <param name="policyName">The name of the policy that refused.</param>
    /// <param name="scheme">The URL scheme that was refused.</param>
    public HttpsRequiredException(string policyName, string scheme)
        : base(BuildMessage(policyName, scheme))
    {
        PolicyName = policyName;
        Scheme = scheme;
    }

    /// <summary>The name of the policy that refused.</summary>
    public string PolicyName { get; }

    /// <summary>The URL scheme that was refused.</summary>
    public string Scheme { get; }

    private static string BuildMessage(string policyName, string scheme)
    {
        ArgumentNullException.ThrowIfNull(policyName);
        ArgumentNullException.ThrowIfNull(scheme);
        return $"{policyName} refused to attach a credential to a request over the '{scheme}' scheme; " +
            "credentials are only sent over https.";
    }
}
