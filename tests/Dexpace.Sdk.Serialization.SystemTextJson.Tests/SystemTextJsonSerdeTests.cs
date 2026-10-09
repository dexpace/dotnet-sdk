// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Streams;
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

    // --- phase 7a: the synchronous stream decode (P7a-13), SERDE-3, SERDE-5, SERDE-6, SERDE-9, SERDE-12 ---

    [Fact]
    public void Sync_Deserialize_from_a_stream_decodes_and_leaves_it_open()
    {
        using var stream = new DisposeCountingStream(new MemoryStream("{\"Name\":\"x\",\"Size\":1}"u8.ToArray()));

        var widget = Serde().Deserialize<Widget>(stream);

        Assert.Equal(new Widget("x", 1), widget);
        Assert.True(stream.CanRead);
        Assert.Equal(0, stream.DisposeCount);
    }

    [Fact]
    public void Sync_Deserialize_from_a_stream_propagates_an_IOException_unwrapped()
    {
        using var stream = new ThrowingStream();

        var ex = Assert.Throws<IOException>(() => Serde().Deserialize<Widget>(stream));

        Assert.Same(stream.Failure, ex);
    }

    [Fact]
    public void Sync_Deserialize_from_a_stream_wraps_malformed_input()
    {
        using var stream = new MemoryStream("{ not json"u8.ToArray());

        var ex = Assert.Throws<DeserializationException>(() => Serde().Deserialize<Widget>(stream));

        Assert.IsAssignableFrom<JsonException>(ex.InnerException);
    }

    [Fact]
    public void Sync_Deserialize_from_a_stream_rejects_a_null_source()
    {
        Assert.Throws<ArgumentNullException>(() => Serde().Deserialize<Widget>((Stream)null!));
    }

    [Fact]
    public async Task A_close_counting_stream_sees_zero_closes_across_all_three_stream_members()
    {
        // SERDE-3: SerializeAsync, DeserializeAsync and the sync Deserialize(Stream) never close a caller-supplied stream.
        var serde = Serde();
        using var written = new DisposeCountingStream(new MemoryStream());
        await serde.SerializeAsync(written, new Widget("a", 1), TestContext.Current.CancellationToken);

        using var asyncRead = new DisposeCountingStream(new MemoryStream("{\"Name\":\"x\",\"Size\":1}"u8.ToArray()));
        await serde.DeserializeAsync<Widget>(asyncRead, TestContext.Current.CancellationToken);

        using var syncRead = new DisposeCountingStream(new MemoryStream("{\"Name\":\"x\",\"Size\":1}"u8.ToArray()));
        serde.Deserialize<Widget>(syncRead);

        Assert.Equal(0, written.DisposeCount);
        Assert.Equal(0, asyncRead.DisposeCount);
        Assert.Equal(0, syncRead.DisposeCount);
    }

    [Fact]
    public void The_seam_default_and_the_override_agree()
    {
        // The override streams; the seam's default materialises. Both must decode identically.
        ISerde serde = Serde();
        var json = "{\"Name\":\"x\",\"Size\":1}"u8.ToArray();

        var streamed = serde.Deserialize<Widget>(new MemoryStream(json));
        var spanned = serde.Deserialize<Widget>(json);

        Assert.Equal(spanned, streamed);
    }

    [Fact]
    public async Task A_list_of_dto_decodes_typed_and_an_unregistered_parametric_target_throws_naming_it()
    {
        // SERDE-6: the element type survives; an unregistered parametric target fails loudly, here as a missing JsonTypeInfo.
        var serde = Serde();
        using var good = new MemoryStream("[{\"Name\":\"x\",\"Size\":1},{\"Name\":\"y\",\"Size\":2}]"u8.ToArray());

        var list = await serde.DeserializeAsync<List<Widget>>(good, TestContext.Current.CancellationToken);

        Assert.IsType<List<Widget>>(list);
        Assert.All(list!, item => Assert.IsType<Widget>(item));
        var ex = Assert.Throws<DeserializationException>(() => serde.Deserialize<List<ApiError>>("[]"u8));
        Assert.Contains("ApiError", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dto_field_access_returns_typed_values()
    {
        // SERDE-5: the result is the closed type, so a field read is a typed read with no cast.
        var widget = Serde().Deserialize<Widget>("{\"Name\":\"x\",\"Size\":7}"u8)!;

        string name = widget.Name;
        int size = widget.Size;

        Assert.Equal("x", name);
        Assert.Equal(7, size);
    }

    [Fact]
    public void A_generic_helper_decodes_into_the_closed_Tristate_type()
    {
        // SEAM-22 for the Tristate type: the closed generic reaches the codec as the generic argument.
        var serde = new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(TristateTestContext.Default));

        var decoded = DecodeGeneric<WidgetPatch>(serde, "{\"name\":null,\"size\":3}");

        Assert.True(decoded!.Name.IsNull);
        Assert.Equal(Tristate.Present(3), decoded.Size);
        Assert.True(decoded.Note.IsAbsent);
    }

    // The helper takes the seam type on purpose: a generic caller that only knows ISerde.
#pragma warning disable CA1859
    private static T? DecodeGeneric<T>(ISerde serde, string json) => serde.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(json));
#pragma warning restore CA1859
}
