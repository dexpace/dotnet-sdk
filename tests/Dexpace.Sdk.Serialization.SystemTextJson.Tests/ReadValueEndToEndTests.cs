// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Serialization;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

/// <summary>
/// SERDE-13 and SERDE-27 over the real adapter (P7a-9, P7a-10): <c>Dexpace.Sdk.Core.Tests</c> proves the readers and handlers
/// over an in-test codec (SEAM-2); this class proves the same rules with <see cref="SystemTextJsonSerde"/> underneath.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReadValueEndToEndTests
{
    private static SystemTextJsonSerde Serde() =>
        new(SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default));

    private static ResponseBody Body(string json) => ResponseBody.FromBytes(System.Text.Encoding.UTF8.GetBytes(json));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReadValueAsync_of_a_root_null_names_T()
    {
        var ex = await Assert.ThrowsAsync<DeserializationException>(async () => await Body("null").ReadValueAsync<Widget>(Serde(), Token));

        Assert.Contains(nameof(Widget), ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void ReadValue_of_a_root_null_names_T()
    {
        var ex = Assert.Throws<DeserializationException>(() => Body("null").ReadValue<Widget>(Serde(), Token));

        Assert.Contains(nameof(Widget), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadValueOrDefault_admits_a_root_null_through_the_real_adapter()
    {
        Assert.Null(await Body("null").ReadValueOrDefaultAsync<Widget>(Serde(), Token));
        Assert.Null(Body("null").ReadValueOrDefault<Widget>(Serde(), Token));
    }

    [Fact]
    public async Task A_Tristate_target_decodes_a_root_null_to_Null()
    {
        // A value-type target never trips the root-null rule: Tristate decodes a root null to its Null state.
        Assert.True((await Body("null").ReadValueAsync<Tristate<string>>(Serde(), Token)).IsNull);
    }

    [Fact]
    public async Task A_typed_read_returns_the_value()
    {
        var widget = await Body("""{"name":"gear","size":9}""").ReadValueAsync<Widget>(Serde(), Token);

        Assert.Equal(new Widget("gear", 9), widget);
    }

    [Fact]
    public void A_typed_sync_read_streams_through_the_adapters_override()
    {
        var widget = Body("""{"name":"gear","size":9}""").ReadValue<Widget>(Serde(), Token);

        Assert.Equal(new Widget("gear", 9), widget);
    }

    [Fact]
    public async Task A_Tristate_PATCH_model_decodes_from_a_response_body()
    {
        var patch = await Body("""{"name":null,"size":3}""").ReadValueAsync<WidgetPatch>(Serde(), Token);

        Assert.True(patch.Name.IsNull);
        Assert.Equal(Tristate.Present(3), patch.Size);
        Assert.True(patch.Note.IsAbsent);
    }

    [Fact]
    public async Task A_204_style_empty_body_names_T()
    {
        // SERDE-27's missing-body rule, with the adapter underneath: not STJ's generic "no JSON tokens" failure.
        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await ResponseBody.FromBytes(Array.Empty<byte>()).ReadValueAsync<Widget>(Serde(), Token));

        Assert.Contains("no body to deserialize as", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Widget), ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task A_malformed_body_is_a_DeserializationException_with_the_JsonException_inside()
    {
        var ex = await Assert.ThrowsAsync<DeserializationException>(async () => await Body("{ not json").ReadValueAsync<Widget>(Serde(), Token));

        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    [Fact]
    public async Task The_body_is_single_use_after_a_typed_read()
    {
        var body = Body("""{"name":"gear","size":9}""");
        await body.ReadValueAsync<Widget>(Serde(), Token);

        await Assert.ThrowsAsync<StreamConsumedException>(async () => await body.ReadValueAsync<Widget>(Serde(), Token));
    }
}
