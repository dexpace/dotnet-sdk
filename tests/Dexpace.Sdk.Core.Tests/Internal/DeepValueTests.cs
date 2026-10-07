// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 packages/core/src/config/equality.test.ts. The cases that assert a JavaScript fact
// (undefined, DataView, typed arrays, bigint) are not ported (roadmap constraint 10).
#pragma warning disable CA1861, CA1825 // The constant arrays are the values under test: each is compared once, and an empty one is the case.
using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

// CFG-33, CFG-34; P5a-20, R5.
[Trait("Category", "Unit")]
public sealed class DeepValueTests
{
    private static readonly double s_otherNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000001);

    [Fact]
    public void Two_nulls_are_equal_and_null_is_not_equal_to_a_value()
    {
        Assert.True(DeepValue.Equals(null, null));
        Assert.False(DeepValue.Equals(null, 1));
        Assert.False(DeepValue.Equals("a", null));
    }

    [Fact]
    public void Null_hashes_to_zero()
    {
        Assert.Equal(0, DeepValue.GetHashCode(null));
    }

    [Fact]
    public void Primitives_compare_with_Equals()
    {
        Assert.True(DeepValue.Equals(1, 1));
        Assert.True(DeepValue.Equals("a", "a"));
        Assert.False(DeepValue.Equals(1, 1L));
        Assert.False(DeepValue.Equals("a", "b"));
    }

    [Fact]
    public void Arrays_compare_element_by_element()
    {
        Assert.True(DeepValue.Equals(new[] { 1, 2, 3 }, new[] { 1, 2, 3 }));
        Assert.False(DeepValue.Equals(new[] { 1, 2, 3 }, new[] { 1, 2, 4 }));
    }

    [Fact]
    public void Arrays_of_different_lengths_are_unequal()
    {
        Assert.False(DeepValue.Equals(new[] { 1, 2 }, new[] { 1, 2, 3 }));
    }

    [Fact]
    public void Two_empty_arrays_are_equal()
    {
        Assert.True(DeepValue.Equals(Array.Empty<int>(), new int[0]));
    }

    [Fact]
    public void Nested_object_arrays_recurse()
    {
        object a = new object[] { 1, new object[] { "a", 2.0 } };
        object b = new object[] { 1, new object[] { "a", 2.0 } };
        object c = new object[] { 1, new object[] { "a", 3.0 } };

        Assert.True(DeepValue.Equals(a, b));
        Assert.False(DeepValue.Equals(a, c));
    }

    [Fact]
    public void Multi_dimensional_arrays_compare_structurally()
    {
        var a = new int[2, 2] { { 1, 2 }, { 3, 4 } };
        var b = new int[2, 2] { { 1, 2 }, { 3, 4 } };
        var c = new int[2, 2] { { 1, 2 }, { 3, 5 } };
        var d = new int[1, 4] { { 1, 2, 3, 4 } };

        Assert.True(DeepValue.Equals(a, b));
        Assert.False(DeepValue.Equals(a, c));
        Assert.False(DeepValue.Equals(a, d));
        Assert.False(DeepValue.Equals(a, new[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void Arrays_of_different_runtime_types_are_unequal()
    {
        Assert.False(DeepValue.Equals(new object[] { 1.0 }, new[] { 1.0 }));
        Assert.False(DeepValue.Equals(new object[] { "a" }, new[] { "a" }));
        Assert.False(DeepValue.Equals(new[] { 1 }, new long[] { 1 }));
    }

    [Fact]
    public void A_non_array_falls_back_to_object_Equals()
    {
        // The bit rule belongs to array elements only: a top-level boxed 0.0 and -0.0 are equal, as object.Equals says.
        Assert.True(DeepValue.Equals(0.0, -0.0));
        Assert.True(DeepValue.Equals(double.NaN, s_otherNaN));
    }

    [Fact]
    public void NaN_equals_NaN_inside_an_array_and_as_boxed_elements()
    {
        Assert.True(DeepValue.Equals(new[] { double.NaN }, new[] { s_otherNaN }));
        Assert.True(DeepValue.Equals(new[] { float.NaN }, new[] { BitConverter.Int32BitsToSingle(0x7FC00001) }));
        Assert.True(DeepValue.Equals(new object[] { double.NaN }, new object[] { s_otherNaN }));
    }

    [Fact]
    public void Positive_zero_differs_from_negative_zero_inside_an_array_and_as_boxed_elements()
    {
        // 0.0.Equals(-0.0) is true (fact 5); the element rule compares bit patterns.
        Assert.False(DeepValue.Equals(new[] { 0.0 }, new[] { -0.0 }));
        Assert.False(DeepValue.Equals(new[] { 0.0f }, new[] { -0.0f }));
        Assert.False(DeepValue.Equals(new object[] { 0.0 }, new object[] { -0.0 }));
    }

    [Fact]
    public void Hashing_mirrors_equality()
    {
        Assert.Equal(DeepValue.GetHashCode(new[] { 1, 2 }), DeepValue.GetHashCode(new[] { 1, 2 }));
        Assert.Equal(DeepValue.GetHashCode(new[] { double.NaN }), DeepValue.GetHashCode(new[] { s_otherNaN }));
        Assert.Equal(DeepValue.GetHashCode(new object[] { 1, new object[] { "a" } }), DeepValue.GetHashCode(new object[] { 1, new object[] { "a" } }));
        Assert.NotEqual(DeepValue.GetHashCode(new[] { 0.0 }), DeepValue.GetHashCode(new[] { -0.0 }));
        Assert.Equal(DeepValue.GetHashCode("abc"), "abc".GetHashCode(StringComparison.Ordinal));
        Assert.Equal(DeepValue.GetHashCode(0.0), DeepValue.GetHashCode(-0.0));
    }

    [Fact]
    public void Hash_distinguishes_element_order_and_booleans_and_lengths()
    {
        Assert.NotEqual(DeepValue.GetHashCode(new[] { 1, 2 }), DeepValue.GetHashCode(new[] { 2, 1 }));
        Assert.NotEqual(DeepValue.GetHashCode(new[] { true }), DeepValue.GetHashCode(new[] { false }));
        Assert.NotEqual(DeepValue.GetHashCode(new[] { 1 }), DeepValue.GetHashCode(new[] { 1, 1 }));
    }

    [Fact]
    public void Equality_is_reflexive_symmetric_and_hash_consistent_over_random_nested_arrays()
    {
        var random = new Random(5142);
        for (var i = 0; i < 2_000; i++)
        {
            var seed = random.Next();
            var a = Generate(new Random(seed), 0);
            var b = Generate(new Random(seed), 0);
            var other = Generate(new Random(seed + 1), 0);

            Assert.True(DeepValue.Equals(a, a));
            Assert.True(DeepValue.Equals(a, b));
            Assert.True(DeepValue.Equals(b, a));
            Assert.Equal(DeepValue.GetHashCode(a), DeepValue.GetHashCode(b));
            Assert.Equal(DeepValue.Equals(a, other), DeepValue.Equals(other, a));
            if (DeepValue.Equals(a, other))
            {
                Assert.Equal(DeepValue.GetHashCode(a), DeepValue.GetHashCode(other));
            }
        }
    }

    [Fact]
    public void A_self_referential_array_throws_InvalidOperationException_rather_than_overflowing_the_stack()
    {
        var a = new object[1];
        a[0] = a;
        var b = new object[1];
        b[0] = b;

        var equals = Assert.Throws<InvalidOperationException>(() => DeepValue.Equals(a, b));
        var hash = Assert.Throws<InvalidOperationException>(() => DeepValue.GetHashCode(a));

        Assert.Contains("128", equals.Message, StringComparison.Ordinal);
        Assert.Contains("128", hash.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nesting_past_the_bound_throws_and_nesting_within_it_does_not()
    {
        Assert.True(DeepValue.Equals(Nest(100), Nest(100)));
        _ = DeepValue.GetHashCode(Nest(100));
        Assert.Throws<InvalidOperationException>(() => DeepValue.Equals(Nest(200), Nest(200)));
        Assert.Throws<InvalidOperationException>(() => DeepValue.GetHashCode(Nest(200)));
    }

    private static object Nest(int depth)
    {
        object current = "leaf";
        for (var i = 0; i < depth; i++)
        {
            current = new object[] { current };
        }

        return current;
    }

    private static object? Generate(Random random, int depth)
    {
        var kind = random.Next(depth >= 4 ? 7 : 8);
        switch (kind)
        {
            case 0:
                return random.Next(3);
            case 1:
                return "s" + random.Next(3);
            case 2:
                return new[] { 0.0, -0.0, double.NaN, 1.5 }[random.Next(4)];
            case 3:
                return random.Next(2) == 0;
            case 4:
                return null;
            case 5:
                return new double[] { random.Next(2), double.NaN, -0.0 }.Take(random.Next(4)).ToArray();
            case 6:
                return Enumerable.Range(0, random.Next(4)).Select(_ => random.Next(3)).ToArray();
            default:
                return Enumerable.Range(0, random.Next(4)).Select(_ => Generate(random, depth + 1)).ToArray();
        }
    }
}
