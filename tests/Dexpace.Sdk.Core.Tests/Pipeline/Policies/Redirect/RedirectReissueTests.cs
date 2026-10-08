// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>Header stripping and the 303 rebuild (REDIR-5, REDIR-7, REDIR-9, REDIR-10, REDIR-11; P6b-5, P6b-13, P6b-23).</summary>
[Trait("Category", "Unit")]
public sealed class RedirectReissueTests
{
    private static readonly Uri s_target = new("https://h/next");

    private static Request Seed(Method? method = null, RequestBody? body = null, params (string Name, string Value)[] extra)
    {
        var headers = new Headers.Builder()
            .Add("Authorization", "Bearer one")
            .Add("Authorization", "Bearer two")
            .Add("Cookie", "a=b")
            .Add("Cookie", "c=d")
            .Add("Proxy-Authorization", "Basic x")
            .Add("Accept", "*/*");
        foreach (var (name, value) in extra)
        {
            headers.Add(name, value);
        }

        return new Request(method ?? Method.Get, new Uri("https://h/start"), headers.Build(), body);
    }

    [Fact]
    public void Authorization_is_removed_every_value_on_a_same_origin_hop()
    {
        var next = RedirectReissue.Build(Seed(), s_target, 302, crossOrigin: false);

        Assert.Empty(next.Headers.GetAll("Authorization"));
        Assert.Equal("*/*", next.Headers.Get("Accept"));
    }

    [Fact]
    public void Cookie_and_Proxy_Authorization_survive_a_same_origin_hop()
    {
        var next = RedirectReissue.Build(Seed(), s_target, 307, crossOrigin: false);

        Assert.Equal(["a=b", "c=d"], next.Headers.GetAll("Cookie").ToArray());
        Assert.Equal("Basic x", next.Headers.Get("Proxy-Authorization"));
    }

    [Fact]
    public void Cookie_and_Proxy_Authorization_are_removed_every_value_cross_origin()
    {
        var next = RedirectReissue.Build(Seed(), s_target, 307, crossOrigin: true);

        Assert.Empty(next.Headers.GetAll("Authorization"));
        Assert.Empty(next.Headers.GetAll("Cookie"));
        Assert.Empty(next.Headers.GetAll("Proxy-Authorization"));
        Assert.Equal("*/*", next.Headers.Get("Accept"));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    [InlineData("GET")]
    public void A_303_becomes_a_GET_from_any_method_with_no_body(string method)
    {
        var body = method is "POST" or "PUT" or "PATCH" ? RequestBody.FromString("x") : null;

        var next = RedirectReissue.Build(Seed(Method.Of(method), body), s_target, 303, crossOrigin: false);

        Assert.Equal(Method.Get, next.Method);
        Assert.Null(next.Body);
        Assert.Equal(s_target, next.Url);
    }

    [Fact]
    public void A_303_removes_every_Content_header_by_case_insensitive_prefix()
    {
        var seed = Seed(
            Method.Post,
            RequestBody.FromString("x"),
            ("content-type", "text/plain"),
            ("CONTENT-LENGTH", "1"),
            ("cOnTeNt-Language", "en"),
            ("Content-MD5", "abc"),
            ("Content-Disposition", "inline"),
            ("Content-Encoding", "gzip"));

        var next = RedirectReissue.Build(seed, s_target, 303, crossOrigin: false);

        Assert.DoesNotContain(next.Headers.Names, n => n.StartsWith("content-", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("*/*", next.Headers.Get("Accept"));
    }

    [Fact]
    public void A_cross_origin_303_also_loses_Cookie_and_Proxy_Authorization()
    {
        var next = RedirectReissue.Build(Seed(Method.Post, RequestBody.FromString("x")), s_target, 303, crossOrigin: true);

        Assert.Empty(next.Headers.GetAll("Cookie"));
        Assert.Empty(next.Headers.GetAll("Proxy-Authorization"));
        Assert.Empty(next.Headers.GetAll("Authorization"));
    }

    [Theory]
    [InlineData(307)]
    [InlineData(308)]
    [InlineData(301)]
    [InlineData(302)]
    public void A_non_303_keeps_method_and_the_same_body_instance(int status)
    {
        var body = RequestBody.FromString("payload");
        var seed = Seed(Method.Post, body);

        var next = RedirectReissue.Build(seed, s_target, status, crossOrigin: false);

        Assert.Equal(Method.Post, next.Method);
        Assert.Same(body, next.Body);
    }

    [Fact]
    public void A_non_303_keeps_Content_headers()
    {
        var seed = Seed(Method.Post, RequestBody.FromString("x"), ("Content-Type", "text/plain"), ("Content-MD5", "abc"));

        var next = RedirectReissue.Build(seed, s_target, 307, crossOrigin: false);

        Assert.Equal("text/plain", next.Headers.Get("Content-Type"));
        Assert.Equal("abc", next.Headers.Get("Content-MD5"));
    }

    [Theory]
    [InlineData(302, false)]
    [InlineData(303, true)]
    [InlineData(307, true)]
    public void Nothing_is_added_to_the_request(int status, bool crossOrigin)
    {
        var seed = Seed();

        var next = RedirectReissue.Build(seed, s_target, status, crossOrigin);

        Assert.All(next.Headers.Names, name => Assert.True(seed.Headers.Contains(name), $"{name} was added"));
        Assert.False(next.Headers.Contains("Referer"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_marker_shaped_header_is_an_ordinary_header(bool crossOrigin)
    {
        // P6b-23 (REDIR-11): the reference's internal marker is retired; a header spelled like it is not interpreted.
        var seed = Seed(extra: ("x-dexpace-internal-redirect-cross-origin", "1"));

        var next = RedirectReissue.Build(seed, s_target, 302, crossOrigin);

        Assert.Equal("1", next.Headers.Get("x-dexpace-internal-redirect-cross-origin"));
    }

    [Fact]
    public void The_rebuild_uses_one_constructor_call()
    {
        // HTTP-7: a GET with a body does not exist, so a 303 over a POST body must not go through With* helpers.
        var exception = Record.Exception(() =>
            RedirectReissue.Build(Seed(Method.Post, RequestBody.FromString("x")), s_target, 303, crossOrigin: true));

        Assert.Null(exception);
    }
}
