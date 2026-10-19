// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Codec;

/// <summary>A nested record of the codec suite's probe models (phase 10, P10-8): the value inside <see cref="ProbeContact"/> and inside a present <see cref="ProbePatch.Address"/>.</summary>
/// <param name="Street">A street line.</param>
/// <param name="City">A city.</param>
public sealed record ProbeAddress(string Street, string City);
