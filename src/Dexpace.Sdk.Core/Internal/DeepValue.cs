// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Internal;

/// <summary>
/// Structural equality and hashing for values that may be arrays (CFG-33, CFG-34; design §8.2, P5a-20).
/// </summary>
/// <remarks>
/// <para>
/// Two nulls are equal and <see langword="null"/> hashes to zero. Two arrays are equal when their runtime array types are
/// identical, their ranks and dimension lengths match and their elements are deep-equal (object arrays recurse, so nested
/// and multi-dimensional arrays compare structurally); <c>object[] { 1.0 }</c> and <c>double[] { 1.0 }</c> differ. Within
/// an array, <see cref="double"/> and <see cref="float"/> elements, boxed or not, compare by bit pattern after every NaN
/// is canonicalised, so NaN equals NaN and <c>+0.0</c> differs from <c>-0.0</c> (CFG-34); <c>0.0.Equals(-0.0)</c> is
/// <see langword="true"/>, so the rule applies to elements only, and a top-level non-array falls back to
/// <see cref="object.Equals(object, object)"/> (CFG-33). Hashing mirrors each rule.
/// </para>
/// <para>
/// <b>Recursion bound (R5).</b> A self-referential array would overflow the stack, which ends a .NET process and cannot be
/// caught, so both operations count nesting depth and throw <see cref="InvalidOperationException"/> past
/// <see cref="MaxDepth"/>, naming the bound and never the content.
/// </para>
/// <para>
/// It ships now because CFG-33 and CFG-34 are 5a's MUSTs and a helper is what they require; no as-built model carries a
/// floating-point array, so it is internal. A public promotion is phase 7a's call (P5a-20).
/// </para>
/// </remarks>
internal static class DeepValue
{
    internal const int MaxDepth = 128;

    private const long CanonicalNaNBits = 0x7FF8000000000000;
    private const int CanonicalSingleNaNBits = 0x7FC00000;

    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> are structurally equal.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    /// <exception cref="InvalidOperationException">The nesting is deeper than <see cref="MaxDepth"/>.</exception>
    internal static new bool Equals(object? a, object? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a is Array left && b is Array right ? ArraysEqual(left, right, 1) : object.Equals(a, b);
    }

    /// <summary>A hash code consistent with <see cref="Equals(object?, object?)"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The hash code; zero for <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">The nesting is deeper than <see cref="MaxDepth"/>.</exception>
    internal static int GetHashCode(object? value) =>
        value switch
        {
            null => 0,
            Array array => ArrayHash(array, 1),
            _ => value.GetHashCode(),
        };

    private static bool ArraysEqual(Array a, Array b, int depth)
    {
        RequireDepth(depth);
        if (a.GetType() != b.GetType() || a.Rank != b.Rank)
        {
            return false;
        }

        for (var dimension = 0; dimension < a.Rank; dimension++)
        {
            if (a.GetLength(dimension) != b.GetLength(dimension))
            {
                return false;
            }
        }

        var left = a.GetEnumerator();
        var right = b.GetEnumerator();
        while (left.MoveNext() && right.MoveNext())
        {
            if (!ElementsEqual(left.Current, right.Current, depth))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ElementsEqual(object? x, object? y, int depth)
    {
        if (x is null || y is null)
        {
            return x is null && y is null;
        }

        return (x, y) switch
        {
            (double dx, double dy) => DoubleBits(dx) == DoubleBits(dy),
            (float fx, float fy) => SingleBits(fx) == SingleBits(fy),
            (Array ax, Array ay) => ReferenceEquals(ax, ay) || ArraysEqual(ax, ay, depth + 1),
            _ => object.Equals(x, y),
        };
    }

    private static int ArrayHash(Array array, int depth)
    {
        RequireDepth(depth);
        var hash = new HashCode();
        hash.Add(array.Rank);
        for (var dimension = 0; dimension < array.Rank; dimension++)
        {
            hash.Add(array.GetLength(dimension));
        }

        foreach (var element in array)
        {
            hash.Add(ElementHash(element, depth));
        }

        return hash.ToHashCode();
    }

    private static int ElementHash(object? element, int depth) =>
        element switch
        {
            null => 0,
            double d => DoubleBits(d).GetHashCode(),
            float f => SingleBits(f).GetHashCode(),
            Array nested => ArrayHash(nested, depth + 1),
            _ => element.GetHashCode(),
        };

    private static long DoubleBits(double value) =>
        double.IsNaN(value) ? CanonicalNaNBits : BitConverter.DoubleToInt64Bits(value);

    private static int SingleBits(float value) =>
        float.IsNaN(value) ? CanonicalSingleNaNBits : BitConverter.SingleToInt32Bits(value);

    private static void RequireDepth(int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException(
                $"The value is nested deeper than {MaxDepth} arrays; a self-referential array cannot be compared or hashed.");
        }
    }
}
