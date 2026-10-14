// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json.Serialization;
using Dexpace.Sdk.Conformance.Codec;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.AotSmoke;

/// <summary>
/// Source-generated metadata for the conformance kit's codec-suite probe models (phase 10, P10-8, P10-17): the only serialization path that survives
/// NativeAOT, in its own context so the shared one is not edited. One line per probe model, in the default generation mode.
/// </summary>
[JsonSerializable(typeof(ProbeAddress))]
[JsonSerializable(typeof(ProbeContact))]
[JsonSerializable(typeof(ProbePatch))]
[JsonSerializable(typeof(ProbeSequence))]
[JsonSerializable(typeof(Tristate<string>))]
[JsonSerializable(typeof(ProbeUnencodable))]
internal sealed partial class SmokeProbeContext : JsonSerializerContext;
