// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Wire;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// TRANSPORT-10 (phase 8b, P8b-11; 8a's F7) at the wire: the caller's <c>Content-Type</c> header is the one on the socket, once, and
/// the body's media type is used only when the caller set none. A body-less <c>POST</c> that carries one is sent with
/// <c>Content-Length: 0</c>, exactly as it is without it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ContentTypeWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<RecordedRequest> SendAsync(string face, Func<LoopbackServer, Request> build)
    {
        await using var server = LoopbackServer.Start(_ => LoopbackResponse.Ok("ok", keepAlive: true));
        await using var transport = new SystemNetHttpClient();
        var request = build(server);

        using var response = face == "async"
            ? await transport.ExecuteAsync(request, RequestOptions.Empty, Ct)
            : await Task.Run(() => transport.Execute(request, RequestOptions.Empty, Ct), Ct);

        return Assert.Single(server.Requests);
    }

    public static TheoryData<string> Faces() => ["async", "sync"];

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_caller_type_over_a_typed_body_is_the_one_on_the_wire(string face)
    {
        var recorded = await SendAsync(face, server => Request
            .Post(server.Url("/a").AbsoluteUri, RequestBody.FromString("{}", CommonMediaTypes.ApplicationJson))
            .WithHeader("Content-Type", "application/vnd.x+json"));

        Assert.Equal(["application/vnd.x+json"], recorded.HeaderValues("Content-Type"));
        Assert.Equal("{}", recorded.BodyText);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_typed_body_with_no_caller_type_sends_the_bodys(string face)
    {
        var recorded = await SendAsync(face, server => Request.Post(server.Url("/b").AbsoluteUri, RequestBody.FromString("{}", CommonMediaTypes.ApplicationJson)));

        var values = recorded.HeaderValues("Content-Type");
        Assert.StartsWith("application/json", Assert.Single(values), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_caller_type_over_an_untyped_body_is_sent(string face)
    {
        var recorded = await SendAsync(face, server => Request
            .Post(server.Url("/c").AbsoluteUri, RequestBody.FromBytes("{}"u8.ToArray()))
            .WithHeader("Content-Type", "application/vnd.y"));

        Assert.Equal(["application/vnd.y"], recorded.HeaderValues("Content-Type"));
    }

    [Theory]
    [MemberData(nameof(Faces))]
    public async Task A_body_less_post_with_a_caller_type_sends_it_with_content_length_zero(string face)
    {
        var recorded = await SendAsync(face, server => Request
            .Create(Method.Post, server.Url("/d").AbsoluteUri)
            .WithHeader("Content-Type", "application/vnd.y"));

        Assert.Equal(["application/vnd.y"], recorded.HeaderValues("Content-Type"));
        Assert.Equal(["0"], recorded.HeaderValues("Content-Length"));
        Assert.Empty(recorded.Body.ToArray());
    }
}
