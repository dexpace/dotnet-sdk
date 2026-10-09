// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Serialization;
using Dexpace.Sdk.TestSupport.Serialization;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Serialization;

/// <summary>
/// SERDE-3 / SERDE-12 (P7a-13): the default interface member <c>ISerde.Deserialize&lt;T&gt;(Stream)</c>, which reads the stream
/// to the end under the materialisation cap and decodes the span. A codec with a streaming sync decoder overrides it; a codec
/// that does not (every fake, and any third-party implementer written before phase 7a) still works.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerdeStreamDefaultTests
{
    [Fact]
    public void The_default_reads_the_stream_to_the_end_and_decodes_the_span()
    {
        var serde = new DefaultImplementationSerde { Result = "decoded" };
        ISerde seam = serde;
        byte[] payload = [1, 2, 3, 4, 5];

        var result = seam.Deserialize<string>(new MemoryStream(payload));

        Assert.Equal("decoded", result);
        Assert.Equal(payload, serde.ReceivedBytes);
    }

    [Fact]
    public void The_default_decodes_a_non_seekable_stream()
    {
        var serde = new DefaultImplementationSerde { Result = "decoded" };
        ISerde seam = serde;
        byte[] payload = [9, 8, 7];

        seam.Deserialize<string>(new NonSeekableStream(payload));

        Assert.Equal(payload, serde.ReceivedBytes);
    }

    [Fact]
    public void The_default_leaves_the_stream_open()
    {
        // SERDE-3: the codec never closes a caller-supplied stream.
        ISerde seam = new DefaultImplementationSerde();
        using var stream = new DisposeCountingStream(new MemoryStream([1, 2, 3]));

        seam.Deserialize<string>(stream);

        Assert.Equal(0, stream.DisposeCount);
    }

    [Fact]
    public void The_default_refuses_a_stream_over_the_cap()
    {
        // The seekable stream declares more than the cap, so the refusal comes before any read: nothing near the cap is
        // allocated, and the stream's Read would throw if it were reached.
        ISerde seam = new DefaultImplementationSerde();
        using var stream = new HugeSeekableStream(ResponseBody.DefaultMaxMaterializedBytes + 1);

        var ex = Assert.Throws<BodyTooLargeException>(() => seam.Deserialize<string>(stream));

        Assert.Contains(ResponseBody.DefaultMaxMaterializedBytes.ToString(System.Globalization.CultureInfo.InvariantCulture), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_IOException_from_the_stream_propagates_unwrapped()
    {
        // SERDE-12.
        ISerde seam = new DefaultImplementationSerde();

        var ex = Assert.Throws<IOException>(() => seam.Deserialize<string>(new FailingReadStream()));

        Assert.Equal("the connection dropped", ex.Message);
    }

    [Fact]
    public void Argument_validation()
    {
        ISerde seam = new DefaultImplementationSerde();

        Assert.Throws<ArgumentNullException>(() => seam.Deserialize<string>((Stream)null!));
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class HugeSeekableStream(long length) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("The cap must be enforced from the declared length, before any read.");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class FailingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("the connection dropped");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
