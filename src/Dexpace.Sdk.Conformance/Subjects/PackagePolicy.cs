// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// What one package may depend on beyond core and the logging facade (NFR-2, design §10 entry 2), mirroring the policy record of
/// <c>scripts/ci/dependency-audit.cs</c>: its one third-party root, whose lock-stepped family counts as one library, and the first-party
/// packages it may reference. A test fails if the two tables disagree.
/// </summary>
/// <param name="ThirdPartyRoot">The package id of the one third-party library the package may depend on, or <see langword="null"/> for none.</param>
/// <param name="FirstPartyIds">The package ids of the other first-party packages it may depend on, beyond core.</param>
public sealed record PackagePolicy(string? ThirdPartyRoot, IReadOnlyList<string> FirstPartyIds);
