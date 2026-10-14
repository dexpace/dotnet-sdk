// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// How a single-implementation seam resolves, for <c>XCUT-23</c>: the three situations of the requirement as actions the
/// driver wires over its own composition root (the DI package, for the SDK).
/// </summary>
/// <param name="ResolveWithNone">Resolves the seam with no candidate installed; must throw an actionable error.</param>
/// <param name="ResolveWithTwo">Resolves the seam with two candidates and no explicit selection; must throw an error naming the choice.</param>
/// <param name="ExplicitWins">Installs an explicit candidate beside a discoverable one and returns whether the explicit one was chosen.</param>
public sealed record SeamResolution(Action ResolveWithNone, Action ResolveWithTwo, Func<bool> ExplicitWins);
