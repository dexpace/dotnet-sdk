// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Buffers;
using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Serialization;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Serialization;

/// <summary>
/// SERDE-3, SERDE-9, SERDE-12 and SERDE-13 (P7a-9 to P7a-12): the four typed readers on <see cref="ResponseBody"/>, over
/// the in-test codec and a body whose materialising readers throw. Every fact has an asynchronous form and a synchronous
/// twin (<c>_sync</c>).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReadValueTests
{
    private static CountingPayloadBody Body(string payload, long length = -1, Exception? disposeFailure = null) =>
        new(Encoding.UTF8.GetBytes(payload), length, disposeFailure);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ---- a value decodes through a stream, never a buffer ---------------------------------------------------------

    [Fact]
    public async Task A_value_decodes_through_a_stream()
    {
        var serde = new Utf8LiteralSerde();
        using var body = Body("ok:abc");

        var value = await body.ReadValueAsync<string>(serde, Token);

        Assert.Equal("abc", value);
        Assert.Equal(1, serde.StreamReads);
        Assert.Equal(1, body.OpenCount);
    }

    [Fact]
    public void A_value_decodes_through_a_stream_sync()
    {
        var serde = new Utf8LiteralSerde();
        using var body = Body("ok:abc");

        var value = body.ReadValue<string>(serde, Token);

        Assert.Equal("abc", value);
        Assert.Equal(1, serde.StreamReads);
    }

    [Fact]
    public async Task A_peeked_byte_is_replayed_to_the_codec()
    {
        var serde = new Utf8LiteralSerde();

        await Body("ok:abc").ReadValueAsync<string>(serde, Token);

        Assert.Equal("ok:abc"u8.ToArray(), serde.LastBytes);
    }

    [Fact]
    public void A_peeked_byte_is_replayed_to_the_codec_sync()
    {
        var serde = new Utf8LiteralSerde();

        Body("ok:abc").ReadValue<string>(serde, Token);

        Assert.Equal("ok:abc"u8.ToArray(), serde.LastBytes);
    }

    // ---- disposal on every path (P7a-11) --------------------------------------------------------------------------

    [Theory]
    [InlineData("ok:abc")]
    [InlineData("bad")]
    [InlineData("io...")]
    [InlineData("null")]
    public async Task The_body_and_its_stream_are_disposed_exactly_once_on_every_path(string payload)
    {
        using var body = Body(payload);

        await Record.ExceptionAsync(async () => await body.ReadValueAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(1, body.StreamDisposeCount);
    }

    [Theory]
    [InlineData("ok:abc")]
    [InlineData("bad")]
    [InlineData("io...")]
    [InlineData("null")]
    public void The_body_and_its_stream_are_disposed_exactly_once_on_every_path_sync(string payload)
    {
        using var body = Body(payload);

        Record.Exception(() => body.ReadValue<string>(new Utf8LiteralSerde(), Token));

        Assert.Equal(1, body.DisposeCount);
        Assert.Equal(1, body.StreamDisposeCount);
    }

    [Fact]
    public async Task A_dispose_failure_after_a_decode_failure_is_attached_not_substituted()
    {
        // RECOV-16's pattern: the primary failure is the codec's; the release failure rides on its trail.
        var releaseFailure = new InvalidOperationException("release failed");
        using var body = Body("bad", disposeFailure: releaseFailure);

        var thrown = await Assert.ThrowsAsync<DeserializationException>(
            async () => await body.ReadValueAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));
    }

    [Fact]
    public void A_dispose_failure_after_a_decode_failure_is_attached_not_substituted_sync()
    {
        var releaseFailure = new InvalidOperationException("release failed");
        using var body = Body("bad", disposeFailure: releaseFailure);

        var thrown = Assert.Throws<DeserializationException>(() => body.ReadValue<string>(new Utf8LiteralSerde(), Token));

        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));
    }

    [Fact]
    public async Task A_dispose_failure_after_a_root_null_is_attached_to_the_root_null_failure()
    {
        var releaseFailure = new InvalidOperationException("release failed");
        using var body = Body("null", disposeFailure: releaseFailure);

        var thrown = await Assert.ThrowsAsync<DeserializationException>(
            async () => await body.ReadValueAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));
    }

    // ---- SERDE-13: root null ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_root_null_for_a_reference_type_is_a_DeserializationException_naming_T()
    {
        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await Body("null").ReadValueAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.Contains("System.String", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ReadValueOrDefaultAsync", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void A_root_null_for_a_reference_type_is_a_DeserializationException_naming_T_sync()
    {
        var ex = Assert.Throws<DeserializationException>(() => Body("null").ReadValue<string>(new Utf8LiteralSerde(), Token));

        Assert.Contains("System.String", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task A_nullable_value_type_admits_a_root_null()
    {
        Assert.Null(await Body("null").ReadValueAsync<int?>(new Utf8LiteralSerde(), Token));
    }

    [Fact]
    public void A_nullable_value_type_admits_a_root_null_sync()
    {
        Assert.Null(Body("null").ReadValue<int?>(new Utf8LiteralSerde(), Token));
    }

    [Fact]
    public async Task ReadValueOrDefault_admits_a_root_null()
    {
        Assert.Null(await Body("null").ReadValueOrDefaultAsync<string>(new Utf8LiteralSerde(), Token));
        Assert.Equal("abc", await Body("ok:abc").ReadValueOrDefaultAsync<string>(new Utf8LiteralSerde(), Token));
    }

    [Fact]
    public void ReadValueOrDefault_admits_a_root_null_sync()
    {
        Assert.Null(Body("null").ReadValueOrDefault<string>(new Utf8LiteralSerde(), Token));
        Assert.Equal("abc", Body("ok:abc").ReadValueOrDefault<string>(new Utf8LiteralSerde(), Token));
    }

    // ---- SERDE-27's missing body -----------------------------------------------------------------------------------

    [Fact]
    public async Task An_empty_body_with_ContentLength_zero_is_a_missing_body_naming_T()
    {
        using var body = Body(string.Empty, length: 0);

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await body.ReadValueAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.Equal("The response has no body to deserialize as 'System.String'.", ex.Message);
        Assert.Null(ex.InnerException);
        Assert.Equal(0, body.OpenCount);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public void An_empty_body_with_ContentLength_zero_is_a_missing_body_naming_T_sync()
    {
        using var body = Body(string.Empty, length: 0);

        var ex = Assert.Throws<DeserializationException>(() => body.ReadValue<string>(new Utf8LiteralSerde(), Token));

        Assert.Equal("The response has no body to deserialize as 'System.String'.", ex.Message);
        Assert.Equal(0, body.OpenCount);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task ReadValueOrDefault_does_not_turn_a_missing_body_into_a_default()
    {
        // Q3: it admits the wire's null, not an absent payload.
        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await Body(string.Empty, length: 0).ReadValueOrDefaultAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.Contains("no body", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadValueOrDefault_does_not_turn_a_missing_body_into_a_default_sync()
    {
        var ex = Assert.Throws<DeserializationException>(
            () => Body(string.Empty, length: 0).ReadValueOrDefault<string>(new Utf8LiteralSerde(), Token));

        Assert.Contains("no body", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_zero_byte_unknown_length_body_is_a_missing_body_naming_T()
    {
        using var body = Body(string.Empty);

        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await body.ReadValueAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.Contains("no body to deserialize as 'System.String'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.OpenCount);
        Assert.Equal(1, body.StreamDisposeCount);
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public void A_zero_byte_unknown_length_body_is_a_missing_body_naming_T_sync()
    {
        using var body = Body(string.Empty);

        var ex = Assert.Throws<DeserializationException>(() => body.ReadValue<string>(new Utf8LiteralSerde(), Token));

        Assert.Contains("no body to deserialize as 'System.String'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, body.StreamDisposeCount);
    }

    [Fact]
    public async Task A_real_zero_byte_stream_body_is_a_missing_body()
    {
        var body = ResponseBody.FromStream(new MemoryStream());

        await Assert.ThrowsAsync<DeserializationException>(
            async () => await body.ReadValueAsync<string>(new Utf8LiteralSerde(), Token));
    }

    // ---- SERDE-12 / SERDE-9 ----------------------------------------------------------------------------------------

    [Fact]
    public async Task An_IOException_mid_stream_propagates_unwrapped()
    {
        var ex = await Record.ExceptionAsync(async () => await Body("io...").ReadValueAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.IsType<IOException>(ex);
        Assert.IsNotType<DeserializationException>(ex);
    }

    [Fact]
    public void An_IOException_mid_stream_propagates_unwrapped_sync()
    {
        var ex = Record.Exception(() => Body("io...").ReadValue<string>(new Utf8LiteralSerde(), Token));

        Assert.IsType<IOException>(ex);
    }

    [Fact]
    public async Task A_codec_DeserializationException_is_the_same_instance()
    {
        var failure = new DeserializationException("codec says no", new FormatException("inner"));

        var thrown = await Assert.ThrowsAsync<DeserializationException>(
            async () => await Body("ok:abc").ReadValueAsync<string>(new ThrowingSerde(failure), Token));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void A_codec_DeserializationException_is_the_same_instance_sync()
    {
        var failure = new DeserializationException("codec says no", new FormatException("inner"));

        var thrown = Assert.Throws<DeserializationException>(() => Body("ok:abc").ReadValue<string>(new ThrowingSerde(failure), Token));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task A_malformed_payload_surfaces_the_codecs_own_inner_exception()
    {
        var ex = await Assert.ThrowsAsync<DeserializationException>(
            async () => await Body("bad").ReadValueAsync<string>(new Utf8LiteralSerde(), Token));

        Assert.IsType<FormatException>(ex.InnerException);
    }

    // ---- single use, cancellation, arguments -----------------------------------------------------------------------

    [Fact]
    public async Task A_second_read_surfaces_the_as_built_exception()
    {
        // R4: the body is disposed after the first read; a second read still sees the single-use latch first.
        var body = ResponseBody.FromBytes("ok:abc"u8.ToArray());
        await body.ReadValueAsync<string>(new Utf8LiteralSerde(), Token);

        await Assert.ThrowsAsync<StreamConsumedException>(
            async () => await body.ReadValueAsync<string>(new Utf8LiteralSerde(), Token));
    }

    [Fact]
    public void A_second_read_surfaces_the_as_built_exception_sync()
    {
        var body = ResponseBody.FromBytes("ok:abc"u8.ToArray());
        body.ReadValue<string>(new Utf8LiteralSerde(), Token);

        Assert.Throws<StreamConsumedException>(() => body.ReadValue<string>(new Utf8LiteralSerde(), Token));
    }

    [Fact]
    public async Task The_cancellation_token_reaches_the_codec()
    {
        using var cts = new CancellationTokenSource();
        var serde = new Utf8LiteralSerde();
        using var body = Body("ok:abc");

        await body.ReadValueAsync<string>(serde, cts.Token);

        Assert.Equal(cts.Token, serde.LastToken);
        Assert.Equal(cts.Token, body.LastOpenToken);
    }

    [Fact]
    public void The_cancellation_token_reaches_the_body_sync()
    {
        // The synchronous stream decode takes no token (P7a-13); the open does.
        using var cts = new CancellationTokenSource();
        using var body = Body("ok:abc");

        body.ReadValue<string>(new Utf8LiteralSerde(), cts.Token);

        Assert.Equal(cts.Token, body.LastOpenToken);
    }

    [Fact]
    public async Task Argument_validation_does_not_dispose_a_body_the_caller_still_owns()
    {
        using var body = Body("ok:abc");

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await body.ReadValueAsync<string>(null!, Token));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await ((ResponseBody)null!).ReadValueAsync<string>(new Utf8LiteralSerde(), Token));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await body.ReadValueOrDefaultAsync<string>(null!, Token));
        Assert.Equal(0, body.DisposeCount);
        Assert.Equal(0, body.OpenCount);
    }

    [Fact]
    public void Argument_validation_does_not_dispose_a_body_the_caller_still_owns_sync()
    {
        using var body = Body("ok:abc");

        Assert.Throws<ArgumentNullException>(() => body.ReadValue<string>(null!, Token));
        Assert.Throws<ArgumentNullException>(() => ((ResponseBody)null!).ReadValue<string>(new Utf8LiteralSerde(), Token));
        Assert.Throws<ArgumentNullException>(() => body.ReadValueOrDefault<string>(null!, Token));
        Assert.Equal(0, body.DisposeCount);
    }

    private sealed class ThrowingSerde(Exception failure) : ISerde
    {
        public MediaType DefaultMediaType => MediaType.Of("application", "json");

        public ValueTask SerializeAsync<T>(Stream destination, T value, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<T?> DeserializeAsync<T>(Stream source, CancellationToken cancellationToken = default) =>
            throw failure;

        public void Serialize<T>(IBufferWriter<byte> destination, T value)
        {
        }

        public T? Deserialize<T>(ReadOnlySpan<byte> utf8) => throw failure;

        public T? Deserialize<T>(Stream source) => throw failure;
    }
}
