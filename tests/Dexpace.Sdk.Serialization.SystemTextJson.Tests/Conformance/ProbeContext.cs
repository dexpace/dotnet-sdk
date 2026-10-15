// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json.Serialization;
using Dexpace.Sdk.Conformance.Codec;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests.Conformance;

/// <summary>
/// The metadata the codec suite's probe models need (phase 10, P10-8): one line per model, in the default generation mode (not the fast-path-only
/// mode, which cannot carry the tri-state wiring, CLAUDE.md). <c>Tristate&lt;string&gt;</c> is registered for the suite's top-level case
/// (<c>SERDE-20</c>); <see cref="ProbeUnencodable"/> is registered too, so its failure is the codec's own and not a missing-metadata one.
/// </summary>
[JsonSerializable(typeof(ProbeAddress))]
[JsonSerializable(typeof(ProbeContact))]
[JsonSerializable(typeof(ProbePatch))]
[JsonSerializable(typeof(ProbeSequence))]
[JsonSerializable(typeof(Tristate<string>))]
[JsonSerializable(typeof(ProbeUnencodable))]
internal sealed partial class ProbeContext : JsonSerializerContext;
