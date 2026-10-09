// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// The three states of a <see cref="Tristate{T}"/> (SERDE-14): a field the caller did not mention, a field the caller
/// set to the wire's <c>null</c>, and a field the caller set to a value.
/// </summary>
/// <remarks>
/// The numeric values are part of the contract: <see cref="Absent"/> is zero so that <c>default(Tristate&lt;T&gt;)</c>
/// is Absent (SERDE-17) for every field, property and record parameter that sets no initializer.
/// </remarks>
public enum TristateState
{
    /// <summary>The field is not on the wire: a writer omits the key, and a reader finds no key.</summary>
    Absent = 0,

    /// <summary>The field is on the wire as the literal <c>null</c>: an explicit request to clear it.</summary>
    Null = 1,

    /// <summary>The field is on the wire with a non-null value.</summary>
    Present = 2,
}
