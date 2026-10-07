// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

#pragma warning disable CA2000 // The responses and bodies under test are released by the assertions.

using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Tests.Execution;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>BODY-34, CTX-9, P5b-13: the internal ownership-moving body swap.</summary>
[Trait("Category", "Unit")]
public sealed class ResponseReplaceBodyTests
{
    private static Response Original(DisposalCountingBody body, string? reason = "OK") =>
        TestResponses.Create(
            Status.Created,
            headers: new Headers.Builder().Add("X-One", "1").Build(),
            body: body,
            protocol: Protocol.Http2,
            reasonPhrase: reason);

    [Fact]
    public void The_new_response_owns_the_new_body_and_keeps_status_headers_protocol_and_reason()
    {
        var oldBody = new DisposalCountingBody();
        var newBody = new DisposalCountingBody();
        var original = Original(oldBody);

        var next = original.ReplaceBody(newBody);

        Assert.NotSame(original, next);
        Assert.Same(newBody, next.Body);
        Assert.Same(original.Request, next.Request);
        Assert.Equal(Status.Created, next.Status);
        Assert.Same(original.Headers, next.Headers);
        Assert.Equal(Protocol.Http2, next.Protocol);
        Assert.Equal("OK", next.ReasonPhrase);
    }

    [Fact]
    public async Task Disposing_the_original_afterwards_does_not_dispose_either_body()
    {
        var oldBody = new DisposalCountingBody();
        var newBody = new DisposalCountingBody();
        var original = Original(oldBody);
        var next = original.ReplaceBody(newBody);

        original.Dispose();
        await original.DisposeAsync();

        Assert.Equal(0, oldBody.DisposeCount);
        Assert.Equal(0, newBody.DisposeCount);
        next.Dispose();
    }

    [Fact]
    public void Disposing_the_new_response_disposes_the_new_body_exactly_once_and_never_the_old_directly()
    {
        var oldBody = new DisposalCountingBody();
        var wrapper = new LoggingResponseBody(oldBody, 16);
        var original = Original(oldBody);
        var next = original.ReplaceBody(wrapper);

        original.Dispose();
        next.Dispose();
        next.Dispose();

        // The wrapper owns the old body and releases it itself, once.
        Assert.Equal(1, oldBody.DisposeCount);
    }

    [Fact]
    public void Exchange_links_move_to_the_new_response()
    {
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store);
        var request = ExecutionTestSupport.NewRequest();
        var original = ExecutionTestSupport.NewResponse(request);
        var exchange = dispatch.PromoteToRequest(request).PromoteToExchange(original);
        original.AttachExchange(exchange);
        Assert.Equal(1, store.Count);

        var next = original.ReplaceBody(ResponseBody.FromBytes(new byte[] { 1, 2, 3 }));
        original.Dispose();

        Assert.Equal(1, store.Count);

        next.Dispose();
        Assert.Equal(0, store.Count);
        next.Dispose();
    }

    [Fact]
    public void Replacing_the_body_of_a_disposed_response_throws()
    {
        var original = Original(new DisposalCountingBody());
        original.Dispose();

        Assert.Throws<ObjectDisposedException>(() => original.ReplaceBody(ResponseBody.FromBytes(new byte[] { 1 })));
    }

    [Fact]
    public void WithBody_is_unchanged()
    {
        var store = new ContextStore(10);
        var dispatch = ExecutionTestSupport.NewDispatch(store);
        var request = ExecutionTestSupport.NewRequest();
        var oldBody = new DisposalCountingBody();
        var original = ExecutionTestSupport.NewResponse(request, oldBody);
        original.AttachExchange(dispatch.PromoteToRequest(request).PromoteToExchange(original));

        var copy = original.WithBody(ResponseBody.FromBytes(new byte[] { 1 }));
        copy.Dispose();

        // The public member leaves the original to be disposed and does not move the link.
        Assert.Equal(1, store.Count);
        original.Dispose();
        Assert.Equal(0, store.Count);
        Assert.Equal(1, oldBody.DisposeCount);
    }

    [Fact]
    public async Task The_async_dispose_latch_is_shared()
    {
        var newBody = new DisposalCountingBody();
        var next = Original(new DisposalCountingBody()).ReplaceBody(newBody);

        next.Dispose();
        await next.DisposeAsync();

        Assert.Equal(1, newBody.DisposeCount);
    }
}
