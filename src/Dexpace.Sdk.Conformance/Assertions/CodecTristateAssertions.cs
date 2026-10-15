// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@54aeed4 tests/conformance/ (the tri-state codec cases) and ruby-sdk@5b17395
// gems/dexpace-conformance/lib/dexpace/conformance/codec_suite/ (tri-state). The suite judges what a codec preserves, through the codec's own
// round trip, not how it spells it: Absent must not encode like Null (and must be shorter, because it omits the key), and each state must come back.
// SERDE-14, SERDE-18 and SERDE-30 are Tristate<T>'s own API in core, with no bypass to break: they stay by reference to TristateTests and
// TristateSentinelTests (validation F6).
using Dexpace.Sdk.Conformance.Codec;
using Dexpace.Sdk.Core.Serialization;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// <c>SERDE-15</c> to <c>SERDE-17</c>, <c>SERDE-19</c> and <c>SERDE-20</c>: a PATCH tri-state keeps Absent, Null and Present apart on the wire
/// and back, a missing key means Absent, the default configuration wires all of it, and a value with no key to omit degrades to a wire null.
/// </summary>
internal static class CodecTristateAssertions
{
    private static readonly ProbeAddress s_address = new("1 Main Street", "Springfield");

    private static ProbePatch AllAbsent => new(default, default, default);

    private static ProbePatch AllNull => new(Tristate<string>.Null, Tristate<int>.Null, Tristate<ProbeAddress>.Null);

    private static ProbePatch Mixed => new(Tristate<string>.Present("Ada"), Tristate<int>.Null, Tristate<ProbeAddress>.Present(s_address));

    internal static ConformanceAssertion[] Assertions() =>
    [
        CodecAssertionBuilder.Define("serde-15.tristate-emission", ["SERDE-15"], RequirementLevel.Must, CodecAssertionBuilder.Both, EmissionAsync),
        CodecAssertionBuilder.Define("serde-16.tristate-decode", ["SERDE-16"], RequirementLevel.Must, CodecAssertionBuilder.Both, DecodeAsync),
        CodecAssertionBuilder.Define("serde-17.missing-key-absent", ["SERDE-17"], RequirementLevel.Must, CodecAssertionBuilder.Both, MissingKeyAbsentAsync),
        CodecAssertionBuilder.Define("serde-19.default-wiring", ["SERDE-19"], RequirementLevel.Must, CodecAssertionBuilder.Both, DefaultWiringAsync),
        CodecAssertionBuilder.Define("serde-20.top-level-degrades", ["SERDE-20"], RequirementLevel.Should, CodecAssertionBuilder.Both, TopLevelDegradesAsync),
    ];

    // Absent omits the key, Null writes it with a wire null, Present writes it with the value: three different encodings, the first the shortest.
    private static async Task EmissionAsync(CodecContext context, CancellationToken token)
    {
        var absent = await Check.GuardAsync(() => context.EncodeAsync(AllAbsent, token), "encoding a PATCH with every member absent", token).ConfigureAwait(false);
        var nulls = await Check.GuardAsync(() => context.EncodeAsync(AllNull, token), "encoding a PATCH with every member null", token).ConfigureAwait(false);
        var present = await Check.GuardAsync(() => context.EncodeAsync(new ProbePatch(Tristate<string>.Present("Ada"), Tristate<int>.Present(36), Tristate<ProbeAddress>.Present(s_address)), token), "encoding a PATCH with every member present", token).ConfigureAwait(false);

        Check.True(!absent.AsSpan().SequenceEqual(nulls), "Absent and Null must not encode alike: a PATCH would be silently corrupted", "different encodings", "identical bytes");
        Check.True(absent.Length < nulls.Length, "Absent must omit the key entirely, so its encoding is shorter than one that writes the key with a null", $"fewer than {nulls.Length} bytes", $"{absent.Length} bytes");
        Check.True(!nulls.AsSpan().SequenceEqual(present) && present.Length > nulls.Length, "Present must write the key with the encoded inner value", $"more than {nulls.Length} bytes", $"{present.Length} bytes");
    }

