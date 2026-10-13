// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json.Serialization;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dexpace.Sdk.Extensions.DependencyInjection.Tests;

/// <summary>A model with a <c>Tristate</c> member, so the round trip proves the codec wires it on the options copy.</summary>
/// <param name="Name">A tri-state string.</param>
/// <param name="Size">A plain number.</param>
public sealed record Gadget(Tristate<string> Name, int Size);

/// <summary>The source-generated context the 7a composition is documented over.</summary>
[JsonSerializable(typeof(Gadget))]
public sealed partial class GadgetJsonContext : JsonSerializerContext;

/// <summary>P9-17 (the 7a hand-off): the codec is composed by the caller, not referenced by the package.</summary>
[Trait("Category", "Unit")]
public sealed class SerdeRegistrationTests
{
    [Fact]
    public async Task CreateDefaultOptions_with_a_context_round_trips_through_the_registered_serde()
    {
        var services = new ServiceCollection();
        _ = services.AddDexpaceClient(TestClients.Name, Config.Section())
            .UseSerde(_ => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(GadgetJsonContext.Default)));
        await using var provider = services.BuildServiceProvider();
        var serde = provider.GetRequiredKeyedService<ISerde>(TestClients.Name);
        var original = new Gadget(Tristate.Null, 3);

        using var stream = new MemoryStream();
        await serde.SerializeAsync(stream, original, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        stream.Position = 0;
        var back = await serde.DeserializeAsync<Gadget>(stream, TestContext.Current.CancellationToken);

        Assert.Equal("""{"name":null,"size":3}""", json);
        Assert.Equal(original, back);
    }

    [Fact]
    public void The_package_does_not_reference_the_codec()
    {
        var references = typeof(DexpaceClientBuilder).Assembly.GetReferencedAssemblies().Select(static a => a.Name);

        Assert.DoesNotContain("Dexpace.Sdk.Serialization.SystemTextJson", references);
    }
}
