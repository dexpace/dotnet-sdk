// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>The exact-length stream request body and <c>FromStream</c> validation: HTTP-39, IO-3, IO-17, IO-40.</summary>
[Trait("Category", "Unit")]
public class RequestBodyStreamTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] Bytes(int length) => Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();

    [Fact]
    public async Task A_known_length_stream_body_writes_exactly_that_many_bytes_from_a_longer_source()
    {
        var data = Bytes(100);
        var body = RequestBody.FromStream(new MemoryStream(data), contentLength: 40);
        using var destination = new MemoryStream();

        await body.WriteToAsync(destination, Token);

        Assert.Equal(data.AsSpan(0, 40).ToArray(), destination.ToArray());
    }

    [Fact]
    public async Task A_known_length_stream_body_over_a_short_source_throws_EndOfStreamException_naming_both_numbers()
    {
        var body = RequestBody.FromStream(new MemoryStream(Bytes(6)), contentLength: 10);
        using var destination = new MemoryStream();

        var error = await Assert.ThrowsAsync<EndOfStreamException>(() => body.WriteToAsync(destination, Token));

        Assert.Equal("The source ended after 6 of 10 bytes.", error.Message);
    }

    [Fact]
    public async Task A_zero_length_stream_body_issues_no_read()
    {
        using var source = new StrictReadStream(new MemoryStream(Bytes(10)));
        var body = RequestBody.FromStream(source, contentLength: 0);
        using var destination = new MemoryStream();

        await body.WriteToAsync(destination, Token);

        Assert.False(source.AnyRead);
        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task An_unknown_length_stream_body_copies_to_the_end()
    {
        // Pin: the as-built behaviour.
        var data = Bytes(200_000);
        var body = RequestBody.FromStream(new MemoryStream(data));
        using var destination = new MemoryStream();

        await body.WriteToAsync(destination, Token);

        Assert.Equal(data, destination.ToArray());
    }

    [Fact]
    public void FromStream_rejects_a_content_length_below_minus_one()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(
            () => RequestBody.FromStream(new MemoryStream(), contentLength: -2));

        Assert.Equal("contentLength", error.ParamName);
    }

    [Fact]
    public void FromStream_rejects_a_non_readable_source()
    {
        using var source = new FailingWriteStream(1, new IOException("unused"));

        var error = Assert.Throws<ArgumentException>(() => RequestBody.FromStream(source));

        Assert.Equal("source", error.ParamName);
    }

    [Fact]
    public async Task Writing_a_stream_body_twice_still_throws_StreamConsumedException()
    {
        var body = RequestBody.FromStream(new MemoryStream(Bytes(5)));
        using var first = new MemoryStream();
        await body.WriteToAsync(first, Token);

        using var second = new MemoryStream();
        await Assert.ThrowsAsync<StreamConsumedException>(() => body.WriteToAsync(second, Token));

        // The latch is flipped before any I/O, so a failed first write also consumes the body.
        var failing = RequestBody.FromStream(new MemoryStream(Bytes(5)));
        using var broken = new FailingWriteStream(1, new IOException("boom"));
        await Assert.ThrowsAsync<IOException>(() => failing.WriteToAsync(broken, Token));
        await Assert.ThrowsAsync<StreamConsumedException>(() => failing.WriteToAsync(second, Token));
    }

    [Fact]
    public async Task A_cancelled_token_stops_the_exact_copy()
    {
        var body = RequestBody.FromStream(new MemoryStream(Bytes(100)), contentLength: 100);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<OperationCanceledException>(() => body.WriteToAsync(destination, cts.Token));
    }
}
