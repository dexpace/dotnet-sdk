// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The normative strength of a requirement, in the RFC 2119 sense the product specification uses (appendix C's
/// <c>Level</c> column). <c>MUST NOT</c> is folded into <see cref="Must"/>: it is a prohibition with the same weight.
/// </summary>
public enum RequirementLevel
{
    /// <summary>The requirement is absolute (<c>MUST</c>, <c>MUST NOT</c>): a transport that fails it does not conform.</summary>
    Must = 0,

    /// <summary>The requirement is a recommendation (<c>SHOULD</c>): a transport may decline it, with a stated reason.</summary>
    Should = 1,

    /// <summary>The requirement is optional (<c>MAY</c>): a transport that declines it is still conforming.</summary>
    May = 2,
}
