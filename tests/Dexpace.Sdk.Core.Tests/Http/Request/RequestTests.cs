// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Requests;

/// <summary>
/// The phase 2a rows for <see cref="Request"/>: HTTP-2, HTTP-3, HTTP-4, HTTP-6, HTTP-7, HTTP-8 (N/A), HTTP-46,
/// HTTP-47 and SEAM-29 (the Request half). The older Request tests stay in <c>BodiesAndRequestTests</c>.
/// </summary>
[Trait("Category", "Unit")]
public class RequestTests
{
    private const string Url = "https://example.test/items";

    public static TheoryData<string> BodylessMethods => ["GET", "HEAD", "TRACE", "CONNECT"];

    public static TheoryData<string> BodyAcceptingMethods => ["POST", "PUT", "PATCH", "DELETE", "OPTIONS"];

    public static TheoryData<string> BadUrls => ["::bad", "/rel", "ftp://h/x", "file:///etc/passwd"];

    private static RequestBody Body() => RequestBody.FromString("{}");

    // ---- Construction, required fields, forbidden bodies, URL errors ----

    [Theory]
    [InlineData("method")]
    [InlineData("url")]
    [InlineData("createUrl")]
    public void A_missing_required_field_throws_ArgumentNullException_with_its_name(string field)
    {
        // HTTP-8 (a request with no method set) is N/A: "no method" is unrepresentable, because method is a required
        // constructor parameter and null is rejected here. See design section 4.2.
        var error = field switch
        {
            "method" => Assert.Throws<ArgumentNullException>(() => new Request(null!, new Uri(Url))),
            "url" => Assert.Throws<ArgumentNullException>(() => new Request(Method.Get, null!)),
            _ => Assert.Throws<ArgumentNullException>(() => Request.Create(Method.Get, null!)),
        };

        Assert.Equal(field == "createUrl" ? "url" : field, error.ParamName);
    }

