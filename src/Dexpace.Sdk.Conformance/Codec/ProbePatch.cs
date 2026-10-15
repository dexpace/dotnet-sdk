// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Conformance.Codec;

/// <summary>
/// The PATCH probe model: three <see cref="Tristate{T}"/> members of three element types (a string, the value type <see cref="int"/> and a
/// record). Each member's declared default is <see cref="Tristate{T}.Absent"/>, as <c>SERDE-17</c> requires, so a record built with
/// no argument for it, and a document with no key for it, both mean absent.
/// </summary>
/// <param name="Name">A string member.</param>
/// <param name="Age">A value-type member.</param>
/// <param name="Address">A record member.</param>
public sealed record ProbePatch(Tristate<string> Name, Tristate<int> Age, Tristate<ProbeAddress> Address);
