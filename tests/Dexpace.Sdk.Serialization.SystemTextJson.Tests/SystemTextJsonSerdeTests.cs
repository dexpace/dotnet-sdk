// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests;

[Trait("Category", "Unit")]
public sealed class SystemTextJsonSerdeTests
{
    private static SystemTextJsonSerde Serde() => new(TestJsonContext.Default);

    [Fact]
    public async Task SerializeAsync_then_DeserializeAsync_round_trips()
    {
        var serde = Serde();
        var widget = new Widget("gizmo", 42);

        using var stream = new MemoryStream();
        await serde.SerializeAsync(stream, widget, TestContext.Current.CancellationToken);
        stream.Position = 0;
        var result = await serde.DeserializeAsync<Widget>(stream, TestContext.Current.CancellationToken);

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

    private sealed record Unregistered(string Value);

    [Fact]
    public void Deserialize_unknown_type_throws_DeserializationException()
    {
        var serde = Serde();
        var ex = Assert.Throws<DeserializationException>(
            () => serde.Deserialize<Unregistered>("{}"u8));
        Assert.Contains("Unregistered", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_malformed_json_throws_DeserializationException()
    {
        var serde = Serde();
        var ex = Assert.Throws<DeserializationException>(() => serde.Deserialize<Widget>("{ not json"u8));
        Assert.IsType<JsonException>(ex.InnerException);
    }

    // --- Widened catch tests ---

    [Fact]
    public void Serialize_reference_cycle_throws_SerializationException()
    {
        // JsonSerializer throws JsonException for reference cycles; the widened catch maps it.
        var serde = Serde();
        var n = new Node();
        n.Next = n;

        var buffer = new ArrayBufferWriter<byte>();
        var ex = Assert.Throws<SerializationException>(() => serde.Serialize(buffer, n));

        // The synchronous writer path reports a cycle as InvalidOperationException (not JsonException, as the async
        // path does); the clause under test is that the cause is chained, whatever its type.
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public async Task SerializeAsync_reference_cycle_throws_SerializationException()
    {
        var serde = Serde();
        var n = new Node();
        n.Next = n;

        using var stream = new MemoryStream();
        var ex = await Assert.ThrowsAsync<SerializationException>(() => serde.SerializeAsync(stream, n, TestContext.Current.CancellationToken).AsTask());
        Assert.IsType<JsonException>(ex.InnerException);
    }

    [Fact]
    public async Task DeserializeAsync_cancelled_token_propagates_OperationCanceledException()
    {
        // Cancellation must NOT be swallowed by the widened catch and re-thrown as DeserializationException.
        var serde = Serde();
        using var stream = new MemoryStream("{\"Name\":\"x\",\"Size\":1}"u8.ToArray());
        var cancelled = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => serde.DeserializeAsync<Widget>(stream, cancelled).AsTask());
    }

    // --- SEAM-20 / SEAM-21 / SEAM-22 pins ---

    private sealed class ThrowingStream : Stream
    {
        public IOException Failure { get; } = new("stream failed");

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw Failure;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw Failure;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw Failure;

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw Failure;

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            throw Failure;
    }

    // The helper takes the seam type on purpose: SEAM-22 is about a generic caller that only knows ISerde.
#pragma warning disable CA1859
    private static async Task<List<T>?> Decode<T>(Core.Serialization.ISerde serde, Stream source) =>
        await serde.DeserializeAsync<List<T>>(source, TestContext.Current.CancellationToken);
#pragma warning restore CA1859

    [Fact]
    public async Task SerializeAsync_leaves_the_destination_open()
    {
        using var stream = new MemoryStream();

        await Serde().SerializeAsync(stream, new Widget("a", 1), TestContext.Current.CancellationToken);

        Assert.True(stream.CanWrite);
    }

    [Fact]
    public async Task DeserializeAsync_leaves_the_source_open()
    {
        using var stream = new MemoryStream("{\"Name\":\"x\",\"Size\":1}"u8.ToArray());

        await Serde().DeserializeAsync<Widget>(stream, TestContext.Current.CancellationToken);

        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task An_IOException_from_the_destination_propagates_unwrapped()
    {
        using var stream = new ThrowingStream();

        var ex = await Assert.ThrowsAsync<IOException>(
            () => Serde().SerializeAsync(stream, new Widget("a", 1), TestContext.Current.CancellationToken).AsTask());

        Assert.Same(stream.Failure, ex);
    }

    [Fact]
    public async Task An_IOException_from_the_source_propagates_unwrapped()
    {
        using var stream = new ThrowingStream();

        var ex = await Assert.ThrowsAsync<IOException>(
            () => Serde().DeserializeAsync<Widget>(stream, TestContext.Current.CancellationToken).AsTask());

        Assert.Same(stream.Failure, ex);
    }

    [Fact]
    public async Task A_generic_helper_decodes_into_the_closed_type()
    {
        // SEAM-22: the capture is the generic argument itself, bound to the closed type at run time.
        using var stream = new MemoryStream("[{\"Name\":\"x\",\"Size\":1}]"u8.ToArray());

        var result = await Decode<Widget>(Serde(), stream);

        var items = Assert.IsType<List<Widget>>(result);
        Assert.Equal(new Widget("x", 1), Assert.Single(items));
    }
}
