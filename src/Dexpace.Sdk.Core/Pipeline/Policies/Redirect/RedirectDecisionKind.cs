// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>The three outcomes of one redirect decision.</summary>
internal enum RedirectDecisionKind
{
    /// <summary>Re-issue the built request.</summary>
    Follow = 0,

    /// <summary>Stop and return the current response open.</summary>
    ReturnCurrent = 1,

    /// <summary>Dispose the current response and throw.</summary>
    Fail = 2,
}
