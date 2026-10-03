// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/body/stream-body.test.ts (ownership) and
// packages/body-file/src/file-body.test.ts (the handle), plus the multipart no-dispose rule.

#pragma warning disable CA2000 // The streams under test are owned by the test.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.TestSupport.Streams;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>
/// BODY-8 and design §10 entry 5 (P3b-1): a request body closes exactly what it opened. The caller's stream stays open
/// after a write, a failed write and <c>ToReplayableAsync</c>; the file and multipart halves are added with those bodies.
/// </summary>
[Trait("Category", "Unit")]
public class RequestBodyOwnershipTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly byte[] s_payload = [1, 2, 3, 4];

    public static TheoryData<bool> SeekableOrNot => [false, true];

    private static RequestBody Wrap(DisposeCountingStream source, bool seekable) =>
        seekable
            ? RequestBody.FromStream(source, contentLength: s_payload.Length)
            : RequestBody.FromStream(source);

    [Theory]
    [MemberData(nameof(SeekableOrNot))]
    public async Task The_callers_stream_is_open_after_a_write_a_failed_write_and_ToReplayableAsync(bool seekable)
    {
        var source = new DisposeCountingStream(new MemoryStream(s_payload));
        var body = Wrap(source, seekable);
        using var failing = new FailingWriteStream(1, new IOException("sink failed"));

        await Assert.ThrowsAsync<IOException>(() => body.WriteToAsync(failing, Token));
        Assert.Equal(0, source.DisposeCount);
        Assert.True(source.CanRead);

        var secondSource = new DisposeCountingStream(new MemoryStream(s_payload));
        var secondBody = Wrap(secondSource, seekable);
        await secondBody.WriteToAsync(new MemoryStream(), Token);
        Assert.Equal(0, secondSource.DisposeCount);

        var thirdSource = new DisposeCountingStream(new MemoryStream(s_payload));
        _ = await Wrap(thirdSource, seekable).ToReplayableAsync(Token);
        Assert.Equal(0, thirdSource.DisposeCount);
    }

    [Fact]
    public async Task A_single_use_body_over_a_consumed_stream_still_does_not_dispose_it()
    {
        var source = new DisposeCountingStream(new MemoryStream(s_payload));
        var body = RequestBody.FromStream(source);
        await body.WriteToAsync(new MemoryStream(), Token);

        await Assert.ThrowsAsync<StreamConsumedException>(() => body.WriteToAsync(new MemoryStream(), Token));

        Assert.Equal(0, source.DisposeCount);
    }
}
