// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>Which refusal a failing decision carries, so the policy can emit the right event without a type test.</summary>
internal enum RedirectFailureKind
{
    /// <summary>Not a failure.</summary>
    None = 0,

    /// <summary>An https to http hop without the opt-in (REDIR-15).</summary>
    SchemeDowngrade = 1,

    /// <summary>A method-preserving hop over a body that cannot be re-sent (REDIR-6).</summary>
    BodyNotReplayable = 2,
}
