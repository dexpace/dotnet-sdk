// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>Why the policy stopped following and returned the current response open (REDIR-22(c)).</summary>
internal enum RedirectStopReason
{
    /// <summary>The status is not 301, 302, 303, 307 or 308 (REDIR-1, REDIR-2).</summary>
    NotARedirect = 0,

    /// <summary>The method, status or predicate declines this redirect (REDIR-3 to REDIR-5, REDIR-20).</summary>
    NotEligible = 1,

    /// <summary>The <c>Location</c> is absent or unusable (REDIR-18, REDIR-19).</summary>
    MalformedLocation = 2,

    /// <summary>The target was already visited on this call (REDIR-16).</summary>
    LoopDetected = 3,

    /// <summary>The hop cap is reached (REDIR-17).</summary>
    HopCap = 4,
}
