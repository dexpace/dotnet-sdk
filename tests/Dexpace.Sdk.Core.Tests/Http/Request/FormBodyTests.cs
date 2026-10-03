// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/body/simple-bodies.test.ts: the form cases (re-derived under WHATWG, see
// tests/vectors/body/form-urlencoded.json) and the sink-failure cases. The `freeze` cases are host facts (roadmap
// constraint 10) and are not ported.

using System.Text;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary><see cref="RequestBody.FromForm"/>: HTTP-36, HTTP-38, BODY-1, BODY-3 (design P3b-7, P3b-14).</summary>
[Trait("Category", "Unit")]
public class FormBodyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<byte[]> WriteAsync(RequestBody body)
    {
        using var sink = new MemoryStream();
        await body.WriteToAsync(sink, Token);
        return sink.ToArray();
    }

    [Fact]
    public async Task FromForm_is_replayable_with_the_exact_length()
    {
        var body = RequestBody.FromForm([new("a b", "c&d")]);

        Assert.True(body.IsReplayable);
        Assert.Equal(Encoding.ASCII.GetBytes("a+b=c%26d"), await WriteAsync(body));
        Assert.Equal(body.ContentLength, (await WriteAsync(body)).Length);
    }

    [Fact]
    public void The_content_type_is_application_x_www_form_urlencoded_with_no_charset()
    {
        var body = RequestBody.FromForm([new("k", "v")]);

        Assert.Equal(CommonMediaTypes.ApplicationFormUrlEncoded, body.ContentType);
        Assert.Null(body.ContentType!.Charset);
        Assert.Equal("application/x-www-form-urlencoded", body.ContentType.ToString());
    }

    [Fact]
    public async Task Two_writes_are_byte_identical()
    {
        var body = RequestBody.FromForm([new("k", "é"), new("k", "~")]);

        Assert.Equal(await WriteAsync(body), await WriteAsync(body));
    }

    [Fact]
    public async Task ToReplayableAsync_returns_the_same_instance()
    {
        var body = RequestBody.FromForm([new("k", "v")]);

        Assert.Same(body, await body.ToReplayableAsync(Token));
        Assert.Same(body, body.ToReplayable(Token));
    }

    [Fact]
    public void Equal_fields_give_equal_bodies_and_hash_in_O1()
    {
        var first = RequestBody.FromForm([new("k", "v")]);
        var second = RequestBody.FromForm([new("k", "v")]);
        var other = RequestBody.FromForm([new("k", "w")]);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, other);
    }

    [Fact]
    public async Task A_Dictionary_and_a_list_of_pairs_are_both_accepted()
    {
        var fromDictionary = RequestBody.FromForm(new Dictionary<string, string> { ["a"] = "1" });
        var fromList = RequestBody.FromForm(new List<KeyValuePair<string, string>> { new("a", "1") });

        Assert.Equal(await WriteAsync(fromDictionary), await WriteAsync(fromList));
    }

    [Fact]
    public void A_null_fields_argument_throws_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => RequestBody.FromForm(null!));

    [Fact]
    public async Task The_sync_twin_writes_the_same_bytes()
    {
        var body = RequestBody.FromForm([new("k", "v v")]);
        using var sink = new MemoryStream();

        body.WriteTo(sink, Token);

        Assert.Equal(await WriteAsync(body), sink.ToArray());
    }

    [Fact]
    public async Task A_throwing_destination_propagates_for_FromBytes()
    {
        var body = RequestBody.FromBytes(new byte[] { 1, 2, 3 });
        using var asyncSink = new FailingWriteStream(1, new IOException("sink failed"));
        using var syncSink = new FailingWriteStream(1, new IOException("sink failed"));

        await Assert.ThrowsAnyAsync<IOException>(() => body.WriteToAsync(asyncSink, Token));
        Assert.ThrowsAny<IOException>(() => body.WriteTo(syncSink, Token));
    }

    [Fact]
    public async Task A_throwing_destination_propagates_for_FromString()
    {
        var body = RequestBody.FromString("text");
        using var asyncSink = new FailingWriteStream(1, new IOException("sink failed"));
        using var syncSink = new FailingWriteStream(1, new IOException("sink failed"));

        await Assert.ThrowsAnyAsync<IOException>(() => body.WriteToAsync(asyncSink, Token));
        Assert.ThrowsAny<IOException>(() => body.WriteTo(syncSink, Token));
    }

    [Fact]
    public async Task A_throwing_destination_propagates_for_FromForm()
    {
        var body = RequestBody.FromForm([new("k", "v")]);
        using var asyncSink = new FailingWriteStream(1, new IOException("sink failed"));
        using var syncSink = new FailingWriteStream(1, new IOException("sink failed"));

        await Assert.ThrowsAnyAsync<IOException>(() => body.WriteToAsync(asyncSink, Token));
        Assert.ThrowsAny<IOException>(() => body.WriteTo(syncSink, Token));
    }
}