    [Fact]
    public void A_request_carries_exactly_method_url_headers_and_body()
    {
        var properties = typeof(Request)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !p.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false))
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Body", "Headers", "Method", "Url"], properties);
    }

    [Fact]
    public void Headers_is_never_null_and_Body_is_nullable()
    {
        var request = Request.Get(Url);

        Assert.NotNull(request.Headers);
        Assert.Empty(request.Headers);
        Assert.Null(request.Body);
        Assert.Same(Headers.Empty, new Request(Method.Get, new Uri(Url), null).Headers);
    }

    [Theory]
    [MemberData(nameof(BodylessMethods))]
    public void A_body_on_GET_HEAD_TRACE_CONNECT_is_rejected(string token)
    {
        var method = Method.Of(token);

        var viaConstructor = Assert.Throws<ArgumentException>(() => new Request(method, new Uri(Url), null, Body()));
        var viaCreate = Assert.Throws<ArgumentException>(() => Request.Create(method, Url, body: Body()));
        var viaWithMethod = Assert.Throws<ArgumentException>(
            () => Request.Create(Method.Post, Url, body: Body()).WithMethod(method));
        var viaWithBody = Assert.Throws<ArgumentException>(() => Request.Create(method, Url).WithBody(Body()));

        foreach (var error in new[] { viaConstructor, viaCreate, viaWithMethod, viaWithBody })
        {
            Assert.Equal("body", error.ParamName);
            Assert.Contains(token, error.Message, StringComparison.Ordinal);
            Assert.Contains("clear the body first", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Deriving_a_request_cannot_install_a_body_carrying_GET()
    {
        // The spec's own HTTP-2 example: a POST with a body, WithMethod(Get), throws; clearing the body first works.
        var post = Request.Post(Url, Body());

        Assert.Throws<ArgumentException>(() => post.WithMethod(Method.Get));
        var get = post.WithoutBody().WithMethod(Method.Get);

        Assert.Equal(Method.Get, get.Method);
        Assert.Null(get.Body);
    }

    [Theory]
    [MemberData(nameof(BodyAcceptingMethods))]
    public void POST_PUT_PATCH_DELETE_OPTIONS_accept_a_body(string token)
    {
        var request = Request.Create(Method.Of(token), Url, body: Body());

        Assert.NotNull(request.Body);
    }

    [Theory]
    [MemberData(nameof(BadUrls))]
    public void A_malformed_relative_non_http_or_file_url_is_rejected(string input)
    {
        // The Linux "/rel" trap: Uri.TryCreate("/rel", Absolute) yields file:///rel, which the scheme test rejects.
        var viaCreate = Assert.Throws<ArgumentException>(() => Request.Create(Method.Get, input));
        Assert.Equal("url", viaCreate.ParamName);

        // A plain new Uri("::bad") would throw UriFormatException before the constructor runs.
        var viaConstructor = Assert.Throws<ArgumentException>(
            () => new Request(Method.Get, new Uri(input, UriKind.RelativeOrAbsolute)));
        Assert.Equal("url", viaConstructor.ParamName);
    }

    [Theory]
    [MemberData(nameof(BadUrls))]
    public void The_url_error_carries_the_redacted_input(string input)
    {
        // RULED (P2a-2): the message carries the input as UrlRedactor renders it, computed so a redactor change
        // cannot drift from this test. For these inputs (no userinfo, no query) that is the input itself.
        var expected = new UrlRedactor().Redact(input);

        var error = Assert.Throws<ArgumentException>(() => Request.Create(Method.Get, input));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_password_in_the_url_never_reaches_the_message()
    {
        var malformed = Assert.Throws<ArgumentException>(() => Request.Create(Method.Get, "https://user:secret@h:bad/"));
        var wrongScheme = Assert.Throws<ArgumentException>(
            () => Request.Create(Method.Get, "ftp://user:secret@h/x?token=hunter2"));
        var viaConstructor = Assert.Throws<ArgumentException>(
            () => new Request(Method.Get, new Uri("ftp://user:secret@h/x?token=hunter2")));

        foreach (var error in new[] { malformed, wrongScheme, viaConstructor })
        {
            Assert.DoesNotContain("secret", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("hunter2", error.Message, StringComparison.Ordinal);
        }
    }

    // ---- With* derivations ----

    [Fact]
    public void Each_With_method_returns_a_new_request_and_leaves_the_original_unchanged()
    {
        var original = Request.Create(Method.Post, Url, Headers.Empty.With("A", "1"), Body());

        var viaMethod = original.WithMethod(Method.Put);
        var viaUrl = original.WithUrl(new Uri("https://other.test/x"));
        var viaHeaders = original.WithHeaders(Headers.Empty.With("B", "2"));
        var viaStringHeader = original.WithHeader("C", "3");
        var viaTypedHeader = original.WithHeader(HttpHeaderName.Of("D"), "4");
        var newBody = RequestBody.FromString("other");
        var viaBody = original.WithBody(newBody);
        var viaNoBody = original.WithoutBody();

        Assert.Equal(Method.Put, viaMethod.Method);
        Assert.Equal("https://other.test/x", viaUrl.Url.AbsoluteUri);
        Assert.False(viaHeaders.Headers.Contains("A"));
        Assert.Equal("2", viaHeaders.Headers.Get("B"));
        Assert.Equal((string[])["1"], viaStringHeader.Headers.GetAll("A"));
        Assert.Equal("3", viaStringHeader.Headers.Get("C"));
        Assert.Equal("4", viaTypedHeader.Headers.Get("d"));
        Assert.Same(newBody, viaBody.Body);
        Assert.Null(viaNoBody.Body);

        Assert.Equal(Method.Post, original.Method);
        Assert.Equal(Url, original.Url.AbsoluteUri);
        Assert.Equal(["A"], original.Headers.Names);
        Assert.NotNull(original.Body);
        foreach (var derived in new[] { viaMethod, viaUrl, viaHeaders, viaStringHeader, viaTypedHeader, viaBody, viaNoBody })
        {
            Assert.NotSame(original, derived);
        }
    }

    [Fact]
    public void WithoutBody_on_a_bodiless_request_returns_the_same_instance()
    {
        var request = Request.Get(Url);

        Assert.Same(request, request.WithoutBody());
    }

    [Fact]
    public void WithUrl_validates_like_the_constructor()
    {
        var request = Request.Get(Url);

        Assert.Throws<ArgumentException>(() => request.WithUrl(new Uri("ftp://example.test/x")));
        Assert.Throws<ArgumentException>(() => request.WithUrl(new Uri("/relative", UriKind.Relative)));
        Assert.Throws<ArgumentNullException>(() => request.WithUrl(null!));
    }

    [Fact]
    public void Every_With_routes_through_the_constructor()
    {
        var get = Request.Get(Url);

        Assert.Throws<ArgumentException>(() => get.WithBody(Body()));
        Assert.Throws<ArgumentNullException>(() => get.WithHeaders(null!));
        Assert.Throws<ArgumentNullException>(() => get.WithMethod(null!));
        Assert.Throws<ArgumentNullException>(() => get.WithBody(null!));
        Assert.Throws<ArgumentException>(() => get.WithHeader("bad name", "v"));
        Assert.Throws<ArgumentException>(() => get.WithHeader(HttpHeaderName.Of("X-A"), "a\r\nb"));
    }

    // ---- Equality (HTTP-46, position A) ----

    [Fact]
    public void Equal_text_gives_equal_requests_and_hashes()
    {
        var a = Request.Get(Url);
        var b = Request.Get(Url);
        var postA = Request.Post(Url, RequestBody.FromString("{}"));
        var postB = Request.Post(Url, RequestBody.FromString("{}"));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(postA, postB);
        Assert.Equal(postA.GetHashCode(), postB.GetHashCode());
        Assert.True(postA == postB);
        Assert.NotEqual(a, postA);

        var map = new Dictionary<Request, string> { [postA] = "found" };
        Assert.Equal("found", map[postB]);
    }

    [Fact]
    public void Userinfo_and_fragment_count()
    {
        Assert.NotEqual(Request.Get("https://a@h/"), Request.Get("https://b@h/"));
        Assert.NotEqual(Request.Get("https://h/p"), Request.Get("https://h/p#frag"));
        Assert.NotEqual(Request.Get("https://h/p#a"), Request.Get("https://h/p#b"));
        Assert.Equal(Request.Get("https://h/p?q=1"), Request.Get("https://h/p?q=1"));
        Assert.NotEqual(Request.Get("https://h/p?q=1"), Request.Get("https://h/p?q=2"));
    }

    [Fact]
    public void Host_names_are_not_resolved()
    {
        Assert.NotEqual(Request.Get("http://localhost/"), Request.Get("http://127.0.0.1/"));
    }

    [Fact]
    public void Header_value_equality_is_used()
    {
        var a = Request.Get(Url).WithHeader("X-Trace", "1");
        var b = Request.Get(Url).WithHeader("x-trace", "1");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Request.Get(Url).WithHeader("X-Trace", "2"));
    }

    [Fact]
    public void Different_bytes_or_different_content_types_are_unequal()
    {
        var json = MediaType.Parse("application/json");
        var plain = MediaType.Parse("text/plain");

        Assert.NotEqual(
            Request.Post(Url, RequestBody.FromString("a")),
            Request.Post(Url, RequestBody.FromString("b")));
        Assert.NotEqual(
            Request.Post(Url, RequestBody.FromBytes("x"u8.ToArray(), json)),
            Request.Post(Url, RequestBody.FromBytes("x"u8.ToArray(), plain)));
        Assert.NotEqual(
            Request.Post(Url, RequestBody.FromBytes("x"u8.ToArray(), json)),
            Request.Post(Url, RequestBody.FromBytes("x"u8.ToArray())));
    }

    [Fact]
    public void FromBytes_and_FromString_over_the_same_bytes_are_equal()
    {
        var contentType = MediaType.Parse("text/plain; charset=utf-8");

        var fromString = Request.Post(Url, RequestBody.FromString("héllo", contentType));
        var fromBytes = Request.Post(Url, RequestBody.FromBytes(System.Text.Encoding.UTF8.GetBytes("héllo"), contentType));

        Assert.Equal(fromString, fromBytes);
        Assert.Equal(fromString.GetHashCode(), fromBytes.GetHashCode());
    }

    [Fact]
    public void Two_FromStream_bodies_over_identical_MemoryStreams_are_unequal()
    {
        var first = Request.Post(Url, RequestBody.FromStream(new MemoryStream([1, 2, 3])));
        var second = Request.Post(Url, RequestBody.FromStream(new MemoryStream([1, 2, 3])));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_request_is_equal_to_itself_with_a_stream_body()
    {
        var request = Request.Post(Url, RequestBody.FromStream(new MemoryStream([1, 2, 3])));

        Assert.Equal(request, request);
        Assert.True(request.Equals(request));
        Assert.Equal(request.GetHashCode(), request.GetHashCode());
    }

    [Fact]
    public async Task A_replayable_copy_equals_a_FromBytes_body()
    {
        var contentType = MediaType.Parse("application/octet-stream");
        var streamBody = RequestBody.FromStream(new MemoryStream([9, 8, 7]), contentType);

        var replayable = await streamBody.ToReplayableAsync(TestContext.Current.CancellationToken);

        Assert.Equal(Request.Post(Url, RequestBody.FromBytes(new byte[] { 9, 8, 7 }, contentType)), Request.Post(Url, replayable));
    }

    [Fact]
    public void An_unknown_RequestBody_subclass_keeps_reference_equality()
    {
        var one = new ForeignBody();
        var two = new ForeignBody();

        Assert.Equal(Request.Post(Url, one), Request.Post(Url, one));
        Assert.NotEqual(Request.Post(Url, one), Request.Post(Url, two));
    }

    [Fact]
    public void Equality_handles_null_and_other_types()
    {
        var request = Request.Get(Url);

        Assert.False(request.Equals((Request?)null));
        Assert.False(request.Equals((object?)"x"));
        Assert.True(request != null);
        Assert.True((Request?)null == null);
    }

    // ---- ToString ----

    [Fact]
    public void ToString_is_method_and_redacted_url()
    {
        var request = Request.Get("https://u:p@h/p?token=abc&api-version=1");

        var expected = $"GET {new UrlRedactor().Redact(request.Url)}";

        Assert.Equal(expected, request.ToString());
        Assert.Equal("GET https://***:***@h/p?token=***&api-version=1", request.ToString());
    }

    [Fact]
    public void ToString_never_contains_header_values_or_the_body()
    {
        var request = Request.Post(Url, RequestBody.FromString("body-secret"))
            .WithHeader("Authorization", "Bearer header-secret");

        var text = request.ToString();

        Assert.DoesNotContain("header-secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("body-secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_of_a_malformed_url_cannot_occur()
    {
        // Pin: the constructor guarantees an absolute http(s) Uri, so ToString never has a malformed URL to render.
        Assert.All(["http://h/", "https://h/p?q=1#f"], u => Assert.StartsWith("GET http", Request.Get(u).ToString(), StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => Request.Get("mailto:a@b"));
    }

    private sealed class ForeignBody : RequestBody
    {
        public override MediaType? ContentType => null;

        public override Task WriteToAsync(Stream destination, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
