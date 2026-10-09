// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json;
using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

/// <summary>The target of the SERDE-21 and SERDE-22 coercion matrix: one member per primitive family.</summary>
public sealed record CoercionTarget(int I, double D, bool B, string S);

/// <summary>The target of the SERDE-24 date round trip.</summary>
public sealed record Stamped(DateTimeOffset At, DateTime Utc);

/// <summary>A context in the default <c>General</c> options, to prove the strictness is not Web-specific.</summary>
[JsonSerializable(typeof(CoercionTarget))]
[JsonSerializable(typeof(Stamped))]
[JsonSerializable(typeof(Widget))]
internal sealed partial class CoercionContext : JsonSerializerContext;

/// <summary>
/// SERDE-21 and SERDE-22 (design position C): the nine coercions a strict codec refuses, as nine named facts, each run
/// under <see cref="SystemTextJsonSerde.CreateDefaultOptions"/> and under a plain <c>General</c> context. A loop that dropped
/// one row would be invisible, so there is no loop (the Ruby design's warning). The JSON uses PascalCase names, which both
/// configurations bind (<c>Web</c> reads case-insensitively).
/// </summary>
[Trait("Category", "Unit")]
public sealed class StrictCoercionTests
{
    private static SystemTextJsonSerde Default() =>
        new(SystemTextJsonSerde.CreateDefaultOptions(CoercionContext.Default));

    private static SystemTextJsonSerde General() => new(CoercionContext.Default);

    private static void AssertRejected(string json)
    {
        foreach (var serde in new[] { Default(), General() })
        {
            var ex = Assert.Throws<DeserializationException>(() => serde.Deserialize<CoercionTarget>(System.Text.Encoding.UTF8.GetBytes(json)));
            Assert.IsAssignableFrom<JsonException>(ex.InnerException);
        }
    }

    private static CoercionTarget Bind(SystemTextJsonSerde serde, string json) =>
        serde.Deserialize<CoercionTarget>(System.Text.Encoding.UTF8.GetBytes(json))!;

    [Fact]
    public void String_five_does_not_bind_to_int() => AssertRejected("""{"I":"5"}""");

    [Fact]
    public void String_1_5_does_not_bind_to_double() => AssertRejected("""{"D":"1.5"}""");

    [Fact]
    public void String_true_does_not_bind_to_bool() => AssertRejected("""{"B":"true"}""");

    [Fact]
    public void Empty_string_does_not_bind_to_int() => AssertRejected("""{"I":""}""");

    [Fact]
    public void Float_1_5_does_not_bind_to_int() => AssertRejected("""{"I":1.5}""");

    [Fact]
    public void Bool_true_does_not_bind_to_int() => AssertRejected("""{"I":true}""");

    [Fact]
    public void Int_1_does_not_bind_to_bool() => AssertRejected("""{"B":1}""");

    [Fact]
    public void Bool_true_does_not_bind_to_double() => AssertRejected("""{"D":true}""");

    [Fact]
    public void Int_5_does_not_bind_to_string() => AssertRejected("""{"S":5}""");

    // ---- SERDE-22: the two permissions ----------------------------------------------------------------------------

    [Fact]
    public void Integer_widens_to_double()
    {
        Assert.Equal(5.0, Bind(Default(), """{"D":5}""").D);
        Assert.Equal(5.0, Bind(General(), """{"D":5}""").D);
    }

    [Fact]
    public void Empty_string_binds_to_string()
    {
        Assert.Equal(string.Empty, Bind(Default(), """{"S":""}""").S);
        Assert.Equal(string.Empty, Bind(General(), """{"S":""}""").S);
    }

    [Fact]
    public void A_well_typed_document_binds()
    {
        const string Json = """{"I":3,"D":1.5,"B":true,"S":"x"}""";

        Assert.Equal(new CoercionTarget(3, 1.5, true, "x"), Bind(Default(), Json));
        Assert.Equal(new CoercionTarget(3, 1.5, true, "x"), Bind(General(), Json));
    }
}
