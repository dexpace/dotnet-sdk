// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Serialization;

/// <summary>
/// SERDE-14, SERDE-17, SERDE-18 and SERDE-30 (design position A, P7a-2, P7a-3, P7a-7): the three-state value type.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TristateTests
{
    private sealed record Dto(string Name);

    private sealed record Patch(Tristate<string> Name, Tristate<int> Size);

    private sealed class PropertyHolder
    {
        public Tristate<string> Name { get; init; }
    }

    private sealed class TypeNameVisitor : ITristateVisitor<Type>
    {
        public Type Visit<T>(Tristate<T> value)
            where T : notnull => typeof(T);
    }

    [Fact]
    public void Default_is_Absent()
    {
        // SERDE-17: no initializer anywhere, and the value is still Absent.
        Tristate<string> local = default;
        var patch = new Patch(default, default);
        var holder = new PropertyHolder();

        Assert.True(local.IsAbsent);
        Assert.Equal(TristateState.Absent, local.State);
        Assert.True(patch.Name.IsAbsent);
        Assert.True(patch.Size.IsAbsent);
        Assert.True(holder.Name.IsAbsent);
        Assert.Equal(Tristate<string>.Absent, local);
    }

    [Fact]
    public void Present_of_null_throws_ArgumentNullException()
    {
        // SERDE-14: the fourth state (Present holding null) cannot be built.
        var ex = Assert.Throws<ArgumentNullException>(() => Tristate<string>.Present(null!));

        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void The_Is_predicates_are_mutually_exclusive_per_state()
    {
        var states = new[] { Tristate<string>.Absent, Tristate<string>.Null, Tristate<string>.Present("a") };

        foreach (var state in states)
        {
            var truths = new[] { state.IsAbsent, state.IsNull, state.IsPresent }.Count(b => b);
            Assert.Equal(1, truths);
        }

        Assert.True(states[0].IsAbsent);
        Assert.True(states[1].IsNull);
        Assert.True(states[2].IsPresent);
        Assert.Equal(TristateState.Present, states[2].State);
    }

    [Fact]
    public void Present_holds_the_value()
    {
        var state = Tristate<string>.Present("a");

        Assert.Equal("a", state.Value);
    }

    [Fact]
    public void Value_throws_InvalidOperationException_naming_the_state()
    {
        var absent = Assert.Throws<InvalidOperationException>(() => Tristate<string>.Absent.Value);
        var @null = Assert.Throws<InvalidOperationException>(() => Tristate<string>.Null.Value);

        Assert.Contains("Absent", absent.Message, StringComparison.Ordinal);
        Assert.Contains("Null", @null.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetValue_is_true_only_for_Present()
    {
        Assert.True(Tristate<int>.Present(3).TryGetValue(out var value));
        Assert.Equal(3, value);

        Assert.False(Tristate<int>.Null.TryGetValue(out var fromNull));
        Assert.Equal(0, fromNull);
        Assert.False(Tristate<int>.Absent.TryGetValue(out var fromAbsent));
        Assert.Equal(0, fromAbsent);
    }

    [Fact]
    public void GetValueOrDefault_returns_default_for_Absent_and_Null_and_the_value_for_Present()
    {
        Assert.Null(Tristate<string>.Absent.GetValueOrDefault());
        Assert.Null(Tristate<string>.Null.GetValueOrDefault());
        Assert.Equal("a", Tristate<string>.Present("a").GetValueOrDefault());
        Assert.Equal(0, Tristate<int>.Null.GetValueOrDefault());
    }

    [Fact]
    public void GetValueOrDefault_with_a_fallback()
    {
        Assert.Equal("fb", Tristate<string>.Absent.GetValueOrDefault("fb"));
        Assert.Equal("fb", Tristate<string>.Null.GetValueOrDefault("fb"));
        Assert.Equal("a", Tristate<string>.Present("a").GetValueOrDefault("fb"));
    }

    [Fact]
    public void Match_invokes_exactly_one_arm_per_state()
    {
        AssertOneArm(Tristate<string>.Absent, expectedAbsent: 1, expectedNull: 0, expectedPresent: 0, "absent");
        AssertOneArm(Tristate<string>.Null, expectedAbsent: 0, expectedNull: 1, expectedPresent: 0, "null");
        AssertOneArm(Tristate<string>.Present("v"), expectedAbsent: 0, expectedNull: 0, expectedPresent: 1, "present:v");
    }

    [Fact]
    public void Match_validates_its_arms()
    {
        Assert.Throws<ArgumentNullException>(() => Tristate<string>.Absent.Match(null!, () => 0, _ => 0));
        Assert.Throws<ArgumentNullException>(() => Tristate<string>.Absent.Match(() => 0, null!, _ => 0));
        Assert.Throws<ArgumentNullException>(() => Tristate<string>.Absent.Match(() => 0, () => 0, null!));
    }

    [Fact]
    public void Implicit_from_a_value_is_Present()
    {
        Tristate<string> text = "a";
        Tristate<int> number = 3;

        Assert.Equal(Tristate<string>.Present("a"), text);
        Assert.Equal(Tristate<int>.Present(3), number);
    }

    [Fact]
    public void Implicit_from_null_is_Null_and_never_throws()
    {
        // P7a-2: an implicit conversion must not throw, and it can never yield Absent.
        string? nothing = null;
        Tristate<string> viaVariable = nothing;
        Tristate<string> viaLiteral = null;

        Assert.True(viaVariable.IsNull);
        Assert.True(viaLiteral.IsNull);
    }

    [Fact]
    public void FromNullable_never_returns_Absent()
    {
        Assert.True(Tristate<string>.FromNullable(null).IsNull);
        Assert.Equal(Tristate<string>.Present("a"), Tristate<string>.FromNullable("a"));
    }

    [Fact]
    public void Implicit_from_the_sentinels_converts_to_any_T()
    {
        // The use of design section 10 entry 21's non-generic markers: one spelling for every T.
        Tristate<string> absentText = Tristate.Absent;
        Tristate<int> nullNumber = Tristate.Null;
        Tristate<Dto> nullDto = Tristate.Null;
        Tristate<Dto> absentDto = Tristate.Absent;

        Assert.True(absentText.IsAbsent);
        Assert.True(nullNumber.IsNull);
        Assert.True(nullDto.IsNull);
        Assert.True(absentDto.IsAbsent);
    }

    [Fact]
    public void Equality_and_hash_are_by_state_and_value()
    {
        Assert.True(Tristate<int>.Absent == Tristate<int>.Absent);
        Assert.True(Tristate<int>.Null == Tristate<int>.Null);
        Assert.True(Tristate<int>.Present(1) == Tristate<int>.Present(1));
        Assert.True(Tristate<int>.Present(1) != Tristate<int>.Present(2));
        Assert.True(Tristate<int>.Absent != Tristate<int>.Null);
        Assert.False(Tristate<int>.Present(1).Equals(Tristate<int>.Null));

        Assert.False(Tristate<string>.Null.Equals((object)"Null"));
        Assert.False(Tristate<string>.Null.Equals((object?)null));
        Assert.True(Tristate<string>.Null.Equals((object)Tristate<string>.Null));
        Assert.False(Tristate<string>.Null.Equals((object)Tristate<int>.Null));

        Assert.Equal(Tristate<int>.Present(1).GetHashCode(), Tristate<int>.Present(1).GetHashCode());
        Assert.Equal(Tristate<string>.Present("a").GetHashCode(), Tristate<string>.Present("a").GetHashCode());
        Assert.NotEqual(Tristate<int>.Absent.GetHashCode(), Tristate<int>.Null.GetHashCode());
    }

    [Fact]
    public void ToString_is_Absent_Null_or_Present_of_the_value()
    {
        // SERDE-30: string equality, not a substring (a record struct would have printed its private fields).
        Assert.Equal("Absent", Tristate<int>.Absent.ToString());
        Assert.Equal("Null", Tristate<int>.Null.ToString());
        Assert.Equal("Present(5)", Tristate<int>.Present(5).ToString());
        Assert.Equal("Present(a)", Tristate<string>.Present("a").ToString());
    }

    [Fact]
    public void Switch_on_State_covers_every_named_state()
    {
        // The three named states are the whole domain; the discard arm is the compiler's CS8524 for an unnamed enum value.
        static string Describe(Tristate<string> value) => value.State switch
        {
            TristateState.Absent => "A",
            TristateState.Null => "N",
            TristateState.Present => "P",
            _ => throw new InvalidOperationException("Unreachable: Tristate has exactly three states."),
        };

        Assert.Equal("A", Describe(default));
        Assert.Equal("N", Describe(Tristate.Null));
        Assert.Equal("P", Describe("x"));
    }

    [Fact]
    public void Property_pattern_matching_reads_the_value()
    {
        Tristate<string> value = "x";

        Assert.True(value is { IsPresent: true, Value: var inner } && inner == "x");
    }

    [Fact]
    public void ITristate_Accept_dispatches_to_the_closed_type()
    {
        ITristate boxedInt = Tristate<int>.Present(3);
        ITristate boxedDefault = default(Tristate<string>);

        Assert.Equal(typeof(int), boxedInt.Accept(new TypeNameVisitor()));
        Assert.Equal(typeof(string), boxedDefault.Accept(new TypeNameVisitor()));
        Assert.Equal(TristateState.Absent, boxedDefault.State);
        Assert.Throws<ArgumentNullException>(() => boxedInt.Accept<Type>(null!));
    }

    [Fact]
    public void A_present_value_type_is_copied_not_boxed_into_state()
    {
        // The allocation-free argument of design section 3.4: Tristate<int> is a plain two-field struct.
        var size = Unsafe.SizeOf<Tristate<int>>();

        Assert.True(size <= 8, $"Tristate<int> is {size} bytes; the design expects an int plus a state enum.");
    }

    [Fact]
    public void Tristate_Absent_and_Null_convert_in_assignment_to_a_Tristate_of_T()
    {
        Tristate<Dto> dto = Tristate.Null;
        var patch = new Patch(Tristate.Present("a"), 3);
        var cleared = patch with { Name = Tristate.Null, Size = Tristate.Absent };

        Assert.True(dto.IsNull);
        Assert.True(cleared.Name.IsNull);
        Assert.True(cleared.Size.IsAbsent);
        Assert.Equal(Tristate<int>.Present(3), patch.Size);
    }

    [Fact]
    public void Tristate_Present_infers_T()
    {
        Tristate<string> text = Tristate.Present("a");
        Tristate<int> number = Tristate.Present(5);

        Assert.Equal("a", text.Value);
        Assert.Equal(5, number.Value);
        Assert.Throws<ArgumentNullException>(() => Tristate.Present<string>(null!));
    }

    [Fact]
    public void Tristate_FromNullable_for_a_nullable_value_type_maps_null_to_Null()
    {
        int? none = null;
        int? some = 3;

        Assert.True(Tristate.FromNullable(none).IsNull);
        Assert.Equal(Tristate<int>.Present(3), Tristate.FromNullable(some));
    }

    [Fact]
    public void Tristate_FromNullable_for_a_reference_type_maps_null_to_Null()
    {
        string? none = null;

        Assert.True(Tristate.FromNullable(none).IsNull);
        Assert.Equal(Tristate<string>.Present("a"), Tristate.FromNullable("a"));
    }

    [Fact]
    public void GetValueOrNull_returns_the_value_or_null_for_value_types()
    {
        Assert.Equal(3, Tristate<int>.Present(3).GetValueOrNull());
        Assert.Null(Tristate<int>.Null.GetValueOrNull());
        Assert.Null(Tristate<int>.Absent.GetValueOrNull());
    }

    [Fact]
    public void The_sentinel_property_types_are_TristateSentinel()
    {
        Assert.Equal(typeof(TristateSentinel), typeof(Tristate).GetProperty("Absent")!.PropertyType);
        Assert.Equal(typeof(TristateSentinel), typeof(Tristate).GetProperty("Null")!.PropertyType);
    }

    private static void AssertOneArm(
        Tristate<string> value, int expectedAbsent, int expectedNull, int expectedPresent, string expectedResult)
    {
        int absent = 0, @null = 0, present = 0;

        var result = value.Match(
            () =>
            {
                absent++;
                return "absent";
            },
            () =>
            {
                @null++;
                return "null";
            },
            v =>
            {
                present++;
                return "present:" + v;
            });

        Assert.Equal(expectedResult, result);
        Assert.Equal((expectedAbsent, expectedNull, expectedPresent), (absent, @null, present));
    }
}
