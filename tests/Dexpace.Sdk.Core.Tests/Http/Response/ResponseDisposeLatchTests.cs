// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The responses and bodies under test are released by the assertions.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>The <see cref="Response"/> dispose latch: HTTP-43 (design P3b-2).</summary>
[Trait("Category", "Unit")]
public class ResponseDisposeLatchTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class ThrowingBody : ResponseBody
    {
        private int _releases;

        public int Releases => Volatile.Read(ref _releases);

        public override MediaType? ContentType => null;

        public override Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream());

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref _releases);
            base.Dispose(disposing);
            throw new InvalidOperationException("release failed");
        }
    }

    [Fact]
    public async Task Double_dispose_releases_once()
    {
        var syncSync = new DisposalCountingBody();
        var syncResponse = TestResponses.Create(Status.Ok, body: syncSync);
        syncResponse.Dispose();
        syncResponse.Dispose();
        Assert.Equal(1, syncSync.DisposeCount);

        var asyncAsync = new DisposalCountingBody();
        var asyncResponse = TestResponses.Create(Status.Ok, body: asyncAsync);
        await asyncResponse.DisposeAsync();
        await asyncResponse.DisposeAsync();
        Assert.Equal(1, asyncAsync.DisposeCount);

        var mixed = new DisposalCountingBody();
        var mixedResponse = TestResponses.Create(Status.Ok, body: mixed);
        mixedResponse.Dispose();
        await mixedResponse.DisposeAsync();
        Assert.Equal(1, mixed.DisposeCount);

        var reversed = new DisposalCountingBody();
        var reversedResponse = TestResponses.Create(Status.Ok, body: reversed);
        await reversedResponse.DisposeAsync();
        reversedResponse.Dispose();
        Assert.Equal(1, reversed.DisposeCount);
    }

    [Fact]
    public async Task A_throwing_body_propagates_once_and_the_second_call_is_a_no_op()
    {
        var body = new ThrowingBody();
        var response = TestResponses.Create(Status.Ok, body: body);

        Assert.Throws<InvalidOperationException>(response.Dispose);
        response.Dispose();
        await response.DisposeAsync();

        Assert.Equal(1, body.Releases);
    }

    [Fact]
    public async Task A_throwing_body_propagates_once_from_the_async_form_too()
    {
        var body = new ThrowingBody();
        var response = TestResponses.Create(Status.Ok, body: body);

        await Assert.ThrowsAsync<InvalidOperationException>(() => response.DisposeAsync().AsTask());
        await response.DisposeAsync();
        response.Dispose();

        Assert.Equal(1, body.Releases);
    }

    [Fact]
    public void A_never_read_body_is_released_by_response_dispose()
    {
        var body = new DisposalCountingBody();
        TestResponses.Create(Status.Ok, body: body).Dispose();
        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task Concurrent_response_disposes_release_once()
    {
        const int Contenders = 16;
        var body = new DisposalCountingBody();
        var response = TestResponses.Create(Status.Ok, body: body);
        using var barrier = new Barrier(Contenders);

        await Task.WhenAll(Enumerable.Range(0, Contenders).Select(i => Task.Run(
            async () =>
            {
                barrier.SignalAndWait(Token);
                if (i % 2 == 0)
                {
                    response.Dispose();
                }
                else
                {
                    await response.DisposeAsync();
                }
            },
            Token)));

        Assert.Equal(1, body.DisposeCount);
    }

    [Fact]
    public async Task EnsureSuccessAsync_on_an_error_status_then_a_caller_dispose_releases_once()
    {
        // A pin: EnsureSuccessAsync disposes the response in its finally; the caller's own dispose is absorbed by the latch.
        var body = new DisposalCountingBody();
        var response = TestResponses.Create(Status.InternalServerError, body: body);

        await Assert.ThrowsAsync<HttpResponseException>(() => response.EnsureSuccessAsync(Token).AsTask());
        response.Dispose();
        await response.DisposeAsync();

        Assert.Equal(1, body.DisposeCount);
    }
}
