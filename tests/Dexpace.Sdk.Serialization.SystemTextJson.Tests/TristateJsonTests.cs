// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

/// <summary>
/// SERDE-15, SERDE-16, SERDE-17, SERDE-19 and SERDE-20 (design position B, P7a-4, P7a-5): the Tristate wire behaviour of
/// <see cref="SystemTextJsonSerde"/>, over a default-generation-mode context (design fact 8). Ported from the Node codec's
/// <c>tristate-replacer.test.ts</c> and <c>tristate-schema.test.ts</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TristateJsonTests
{
    private static SystemTextJsonSerde Serde() =>
        new(SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default));

    private static string Encode<T>(SystemTextJsonSerde serde, T value)
    {
        var writer = new ArrayBufferWriter<byte>();
        serde.Serialize(writer, value);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    private static T? Decode<T>(SystemTextJsonSerde serde, string json) => serde.Deserialize<T>(Encoding.UTF8.GetBytes(json));

    private static string Encode<T>(T value) => Encode(Serde(), value);

    private static T? Decode<T>(string json) => Decode<T>(Serde(), json);

    // ---- encode: SERDE-15, SERDE-19 -------------------------------------------------------------------------------

    [Fact]
    public void Absent_omits_the_key()
    {
        Assert.Equal("{}", Encode(new WidgetPatch(default, default, default)));
    }

    [Fact]
    public void Null_emits_json_null()
    {
        Assert.Equal("""{"name":null}""", Encode(new WidgetPatch(Tristate.Null, default, default)));
    }

    [Fact]
    public void Present_emits_the_value()
    {
        Assert.Equal("""{"name":"a","size":3}""", Encode(new WidgetPatch("a", 3, default)));
    }

    [Fact]
    public void The_three_states_in_one_document()
    {
        // The AOT smoke's expected bytes: Null written, Present written, Absent omitted, camelCase from the defaults.
        Assert.Equal("""{"name":null,"size":3}""", Encode(new WidgetPatch(Tristate.Null, Tristate.Present(3), default)));
    }

    [Fact]
    public void A_present_object_encodes_the_object_not_the_wrapper()
    {
        var json = Encode(new NestedPatch { Inner = new Widget("g", 1) });

        Assert.Equal("""{"inner":{"name":"g","size":1}}""", json);
        Assert.DoesNotContain("state", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_present_list_encodes_as_an_array()
    {
        var json = Encode(new NestedPatch { Many = new List<Widget> { new("a", 1), new("b", 2) } });

        Assert.Equal("""{"many":[{"name":"a","size":1},{"name":"b","size":2}]}""", json);
    }

    [Fact]
    public void A_nested_Tristate_inside_a_present_dto_is_still_rewritten()
    {
        var inner = new WidgetPatch("n", default, Tristate.Null);

        var json = Encode(new Holder(new WidgetPatch(default, 1, default), Tristate.Present(inner)));

        Assert.Equal("""{"patch":{"size":1},"nestedTristate":{"name":"n","note":null}}""", json);
    }

    [Fact]
    public void A_property_modifier_from_the_caller_is_composed_not_replaced()
    {
        // P7a-4: the caller's ShouldSerialize survives; the serde's omit-Absent predicate is ANDed with it.
        var resolver = TristateTestContext.Default.WithAddedModifier(typeInfo =>
        {
            foreach (var property in typeInfo.Properties.Where(p => p.PropertyType == typeof(Tristate<int>)))
            {
                property.ShouldSerialize = static (_, value) => value is not Tristate<int> { IsPresent: true, Value: 7 };
            }
        });
        var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(resolver));

        Assert.Equal("{}", Encode(serde, new WidgetPatch(default, default, default)));
        Assert.Equal("{}", Encode(serde, new WidgetPatch(default, 7, default)));
        Assert.Equal("""{"size":8}""", Encode(serde, new WidgetPatch(default, 8, default)));
    }

    [Fact]
    public void Tristate_wiring_is_on_by_default_for_a_bare_options_constructor()
    {
        // SERDE-19: no AddTristateSupport call by the caller, and Absent is still omitted.
        var serde = new SystemTextJsonSerde(new JsonSerializerOptions { TypeInfoResolver = TristateTestContext.Default });

        Assert.Equal("""{"Name":null}""", Encode(serde, new WidgetPatch(Tristate.Null, default, default)));
    }

    [Fact]
    public void The_context_constructor_is_Tristate_wired_too()
    {
        Assert.Equal("{}", Encode(new SystemTextJsonSerde(TristateTestContext.Default), new WidgetPatch(default, default, default)));
    }

    [Fact]
    public void A_caller_registered_Tristate_converter_wins()
    {
        // P7a-5: a caller's own converter precedes ours in Converters; the Absent omission still applies on top.
        var options = new JsonSerializerOptions { TypeInfoResolver = TristateTestContext.Default };
        options.Converters.Add(new CallerStringTristateConverter());
        var serde = new SystemTextJsonSerde(options);

        var json = Encode(serde, new WidgetPatch("a", default, Tristate.Null));

        Assert.Equal("""{"Name":"caller:a","Note":null}""", json);
    }

    [Fact]
    public void A_property_named_with_an_empty_string_is_handled()
    {
        // Node's case: a Tristate property whose JSON name is "".
        Assert.Equal("{}", Encode(new EmptyName()));
        Assert.Equal("""{"":"x"}""", Encode(new EmptyName { Weird = "x" }));
        Assert.Equal("""{"":null}""", Encode(new EmptyName { Weird = Tristate.Null }));
        Assert.Equal("x", Decode<EmptyName>("""{"":"x"}""")!.Weird.Value);
    }

    // ---- decode: SERDE-16, SERDE-17 -------------------------------------------------------------------------------

    [Fact]
    public void Missing_null_and_value_decode_to_Absent_Null_Present_for_string()
    {
        Assert.True(Decode<WidgetPatch>("{}")!.Name.IsAbsent);
        Assert.True(Decode<WidgetPatch>("""{"name":null}""")!.Name.IsNull);
        Assert.Equal(Tristate.Present("v"), Decode<WidgetPatch>("""{"name":"v"}""")!.Name);
    }

    [Fact]
    public void Missing_null_and_value_decode_to_Absent_Null_Present_for_a_value_type()
    {
        // The AOT hazard: Tristate<int> is the value-type instantiation.
        Assert.True(Decode<WidgetPatch>("{}")!.Size.IsAbsent);
        Assert.True(Decode<WidgetPatch>("""{"size":null}""")!.Size.IsNull);
        Assert.Equal(Tristate.Present(3), Decode<WidgetPatch>("""{"size":3}""")!.Size);
    }

    [Fact]
    public void Missing_null_and_value_decode_to_Absent_Null_Present_for_an_object_with_its_type_preserved()
    {
        Assert.True(Decode<NestedPatch>("{}")!.Inner.IsAbsent);
        Assert.True(Decode<NestedPatch>("""{"inner":null}""")!.Inner.IsNull);

        var present = Decode<NestedPatch>("""{"inner":{"name":"g","size":1}}""")!.Inner;

        Assert.True(present.IsPresent);
        Assert.IsType<Widget>(present.Value);
        Assert.Equal(new Widget("g", 1), present.Value);
    }

    [Fact]
    public void Missing_null_and_value_decode_to_Absent_Null_Present_for_a_list_with_its_type_preserved()
    {
        Assert.True(Decode<NestedPatch>("{}")!.Many.IsAbsent);
        Assert.True(Decode<NestedPatch>("""{"many":null}""")!.Many.IsNull);

        var present = Decode<NestedPatch>("""{"many":[{"name":"a","size":1},{"name":"b","size":2}]}""")!.Many;

        Assert.True(present.IsPresent);
        Assert.IsType<List<Widget>>(present.Value);
        Assert.Equal(2, present.Value.Count);
        Assert.Equal(new Widget("b", 2), present.Value[1]);
    }

    [Fact]
    public void Mixed_document_decodes_each_property_independently()
    {
        // Design fact 7: a missing key never reaches the converter.
        var shaped = Decode<Shaped>("""{"B":null,"C":"v"}""")!;

        Assert.True(shaped.A.IsAbsent);
        Assert.True(shaped.B.IsNull);
        Assert.Equal(Tristate.Present("v"), shaped.C);
    }

    [Fact]
    public void A_positional_record_and_a_class_with_init_properties_both_default_to_Absent()
    {
        Assert.True(Decode<Shaped>("{}")!.A.IsAbsent);
        Assert.True(Decode<NestedPatch>("{}")!.Inner.IsAbsent);
        Assert.True(new NestedPatch().Inner.IsAbsent);
    }

    [Fact]
    public void A_value_of_the_wrong_type_is_a_DeserializationException()
    {
        // SERDE-9: the Tristate converter's failures are wrapped like any other.
        var ex = Assert.Throws<DeserializationException>(() => Decode<WidgetPatch>("""{"size":"x"}"""));

        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    [Fact]
    public void A_non_null_token_whose_inner_result_is_null_is_a_JsonException()
    {
        var options = SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default);
        options.Converters.Insert(0, new NullReturningQuirkyConverter());
        var serde = new SystemTextJsonSerde(options);

        var ex = Assert.Throws<DeserializationException>(() => Decode<QuirkHolder>(serde, """{"q":5}"""));

        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
        Assert.Contains("Quirky", ex.InnerException!.Message, StringComparison.Ordinal);
    }

    // ---- positions with no key: SERDE-20 ---------------------------------------------------------------------------

    [Fact]
    public void Top_level_Absent_and_Null_both_write_null()
    {
        Assert.Equal("null", Encode(default(Tristate<string>)));
        Assert.Equal("null", Encode(Tristate<string>.Null));
        Assert.Equal("\"a\"", Encode(Tristate<string>.Present("a")));
    }

    [Fact]
    public void A_top_level_null_decodes_to_Null()
    {
        // Design fact 9: the converter sees the null token at the root.
        Assert.True(Decode<Tristate<string>>("null").IsNull);
        Assert.True(Decode<Tristate<int>>("null").IsNull);
        Assert.Equal(Tristate.Present(4), Decode<Tristate<int>>("4"));
    }

    [Fact]
    public void An_array_element_Absent_writes_null_and_indices_do_not_shift()
    {
        var json = Encode(new ArrayHolder(["a", default, Tristate.Null, "d"]));

        Assert.Equal("""{"items":["a",null,null,"d"]}""", json);
        var decoded = Decode<ArrayHolder>(json)!;
        Assert.Equal(4, decoded.Items.Length);
        Assert.True(decoded.Items[1].IsNull);
    }

    [Fact]
    public void A_dictionary_value_Absent_writes_null()
    {
        // The documented compromise: STJ has no per-entry ShouldSerialize.
        var json = Encode(new DictHolder(new Dictionary<string, Tristate<string>> { ["a"] = default, ["b"] = "x" }));

        Assert.Equal("""{"map":{"a":null,"b":"x"}}""", json);
    }

    // ---- AddTristateSupport: P7a-4 --------------------------------------------------------------------------------

    [Fact]
    public void AddTristateSupport_without_a_resolver_throws_ArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new JsonSerializerOptions().AddTristateSupport());

        Assert.Contains("TypeInfoResolver", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AddTristateSupport", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddTristateSupport_rejects_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => TristateJsonSerializerOptionsExtensions.AddTristateSupport(null!));
    }

    [Fact]
    public void AddTristateSupport_is_idempotent_in_effect()
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = TristateTestContext.Default };
        options.AddTristateSupport();
        var convertersAfterOne = options.Converters.Count;
        options.AddTristateSupport();

        Assert.Equal(convertersAfterOne, options.Converters.Count);
        Assert.Equal(1, options.Converters.Count(c => c.GetType().Name == "TristateConverterFactory"));
        var json = JsonSerializer.Serialize(
            new WidgetPatch(Tristate.Null, default, default), (JsonTypeInfo<WidgetPatch>)options.GetTypeInfo(typeof(WidgetPatch)));
        Assert.Equal("""{"Name":null}""", json);
    }

    [Fact]
    public void AddTristateSupport_makes_plain_JsonSerializer_omit_Absent()
    {
        // The direct-JsonSerializer caller of the design (an ASP.NET endpoint, a test) is protected too.
        var options = new JsonSerializerOptions { TypeInfoResolver = TristateTestContext.Default };
        options.AddTristateSupport();

        var json = JsonSerializer.Serialize(
            new WidgetPatch(default, 3, default), (JsonTypeInfo<WidgetPatch>)options.GetTypeInfo(typeof(WidgetPatch)));

        Assert.Equal("""{"Size":3}""", json);
    }

    [Fact]
    public void Without_AddTristateSupport_an_Absent_or_Null_field_throws_and_a_Present_field_is_written_as_an_object()
    {
        // What the AddTristateSupport remarks and serde.md say: unwired, System.Text.Json reads Tristate<T>.Value, whose getter
        // throws for Absent and Null (the failure is loud, not a silent object), and a Present field is written as an object of
        // the struct's public properties rather than as its value.
        var options = new JsonSerializerOptions { TypeInfoResolver = TristateTestContext.Default };
        var info = (JsonTypeInfo<WidgetPatch>)options.GetTypeInfo(typeof(WidgetPatch));

        var absent = Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(new WidgetPatch(default, 3, "n"), info));
        var @null = Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(new WidgetPatch(Tristate.Null, 3, "n"), info));
        var present = JsonSerializer.Serialize(new WidgetPatch("a", 3, "n"), info);

        Assert.Contains("Absent", absent.Message, StringComparison.Ordinal);
        Assert.Contains("Null", @null.Message, StringComparison.Ordinal);
        Assert.StartsWith("""{"Name":{""", present, StringComparison.Ordinal);
        Assert.Contains("\"Value\":\"a\"", present, StringComparison.Ordinal);
        Assert.NotEqual("""{"Name":"a","Size":3,"Note":"n"}""", present);
    }

    [Fact]
    public void AddTristateSupport_on_read_only_options_throws_InvalidOperationException()
    {
        // R10: the extension mutates; frozen options reject it, and the serde never hits this (it wires its private copy).
        var options = new JsonSerializerOptions { TypeInfoResolver = TristateTestContext.Default };
        options.MakeReadOnly();

        Assert.Throws<InvalidOperationException>(options.AddTristateSupport);
    }

    // ---- generation modes: design fact 8 / R1 ---------------------------------------------------------------------

    [Fact]
    public void Absent_is_omitted_under_the_default_mode_context()
    {
        // The headline test: if a default-mode source-generated fast path ignored ShouldSerialize, Absent would
        // serialise as null and a PATCH would silently clear fields.
        var serde = new SystemTextJsonSerde(TristateTestContext.Default);

        Assert.Equal("{}", Encode(serde, new WidgetPatch(default, default, default)));
    }

    [Fact]
    public void Absent_is_omitted_under_a_metadata_mode_context()
    {
        var serde = new SystemTextJsonSerde(TristateMetadataContext.Default);

        Assert.Equal("""{"Size":3}""", Encode(serde, new WidgetPatch(default, 3, default)));
    }

    [Fact]
    public void A_fast_path_only_context_cannot_carry_Tristate_models()
    {
        // Documented limitation (design R1, task 0.1): a context generated with GenerationMode.Serialization carries no
        // property metadata, so any options other than its own (the serde's private copy) cannot serialise through it.
        var serde = new SystemTextJsonSerde(TristateFastPathContext.Default);

        var ex = Assert.Throws<SerializationException>(() => Encode(serde, new WidgetPatch(default, 3, default)));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    // ---- the mandated round-trip property test: P7a-24 -------------------------------------------------------------

    [Fact]
    public void Seeded_round_trip_of_10000_generated_models()
    {
        const int Seed = 20261009;
        TestContext.Current.SendDiagnosticMessage($"Seeded_round_trip_of_10000_generated_models seed={Seed}");
        var random = new Random(Seed);
        var serde = Serde();

        for (var i = 0; i < 10_000; i++)
        {
            var model = new WidgetPatch(RandomString(random), RandomInt(random), RandomString(random));

            var decoded = Decode<WidgetPatch>(serde, Encode(serde, model))!;

            Assert.Equal(model.Name, decoded.Name);
            Assert.Equal(model.Size, decoded.Size);
            Assert.Equal(model.Note, decoded.Note);
            Assert.Equal(model.Name.State, decoded.Name.State);
            Assert.Equal(model.Size.State, decoded.Size.State);
            Assert.Equal(model.Note.State, decoded.Note.State);
        }
    }

    private static readonly string[] s_fragments =
        ["a", "Z", "0", " ", "\"", "\\", "{", "}", "[", "]", ",", ":", "null", "é", "日本", "\U0001F600", "\t", "\n", "\u0001", "</script>"];

    private static Tristate<string> RandomString(Random random)
    {
        switch (random.Next(3))
        {
            case 0:
                return default;
            case 1:
                return Tristate.Null;
            default:
                var builder = new StringBuilder();
                for (var n = random.Next(0, 8); n > 0; n--)
                {
                    builder.Append(s_fragments[random.Next(s_fragments.Length)]);
                }

                return Tristate.Present(builder.ToString());
        }
    }

    private static Tristate<int> RandomInt(Random random) => random.Next(4) switch
    {
        0 => default,
        1 => Tristate.Null,
        2 => Tristate.Present(random.Next(2) == 0 ? int.MinValue : int.MaxValue),
        _ => Tristate.Present(random.Next(int.MinValue, int.MaxValue)),
    };

    private sealed class CallerStringTristateConverter : JsonConverter<Tristate<string>>
    {
        public override bool HandleNull => true;

        public override Tristate<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null ? Tristate<string>.Null : Tristate<string>.Present(reader.GetString()!);

        public override void Write(Utf8JsonWriter writer, Tristate<string> value, JsonSerializerOptions options)
        {
            if (value.IsPresent)
            {
                writer.WriteStringValue("caller:" + value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
