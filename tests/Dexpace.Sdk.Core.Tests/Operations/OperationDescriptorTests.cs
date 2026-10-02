// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Operations;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Operations;

/// <summary>
/// <c>OperationDescriptor</c> (SEAM-26, SEAM-28; design §3.5, §4, positions H and K; plan ruling 5): shape, template and
/// parameter validation through the initializer and through <c>with</c>, equality and <c>ToString</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OperationDescriptorTests
{
    private static OperationDescriptor Get(string template) => new() { Method = Method.Get, PathTemplate = template };

    [Fact]
    public void Method_and_PathTemplate_are_required_members()
    {
        // A missing one is CS9035 at compile time (design §10 no-builder-objects).
        var type = typeof(OperationDescriptor);

        Assert.NotNull(type.GetProperty(nameof(OperationDescriptor.Method))!.GetCustomAttributes(typeof(RequiredMemberAttribute), false).SingleOrDefault());
        Assert.NotNull(type.GetProperty(nameof(OperationDescriptor.PathTemplate))!.GetCustomAttributes(typeof(RequiredMemberAttribute), false).SingleOrDefault());
        Assert.Empty(type.GetProperty(nameof(OperationDescriptor.Query))!.GetCustomAttributes(typeof(RequiredMemberAttribute), false));
    }

    [Fact]
    public void A_parameterless_GET_needs_only_Method_and_PathTemplate()
    {
        var descriptor = Get("/pets");

        Assert.NotNull(descriptor.PathParameters);
        Assert.Empty(descriptor.PathParameters);
        Assert.Same(Query.Empty, descriptor.Query);
        Assert.Same(Headers.Empty, descriptor.Headers);
        Assert.Null(descriptor.Body);
        Assert.Null(descriptor.OperationId);
    }

    [Fact]
    public void A_null_Method_or_PathTemplate_throws_ArgumentNullException()
    {
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => new OperationDescriptor { Method = null!, PathTemplate = "/p" }).ParamName);
        Assert.Equal("value", Assert.Throws<ArgumentNullException>(() => new OperationDescriptor { Method = Method.Get, PathTemplate = null! }).ParamName);
    }

    [Fact]
    public void An_empty_PathTemplate_is_allowed()
    {
        Assert.Equal(string.Empty, Get(string.Empty).PathTemplate);
    }

    [Theory]
    [InlineData("/a/{id")]
    [InlineData("/a/id}")]
    [InlineData("/a/{{id}}")]
    [InlineData("/a/{}")]
    [InlineData("/a?x=1")]
    [InlineData("/a#frag")]
    [InlineData("/a b")]
    [InlineData("/a%")]
    [InlineData("/a%2")]
    [InlineData("/a%zz")]
    [InlineData("/a%2{x}")]
    [InlineData("/é")]
    [InlineData("/../x")]
    [InlineData("/%2e%2e/x")]
    [InlineData("/%2E%2E/x")]
    [InlineData("/.%2e/x")]
    [InlineData("/%2e./x")]
    [InlineData("/./x")]
    [InlineData("/c/..")]
    [InlineData("/c/.")]
    public void PathTemplate_validation_throws_ArgumentException_naming_the_template(string template)
    {
        var viaInitializer = Assert.Throws<ArgumentException>(() => Get(template));
        var viaWith = Assert.Throws<ArgumentException>(() => Get("/ok") with { PathTemplate = template });

        foreach (var ex in new[] { viaInitializer, viaWith })
        {
            Assert.Contains(template, ex.Message, StringComparison.Ordinal);
            Assert.Equal(nameof(OperationDescriptor.PathTemplate), ex.ParamName);
        }
    }

    [Theory]
    [InlineData("/a..b/{x}")]
    [InlineData("/.well-known/{x}")]
    [InlineData("/v1.2/{x}")]
    [InlineData("/...")]
    [InlineData("/a./.b")]
    [InlineData("/.{x}")]
    public void Dots_inside_a_segment_are_not_dot_segments(string template)
    {
        Assert.Equal(template, Get(template).PathTemplate);
    }

    [Fact]
    public void Placeholder_names_are_any_brace_free_text_compared_ordinally()
    {
        var descriptor = Get("/a/{id}/{Id}/{ id }/{id}");

        // {id} and {Id} are two placeholders; { id } keeps its spaces; a repeated {id} is one placeholder.
        Assert.Equal(["id", "Id", " id "], descriptor.Placeholders);
    }

    [Fact]
    public void A_valid_template_with_percent_escapes_and_several_placeholders_is_accepted()
    {
        var descriptor = Get("/a%20b/{x}/c/{y}");

        Assert.Equal(["x", "y"], descriptor.Placeholders);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void PathParameters_reject_dot_and_dot_dot_naming_the_key(string value)
    {
        var parameters = ImmutableDictionary<string, string>.Empty.Add("the-key", value);

        var viaInitializer = Assert.Throws<ArgumentException>(() => new OperationDescriptor { Method = Method.Get, PathTemplate = "/{the-key}", PathParameters = parameters });
        var viaWith = Assert.Throws<ArgumentException>(() => Get("/{the-key}") with { PathParameters = parameters });
        var viaHelper = Assert.Throws<ArgumentException>(() => Get("/{the-key}").WithPathParameter("the-key", value));

        foreach (var ex in new[] { viaInitializer, viaWith, viaHelper })
        {
            Assert.Contains("the-key", ex.Message, StringComparison.Ordinal);
            Assert.Equal(nameof(OperationDescriptor.PathParameters), ex.ParamName);
        }
    }

    [Theory]
    [InlineData("high")]
    [InlineData("low")]
    [InlineData("embedded")]
    public void PathParameters_reject_a_lone_surrogate_naming_the_key_and_never_the_value(string kind)
    {
        // The lone surrogate is built here: xUnit's theory-data serialisation would replace it with U+FFFD.
        var value = kind switch
        {
            "high" => "\uD800",
            "low" => "\uDFFF",
            _ => "ok\uD800ok",
        };
        var parameters = ImmutableDictionary<string, string>.Empty.Add("k", value);

        var viaInitializer = Assert.Throws<ArgumentException>(() => new OperationDescriptor { Method = Method.Get, PathTemplate = "/{k}", PathParameters = parameters });
        var viaWith = Assert.Throws<ArgumentException>(() => Get("/{k}") with { PathParameters = parameters });

        foreach (var ex in new[] { viaInitializer, viaWith })
        {
            Assert.Contains("'k'", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(value, ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_well_formed_surrogate_pair_is_ordinary_text()
    {
        var descriptor = Get("/{k}").WithPathParameter("k", "\U0001F600");

        Assert.Equal("\U0001F600", descriptor.PathParameters["k"]);
    }

    [Fact]
    public void A_null_PathParameters_or_null_value_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => Get("/p") with { PathParameters = null! });
        Assert.Throws<ArgumentNullException>(() => Get("/{k}").WithPathParameter("k", null!));
        Assert.Throws<ArgumentNullException>(() => Get("/{k}").WithPathParameter(null!, "v"));
    }

    [Fact]
    public void PathParameters_are_re_based_on_ordinal_keys()
    {
        var caseInsensitive = ImmutableDictionary<string, string>.Empty
            .WithComparers(StringComparer.OrdinalIgnoreCase)
            .Add("id", "lower");

        var descriptor = Get("/{id}/{Id}") with { PathParameters = caseInsensitive };
        var both = descriptor.WithPathParameter("Id", "upper");

        Assert.Equal(2, both.PathParameters.Count);
        Assert.Equal("lower", both.PathParameters["id"]);
        Assert.Equal("upper", both.PathParameters["Id"]);
        Assert.Same(StringComparer.Ordinal, both.PathParameters.KeyComparer);
    }

    [Fact]
    public void WithPathParameter_adds_and_replaces_without_mutating_the_original()
    {
        var original = Get("/{id}");

        var added = original.WithPathParameter("id", "1");
        var replaced = added.WithPathParameter("id", "2");

        Assert.Empty(original.PathParameters);
        Assert.Equal("1", added.PathParameters["id"]);
        Assert.Equal("2", replaced.PathParameters["id"]);
        Assert.Equal("1", added.PathParameters["id"]);
    }

    [Fact]
    public void Equality_compares_PathParameters_by_content_and_delegates_the_rest()
    {
        OperationDescriptor Build() => new()
        {
            Method = Method.Post,
            PathTemplate = "/pets/{id}",
            PathParameters = ImmutableDictionary<string, string>.Empty.Add("id", "7"),
            Query = new Query.Builder().Add("a", "1").Build(),
            Headers = new Headers.Builder().Add("X-A", "1").Build(),
            Body = RequestBody.FromString("hello"),
            OperationId = "addPet",
        };

        var left = Build();
        var right = Build();

        Assert.NotSame(left.PathParameters, right.PathParameters);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.True(left == right);
        Assert.NotEqual(left, right with { PathParameters = right.PathParameters.SetItem("id", "8") });
        Assert.NotEqual(left, right with { OperationId = "other" });
        Assert.NotEqual(left, right with { Method = Method.Put });
        Assert.NotEqual(left, right with { Query = Query.Empty });
        Assert.NotEqual(left, right with { Headers = Headers.Empty });
        Assert.NotEqual(left, right with { Body = null });
        Assert.False(left.Equals(null));
    }

    [Fact]
    public void ToString_shows_method_and_template_never_values()
    {
        var descriptor = new OperationDescriptor
        {
            Method = Method.Get,
            PathTemplate = "/pets/{id}",
            PathParameters = ImmutableDictionary<string, string>.Empty.Add("id", "PATH-SECRET"),
            Query = new Query.Builder().Add("k", "QUERY-SECRET").Build(),
            Headers = new Headers.Builder().Add("Authorization", "HEADER-SECRET").Build(),
        };

        var text = descriptor.ToString();

        Assert.Equal("GET /pets/{id}", text);
        Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
    }

    // --- SEAM-28 ---

    [Fact]
    public void OperationId_null_is_accepted_and_blank_is_rejected()
    {
        Assert.Null(Get("/p").OperationId);
        Assert.Equal("listPets", (Get("/p") with { OperationId = "listPets" }).OperationId);

        foreach (var blank in new[] { string.Empty, "  ", "\t" })
        {
            Assert.Throws<ArgumentException>(() => new OperationDescriptor { Method = Method.Get, PathTemplate = "/p", OperationId = blank });
            Assert.Throws<ArgumentException>(() => Get("/p") with { OperationId = blank });
        }
    }

    [Fact]
    public void The_operation_id_never_reaches_the_url_headers_or_body()
    {
        var baseAddress = new Uri("https://api.example.com/v1");
        var body = RequestBody.FromString("payload");
        var headers = new Headers.Builder().Add("X-A", "1").Build();
        var plain = new OperationDescriptor { Method = Method.Post, PathTemplate = "/pets", Headers = headers, Body = body };
        var named = plain with { OperationId = "uniqueOperationId" };

        var plainRequest = plain.BuildRequest(baseAddress);
        var namedRequest = named.BuildRequest(baseAddress);

        Assert.Equal(plainRequest.Url.AbsoluteUri, namedRequest.Url.AbsoluteUri);
        Assert.Equal(plainRequest.Headers, namedRequest.Headers);
        Assert.Same(body, namedRequest.Body);
        Assert.DoesNotContain("uniqueOperationId", namedRequest.Url.AbsoluteUri, StringComparison.Ordinal);
        Assert.All(namedRequest.Headers, header => Assert.DoesNotContain(header.Value, value => value.Contains("uniqueOperationId", StringComparison.Ordinal)));
    }
}
