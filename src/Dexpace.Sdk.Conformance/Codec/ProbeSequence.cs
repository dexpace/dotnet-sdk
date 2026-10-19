// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Conformance.Codec;

/// <summary>
/// An array of <see cref="Tristate{T}"/> elements, for <c>SERDE-20</c>: an element inside an array has no key to omit, so an absent
/// element degrades to the wire null and the indices never shift.
/// </summary>
/// <param name="Items">The elements.</param>
public sealed record ProbeSequence(Tristate<string>[] Items);
