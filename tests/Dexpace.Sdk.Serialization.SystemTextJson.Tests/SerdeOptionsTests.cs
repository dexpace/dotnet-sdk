// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

/// <summary>
/// SERDE-19, SERDE-21, SERDE-25 and SERDE-26 (design position C, P7a-5, P7a-8): how <see cref="SystemTextJsonSerde"/> treats
/// the options it is given and the default options it offers.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerdeOptionsTests
{
    private static JsonSerializerOptions CallerOptions() => new() { TypeInfoResolver = TristateTestContext.Default };

    private static string Encode<T>(SystemTextJsonSerde serde, T value)
    {
        var writer = new ArrayBufferWriter<byte>();
        serde.Serialize(writer, value);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }

    // ---- SERDE-26: the caller's options are never frozen or wired -------------------------------------------------

    [Fact]
    public void The_callers_options_are_not_frozen_by_construction()
    {
        var options = CallerOptions();

        _ = new SystemTextJsonSerde(options);

        Assert.False(options.IsReadOnly);
        options.WriteIndented = true;
        Assert.True(options.WriteIndented);
    }

    [Fact]
    public void The_callers_options_keep_their_converter_count_and_resolver_reference()
    {
        var options = CallerOptions();
        var resolver = options.TypeInfoResolver;
        var convertersBefore = options.Converters.Count;

        _ = new SystemTextJsonSerde(options);

        Assert.Equal(convertersBefore, options.Converters.Count);
        Assert.DoesNotContain(options.Converters, c => c.GetType().Name == "TristateConverterFactory");
        Assert.Same(resolver, options.TypeInfoResolver);
    }

    [Fact]
    public void The_serde_injects_nothing_into_the_callers_options_beyond_a_private_Tristate_copy()
    {
        // No SDK-injected change: the caller's own options are exactly what they were, so the caller's JsonSerializer
        // use of them is unaffected by the serde (the Absent omission lives on the serde's private copy).
        var options = CallerOptions();
        var serde = new SystemTextJsonSerde(options);

        Assert.Equal("{}", Encode(serde, new WidgetPatch(default, default, default)));
        Assert.Empty(options.Converters);
        Assert.False(options.IsReadOnly);
    }

    [Fact]
    public void A_later_mutation_of_the_callers_options_does_not_affect_the_serde()
    {
        var options = CallerOptions();
        var serde = new SystemTextJsonSerde(options);

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        Assert.Equal("""{"Name":"a"}""", Encode(serde, new WidgetPatch("a", default, default)));
    }

    [Fact]
    public void The_context_constructor_copies_the_context_options()
    {
        var contextOptions = TristateTestContext.Default.Options;
        var convertersBefore = contextOptions.Converters.Count;
        var readOnlyBefore = contextOptions.IsReadOnly;

        var serde = new SystemTextJsonSerde(TristateTestContext.Default);

        Assert.Equal("{}", Encode(serde, new WidgetPatch(default, default, default)));
        Assert.Equal(convertersBefore, contextOptions.Converters.Count);
        Assert.DoesNotContain(contextOptions.Converters, c => c.GetType().Name == "TristateConverterFactory");
        Assert.Equal(readOnlyBefore, contextOptions.IsReadOnly);
    }

    [Fact]
    public void A_null_options_argument_throws_and_a_resolverless_one_still_throws_ArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => new SystemTextJsonSerde((JsonSerializerOptions)null!));
        Assert.Throws<ArgumentNullException>(() => new SystemTextJsonSerde((JsonSerializerContext)null!));
        Assert.Throws<ArgumentException>(() => new SystemTextJsonSerde(new JsonSerializerOptions()));
    }

    [Fact]
    public void Constructing_twice_from_one_options_object_works()
    {
        var options = CallerOptions();

        var first = new SystemTextJsonSerde(options);
        var second = new SystemTextJsonSerde(options);

        Assert.Equal("""{"Size":1}""", Encode(first, new WidgetPatch(default, 1, default)));
        Assert.Equal("""{"Size":1}""", Encode(second, new WidgetPatch(default, 1, default)));
    }

    // ---- SERDE-25 / SERDE-21: CreateDefaultOptions -----------------------------------------------------------------

    [Fact]
    public void CreateDefaultOptions_returns_a_fresh_mutable_instance_per_call()
    {
        var first = SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default);
        var second = SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default);

        Assert.NotSame(first, second);
        Assert.False(first.IsReadOnly);
        Assert.False(second.IsReadOnly);
        first.WriteIndented = true;
        Assert.False(second.WriteIndented);
    }

    [Fact]
    public void CreateDefaultOptions_uses_Web_naming_and_strict_numbers()
    {
        var options = SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default);

        Assert.Equal(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.Equal(JsonNumberHandling.Strict, options.NumberHandling);
        Assert.True(options.RespectNullableAnnotations);
    }

    [Fact]
    public void CreateDefaultOptions_requires_a_resolver()
    {
        Assert.Throws<ArgumentNullException>(() => SystemTextJsonSerde.CreateDefaultOptions(null!));
    }

    [Fact]
    public void CreateDefaultOptions_is_Tristate_wired()
    {
        var options = SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default);

        Assert.Contains(options.Converters, c => c.GetType().Name == "TristateConverterFactory");
        var json = JsonSerializer.Serialize(
            new WidgetPatch(default, 3, default),
            (System.Text.Json.Serialization.Metadata.JsonTypeInfo<WidgetPatch>)options.GetTypeInfo(typeof(WidgetPatch)));
        Assert.Equal("""{"size":3}""", json);
    }

    [Fact]
    public void CreateDefaultOptions_is_wired_once_even_after_a_serde_wires_its_copy_again()
    {
        var options = SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default);
        var convertersBefore = options.Converters.Count;

        var serde = new SystemTextJsonSerde(options);

        Assert.Equal(convertersBefore, options.Converters.Count);
        Assert.Equal("""{"size":3}""", Encode(serde, new WidgetPatch(default, 3, default)));
    }

    [Fact]
    public void RespectNullableAnnotations_rejects_a_member_null_under_the_defaults()
    {
        var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default));

        var ex = Assert.Throws<DeserializationException>(
            () => serde.Deserialize<NonNullable>("""{"required":null}"""u8));

        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    [Fact]
    public void A_root_null_is_still_returned_as_null_under_RespectNullableAnnotations()
    {
        // Design fact 5: the reader layer (ReadValue) rejects it, not the codec.
        var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default));

        Assert.Null(serde.Deserialize<NonNullable>("null"u8));
    }

    // ---- SERDE-23, SERDE-24, SERDE-29: STJ defaults that the conformance suite asserts ----------------------------

    [Fact]
    public void An_unmapped_member_is_skipped()
    {
        // SERDE-23: forward compatibility, under the defaults and under a plain context.
        const string Json = """{"name":"a","size":1,"extra":{"x":[1,2]}}""";
        var bytes = Encoding.UTF8.GetBytes(Json);

        Assert.Equal(new Widget("a", 1), new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(CoercionContext.Default)).Deserialize<Widget>(bytes));
        Assert.Equal(new Widget("a", 1), new SystemTextJsonSerde(CoercionContext.Default).Deserialize<Widget>(Encoding.UTF8.GetBytes("""{"Name":"a","Size":1,"Extra":{"x":[1,2]}}""")));
    }

    [Fact]
    public void DateTimeOffset_round_trips_as_ISO_8601_and_the_same_instant()
    {
        // SERDE-24: an offset survives as text (+05:30), and the decoded value is the same instant.
        var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(CoercionContext.Default));
        var stamped = new Stamped(
            new DateTimeOffset(2026, 10, 9, 12, 30, 15, TimeSpan.FromMinutes(330)),
            new DateTime(2026, 10, 9, 7, 0, 0, DateTimeKind.Utc));

        var json = Encode(serde, stamped);
        var decoded = serde.Deserialize<Stamped>(Encoding.UTF8.GetBytes(json))!;

        Assert.Contains("\"at\":\"2026-10-09T12:30:15+05:30\"", json, StringComparison.Ordinal);
        Assert.Contains("\"utc\":\"2026-10-09T07:00:00Z\"", json, StringComparison.Ordinal);
        Assert.Equal(stamped.At.UtcDateTime, decoded.At.UtcDateTime);
        Assert.Equal(stamped.At.Offset, decoded.At.Offset);
        Assert.Equal(stamped.Utc, decoded.Utc);
        Assert.Equal(DateTimeKind.Utc, decoded.Utc.Kind);
    }

    [Fact]
    public async Task Many_workers_share_one_serde_without_cross_talk()
    {
        // SERDE-29: a fresh serde (cold metadata cache, so the first-use initialisation races), then the warm path.
        const int Workers = 32;
        const int Iterations = 500;
        var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(CoercionContext.Default));
        var gate = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, Workers).Select(worker => Task.Run(async () =>
        {
            await gate.Task;
            for (var i = 0; i < Iterations; i++)
            {
                var widget = new Widget($"w{worker}-{i}", (worker * 1000) + i);
                var writer = new ArrayBufferWriter<byte>();
                serde.Serialize(writer, widget);
                Assert.Equal(widget, serde.Deserialize<Widget>(writer.WrittenSpan));
            }
        }, TestContext.Current.CancellationToken)).ToArray();

        gate.SetResult();
        await Task.WhenAll(tasks);

        Assert.All(tasks, t => Assert.True(t.IsCompletedSuccessfully));
    }
}
