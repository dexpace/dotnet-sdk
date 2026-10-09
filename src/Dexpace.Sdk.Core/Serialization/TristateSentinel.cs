// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// A type-less marker for the Absent or Null state, implicitly convertible to <see cref="Tristate{T}"/> for every
/// <c>T</c> (P7a-7). It is what <see cref="Tristate.Absent"/> and <see cref="Tristate.Null"/> return, so
/// <c>patch with { Name = Tristate.Null }</c> compiles for any property type.
/// </summary>
/// <remarks>
/// This is design section 10 entry 21's "non-generic markers and implicit conversions": C# generics are invariant, so a
/// covariant <c>Tristate&lt;Never&gt;</c> is not available, and the marker serves the same ergonomics. There is no
/// Present sentinel: a Present state needs a value, which only <see cref="Tristate.Present{T}"/> can supply.
/// </remarks>
public readonly struct TristateSentinel : IEquatable<TristateSentinel>
{
    private readonly TristateState _kind;

    internal TristateSentinel(TristateState kind)
    {
        if (kind == TristateState.Present)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A sentinel stands for Absent or Null only.");
        }

        _kind = kind;
    }

    internal static TristateSentinel Absent => default;

    internal static TristateSentinel Null => new(TristateState.Null);

    internal TristateState Kind => _kind;

    /// <summary>Compares two sentinels by kind.</summary>
    /// <param name="left">The first sentinel.</param>
    /// <param name="right">The second sentinel.</param>
    /// <returns><see langword="true"/> when both are Absent or both are Null.</returns>
    public static bool operator ==(TristateSentinel left, TristateSentinel right) => left.Equals(right);

    /// <summary>Compares two sentinels by kind.</summary>
    /// <param name="left">The first sentinel.</param>
    /// <param name="right">The second sentinel.</param>
    /// <returns><see langword="true"/> when one is Absent and the other Null.</returns>
    public static bool operator !=(TristateSentinel left, TristateSentinel right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(TristateSentinel other) => _kind == other._kind;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TristateSentinel other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => (int)_kind;

    /// <summary>Renders the kind as <c>Absent</c> or <c>Null</c> (SERDE-30).</summary>
    /// <returns><c>Absent</c> or <c>Null</c>.</returns>
    public override string ToString() => _kind == TristateState.Null ? "Null" : "Absent";
}
