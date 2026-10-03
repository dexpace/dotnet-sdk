// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/body/multipart-body.test.ts: every case except the builder cases
// (`newBuilder`, `parts`), which test a builder object this port does not have (design §10 entry 10). The strip-CR/LF case
// is inverted to a rejection (P3b-8); an empty part list being rejected is added.

#pragma warning disable CA2000 // The streams and bodies under test are owned by the test.

using System.Text;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary><see cref="RequestBody.Multipart"/>: HTTP-51, BODY-2, BODY-6 (composite), BODY-8 (design P3b-8).</summary>
[Trait("Category", "Unit")]
public class MultipartBodyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MultipartPart Field(string name, string value) => new(name, RequestBody.FromString(value));

    private static RequestBody SingleUse(byte[] bytes) => RequestBody.FromStream(new MemoryStream(bytes));

    private static async Task<string> RenderAsync(RequestBody body)
    {
        using var sink = new MemoryStream();
        await body.WriteToAsync(sink, Token);
        return Encoding.UTF8.GetString(sink.ToArray());
    }

    // A caller-supplied body can report one length and write another.
    private sealed class LyingBody(long declared, int actual, bool replayable = true) : RequestBody
    {
        public override MediaType? ContentType => null;

        public override long ContentLength => declared;

        public override bool IsReplayable => replayable;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            destination.WriteAsync(new byte[actual], cancellationToken).AsTask();

        public override void WriteTo(Stream destination, CancellationToken cancellationToken = default) =>
            destination.Write(new byte[actual]);
    }

    [Fact]
    public async Task The_wire_bytes_match_a_hand_written_frame()
    {
        var body = RequestBody.Multipart(
            [new MultipartPart("field", RequestBody.FromBytes(Encoding.UTF8.GetBytes("value")))],
            "B");

        Assert.Equal("--B\r\nContent-Disposition: form-data; name=\"field\"\r\n\r\nvalue\r\n--B--\r\n", await RenderAsync(body));
    }

    [Fact]
    public async Task Two_parts_with_filename_and_content_type_frame_correctly()
    {
        var body = RequestBody.Multipart(
            [
                Field("a", "1"),
                new MultipartPart("file", RequestBody.FromBytes(new byte[] { 65 }, MediaType.Of("text", "plain")), "a.txt"),
            ],
            "B");

        Assert.Equal(
            "--B\r\nContent-Disposition: form-data; name=\"a\"\r\nContent-Type: text/plain;charset=utf-8\r\n\r\n1\r\n"
            + "--B\r\nContent-Disposition: form-data; name=\"file\"; filename=\"a.txt\"\r\nContent-Type: text/plain\r\n\r\nA\r\n"
            + "--B--\r\n",
            await RenderAsync(body));
    }

    [Fact]
    public void The_content_type_is_multipart_form_data_with_the_boundary_parameter()
    {
        var body = RequestBody.Multipart([Field("a", "x")], "FIXED");

        Assert.Equal("multipart", body.ContentType!.Type);
        Assert.Equal("form-data", body.ContentType.Subtype);
        Assert.Equal("FIXED", body.ContentType.Parameters["boundary"]);
    }

    [Fact]
    public async Task ContentLength_equals_the_bytes_actually_written()
    {
        var body = RequestBody.Multipart(
            [Field("a", "hello"), new MultipartPart("b", RequestBody.FromBytes(new byte[1000]), "f.bin"), Field("café", "é")],
            "FIXEDBOUNDARY");
        using var sink = new MemoryStream();

        await body.WriteToAsync(sink, Token);

        Assert.Equal(body.ContentLength, sink.Length);
        using var syncSink = new MemoryStream();
        body.WriteTo(syncSink, Token);
        Assert.Equal(sink.ToArray(), syncSink.ToArray());
    }

    [Fact]
    public void ContentLength_is_minus_one_when_any_part_is_unknown() =>
        Assert.Equal(-1, RequestBody.Multipart([Field("a", "x"), new MultipartPart("b", SingleUse([1]))]).ContentLength);

    [Fact]
    public void IsReplayable_is_the_conjunction_over_the_parts()
    {
        Assert.True(RequestBody.Multipart([Field("a", "x")]).IsReplayable);
        Assert.True(RequestBody.Multipart([Field("a", "x"), new MultipartPart("b", RequestBody.FromStream(new MemoryStream([1]), contentLength: 1))]).IsReplayable);
        Assert.False(RequestBody.Multipart([Field("a", "x"), new MultipartPart("b", SingleUse([1]))]).IsReplayable);
    }

    [Fact]
    public async Task A_replayable_composite_writes_identical_bytes_twice_and_admits_concurrent_writes()
    {
        var body = RequestBody.Multipart([Field("a", "x"), Field("b", "y")], "B");

        var first = await RenderAsync(body);
        Assert.Equal(first, await RenderAsync(body));
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => RenderAsync(body), Token)));
        Assert.All(concurrent, c => Assert.Equal(first, c));
        Assert.Same(body, await body.ToReplayableAsync(Token));
    }

    [Fact]
    public async Task A_single_use_composite_refuses_a_second_write_before_any_byte()
    {
        var body = RequestBody.Multipart([new MultipartPart("a", SingleUse([1, 2]))], "B");
        _ = await RenderAsync(body);
        using var second = new MemoryStream();

        await Assert.ThrowsAsync<StreamConsumedException>(() => body.WriteToAsync(second, Token));
        Assert.Throws<StreamConsumedException>(() => body.WriteTo(second, Token));

        Assert.Equal(0, second.Length);
    }

    [Fact]
    public async Task A_part_whose_own_length_lies_is_caught_by_the_bounded_writer()
    {
        var tooLong = RequestBody.Multipart([new MultipartPart("a", new LyingBody(1, 5))], "B");
        using var sink = new MemoryStream();

        await Assert.ThrowsAsync<IOException>(() => tooLong.WriteToAsync(sink, Token));
        // The overrun chunk was refused before it reached the sink: nothing past the declared total was written.
        Assert.True(sink.Length <= tooLong.ContentLength);

        var tooShort = RequestBody.Multipart([new MultipartPart("a", new LyingBody(5, 1))], "B");
        await Assert.ThrowsAsync<EndOfStreamException>(() => tooShort.WriteToAsync(new MemoryStream(), Token));
        Assert.Throws<EndOfStreamException>(() => tooShort.WriteTo(new MemoryStream(), Token));
        Assert.Throws<IOException>(() => tooLong.WriteTo(new MemoryStream(), Token));
    }

    [Fact]
    public async Task An_unknown_length_composite_is_not_length_checked()
    {
        var body = RequestBody.Multipart([new MultipartPart("a", RequestBody.FromStream(new MemoryStream([1, 2, 3])))], "B");

        Assert.Equal(-1, body.ContentLength);
        Assert.Contains("\r\n\u0001\u0002\u0003\r\n", await RenderAsync(body), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("a\r\nX-Injected: pwned")]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    public void Part_headers_cannot_break_the_framing(string name)
    {
        // Node strips CR/LF silently; this port rejects, so a field name is never changed behind the caller's back (P3b-8).
        Assert.Throws<ArgumentException>(() => new MultipartPart(name, RequestBody.FromString("x")));
        Assert.Throws<ArgumentException>(() => new MultipartPart("ok", RequestBody.FromString("x"), name));
    }

    [Fact]
    public async Task A_quote_and_backslash_in_a_name_are_escaped_and_do_not_break_the_framing()
    {
        var rendered = await RenderAsync(RequestBody.Multipart([Field("a\"b\\c", "x")], "B"));

        Assert.Contains("name=\"a\\\"b\\\\c\"", rendered, StringComparison.Ordinal);
        var headerBlock = rendered[..rendered.IndexOf("\r\n\r\n", StringComparison.Ordinal)];
        // Three lines of framing and no injected extras: boundary, disposition, and the string part's content type.
        Assert.Equal(3, headerBlock.Split("\r\n").Length);
    }

    [Fact]
    public void An_empty_part_list_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => RequestBody.Multipart([]));
        Assert.Throws<ArgumentNullException>(() => RequestBody.Multipart(null!));
        Assert.Throws<ArgumentNullException>(() => RequestBody.Multipart([null!]));
    }

    [Fact]
    public async Task Nothing_the_composite_was_given_is_disposed()
    {
        var partSource = new DisposeCountingStream(new MemoryStream([1, 2, 3]));
        var destination = new DisposeCountingStream(new MemoryStream());
        var seekablePart = RequestBody.FromStream(partSource, contentLength: 3);
        var body = RequestBody.Multipart([new MultipartPart("a", seekablePart)], "B");

        await body.WriteToAsync(destination, Token);
        body.WriteTo(destination, Token);

        Assert.Equal(0, partSource.DisposeCount);
        Assert.Equal(0, destination.DisposeCount);
        Assert.True(destination.CanWrite);
    }

    [Fact]
    public async Task The_destination_is_not_closed()
    {
        var destination = new DisposeCountingStream(new MemoryStream());

        await RequestBody.Multipart([Field("a", "x")]).WriteToAsync(destination, Token);

        Assert.Equal(0, destination.DisposeCount);
    }

    [Fact]
    public async Task A_failure_in_a_part_propagates_and_stops_the_write()
    {
        var failing = new FailingPartBody();
        var body = RequestBody.Multipart([new MultipartPart("a", failing), Field("b", "never")], "B");
        using var sink = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(() => body.WriteToAsync(sink, Token));

        Assert.DoesNotContain("never", Encoding.UTF8.GetString(sink.ToArray()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sink_failure_propagates_as_the_sink_failure()
    {
        var body = RequestBody.Multipart([Field("a", "x")], "B");
        using var sink = new FailingWriteStream(1, new IOException("SOCKET GONE"));

        var error = await Assert.ThrowsAsync<IOException>(() => body.WriteToAsync(sink, Token));

        Assert.Equal("SOCKET GONE", error.Message);
    }

    [Fact]
    public void Equality_is_identity()
    {
        var first = RequestBody.Multipart([Field("a", "x")], "B");
        var second = RequestBody.Multipart([Field("a", "x")], "B");

        Assert.Equal(first, first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task A_nested_multipart_part_frames_correctly()
    {
        var inner = RequestBody.Multipart([Field("x", "1")], "IN");
        var outer = RequestBody.Multipart([new MultipartPart("nested", inner)], "OUT");

        var rendered = await RenderAsync(outer);

        Assert.Contains("Content-Type: multipart/form-data;boundary=IN\r\n", rendered, StringComparison.Ordinal);
        Assert.Contains("--IN\r\nContent-Disposition: form-data; name=\"x\"", rendered, StringComparison.Ordinal);
        Assert.EndsWith("--IN--\r\n\r\n--OUT--\r\n", rendered, StringComparison.Ordinal);
        using var sink = new MemoryStream();
        await outer.WriteToAsync(sink, Token);
        Assert.Equal(outer.ContentLength, sink.Length);
    }

    [Fact]
    public async Task Declared_length_equals_bytes_written_for_a_spread_of_part_sets()
    {
        string[] names = ["", "a", "café", "a\"b", "back\\slash", new string('n', 300)];
        string[] contents = ["", "x", "é中", new string('c', 5000)];
        foreach (var name in names)
        {
            foreach (var content in contents)
            {
                var body = RequestBody.Multipart([Field(name, content), new MultipartPart(name, RequestBody.FromString(content), name)]);
                using var sink = new MemoryStream();

                await body.WriteToAsync(sink, Token);

                Assert.Equal(body.ContentLength, sink.Length);
            }
        }
    }

    private sealed class FailingPartBody : RequestBody
    {
        public override MediaType? ContentType => null;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("part failed");
    }
}
