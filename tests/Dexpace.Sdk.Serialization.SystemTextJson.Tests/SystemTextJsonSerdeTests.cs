// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

public sealed class SystemTextJsonSerdeTests
{
    private static SystemTextJsonSerde Serde() => new(TestJsonContext.Default);

    [Fact]
    public async Task SerializeAsync_then_DeserializeAsync_round_trips()
    {
        var serde = Serde();
        var widget = new Widget("gizmo", 42);

        using var stream = new MemoryStream();
        await serde.SerializeAsync(stream, widget);
        stream.Position = 0;
        var result = await serde.DeserializeAsync<Widget>(stream);

        Assert.Equal(widget, result);
    }

    [Fact]
    public void DefaultMediaType_is_application_json_utf8()
    {
        Assert.Equal(CommonMediaTypes.ApplicationJsonUtf8, Serde().DefaultMediaType);
    }

    [Fact]
    public void Serialize_then_Deserialize_sync_round_trips()
    {
        var serde = Serde();
        var widget = new Widget("sprocket", 7);

        var buffer = new ArrayBufferWriter<byte>();
        serde.Serialize(buffer, widget);
        var result = serde.Deserialize<Widget>(buffer.WrittenSpan);

        Assert.Equal(widget, result);
    }
}