    // Each state decodes back to itself, the inner value keeping its declared element type.
    private static async Task DecodeAsync(CodecContext context, CancellationToken token)
    {
        var back = await context.RoundTripAsync(Mixed, "a PATCH (present string, null number, present record)", token).ConfigureAwait(false);
        Check.True(back is not null, "a PATCH must decode", "a value", "null");
        Check.True(back!.Name.IsPresent && back.Name.Value == "Ada", "a present string member must decode to Present with the inner value", "Present(Ada)", back.Name.ToString());
        Check.True(back.Age.IsNull, "an explicit null member must decode to Null", "Null", back.Age.ToString());
        Check.True(back.Address.IsPresent && back.Address.Value == s_address, "a present record member must decode to Present with the inner record", "Present(address)", back.Address.ToString());

        var second = await context.RoundTripAsync(new ProbePatch(Tristate<string>.Null, Tristate<int>.Present(0), default), "a PATCH (null string, present zero, absent record)", token).ConfigureAwait(false);
        Check.True(second is not null && second.Name.IsNull, "an explicit null member must decode to Null", "Null", second?.Name.ToString() ?? "null");
        Check.True(second!.Age.IsPresent && second.Age.Value == 0, "a present value-type member must decode to Present with its declared element type preserved", "Present(0)", second.Age.ToString());
        Check.True(second.Address.IsAbsent, "an absent member must decode to Absent", "Absent", second.Address.ToString());
    }

    // A document with no key for a member means Absent: the codec's own encoding of an all-absent PATCH has no keys.
    private static async Task MissingKeyAbsentAsync(CodecContext context, CancellationToken token)
    {
        var back = await context.RoundTripAsync(AllAbsent, "a PATCH with every member absent (a document with no keys)", token).ConfigureAwait(false);

        Check.True(back is not null, "a document with no keys must decode to a value", "a value", "null");
        Check.True(
            back!.Name.IsAbsent && back.Age.IsAbsent && back.Address.IsAbsent,
            "a member with no key on the wire must resolve to Absent (its declared default), not to Null",
            "Absent, Absent, Absent",
            $"{back.Name}, {back.Age}, {back.Address}");
    }

    // The codec as the factory built it, with no extra configuration, keeps a mixed PATCH intact.
    private static async Task DefaultWiringAsync(CodecContext context, CancellationToken token)
    {
        var back = await context.RoundTripAsync(Mixed, "a mixed PATCH through the codec's default configuration", token).ConfigureAwait(false);

        Check.True(
            back is not null && back.Name == Mixed.Name && back.Age == Mixed.Age && back.Address == Mixed.Address,
            "the default codec configuration must wire the tri-state semantics: Absent, Null and Present must all survive, or a PATCH is silently corrupted",
            "the PATCH unchanged",
            back is null ? "null" : $"{back.Name}, {back.Age}, {back.Address}");
    }

    // With no enclosing object to omit a key, a top-level value or an array element degrades to the wire null and never throws.
    private static async Task TopLevelDegradesAsync(CodecContext context, CancellationToken token)
    {
        var absent = await Check.GuardAsync(() => context.EncodeAsync(Tristate<string>.Absent, token), "encoding a top-level Absent", token).ConfigureAwait(false);
        var nulls = await Check.GuardAsync(() => context.EncodeAsync(Tristate<string>.Null, token), "encoding a top-level Null", token).ConfigureAwait(false);
        Check.True(absent.AsSpan().SequenceEqual(nulls), "a top-level Absent has no key to omit, so it must encode as the wire null, like Null", "identical bytes", "different encodings");

        var sequence = new ProbeSequence([Tristate<string>.Present("x"), Tristate<string>.Absent, Tristate<string>.Null, Tristate<string>.Present("y")]);
        var back = await context.RoundTripAsync(sequence, "an array of tri-state elements, one absent", token).ConfigureAwait(false);
        Check.True(back is { Items.Length: 4 }, "an absent array element must degrade to null without shifting the indices", "4 elements", back is null ? "null" : back.Items.Length + " elements");
        Check.True(back!.Items[0].IsPresent && back.Items[0].Value == "x" && back.Items[3].IsPresent && back.Items[3].Value == "y", "the present elements must keep their indices", "x at 0 and y at 3", $"{back.Items[0]} at 0 and {back.Items[3]} at 3");
        Check.True(!back.Items[1].IsPresent && !back.Items[2].IsPresent, "the absent and null elements must not decode as present", "no value", $"{back.Items[1]} and {back.Items[2]}");
    }
}
