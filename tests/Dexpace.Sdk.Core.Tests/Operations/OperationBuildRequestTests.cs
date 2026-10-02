// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Operations;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Operations;

/// <summary>
/// <c>BuildRequest</c> (SEAM-26, SEAM-27; design §3.5, positions H and K; plan rulings 5 and 6), driven by
/// <c>tests/vectors/seam/operation-compose.json</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OperationBuildRequestTests
{
    // A test-local case shape; the loader deserialises it with System.Text.Json (tests are not AOT).
    public sealed record ComposeCase(
        string Name,
        string Base,
        string Method,
        string Template,
        Dictionary<string, string>? PathParameters,
        List<List<string>>? Query,
        string? Expected,
        string? Error,
        string? ErrorAt,
        List<string>? MustContain,
        List<string>? MustNotContain)
    {
        public override string ToString() => Name;
    }

    public static TheoryData<ComposeCase> Cases()
    {
        var data = new TheoryData<ComposeCase>();
        foreach (var vector in VectorFile.Load<ComposeCase>("seam/operation-compose.json"))
        {
            data.Add(vector);
        }

        return data;
    }

    private static Uri BaseOf(ComposeCase vector) => new(vector.Base, UriKind.RelativeOrAbsolute);

    private static OperationDescriptor DescriptorOf(ComposeCase vector)
    {
        var query = new Query.Builder();
        foreach (var pair in vector.Query ?? [])
        {
            query.Add(pair[0], pair[1]);
        }

        return new OperationDescriptor
        {
            Method = Method.Of(vector.Method),
            PathTemplate = vector.Template,
            PathParameters = (vector.PathParameters ?? []).ToImmutableDictionary(StringComparer.Ordinal),
            Query = query.Build(),
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void BuildRequest_matches_the_composition_vectors(ComposeCase vector)
    {
        if (vector.Expected is { } expected)
        {
            var request = DescriptorOf(vector).BuildRequest(BaseOf(vector));

            Assert.Equal(expected, request.Url.AbsoluteUri);
            return;
        }

        var errorType = vector.Error switch
        {
            "ArgumentException" => typeof(ArgumentException),
            "InvalidOperationException" => typeof(InvalidOperationException),
            _ => throw new InvalidOperationException($"Unknown error type in vector '{vector.Name}'."),
        };

        var built = false;
        var raised = Record.Exception(() =>
        {
            var descriptor = DescriptorOf(vector);
            built = true;
            descriptor.BuildRequest(BaseOf(vector));
        });

        Assert.NotNull(raised);
        Assert.Equal(errorType, raised.GetType());
        Assert.Equal(vector.ErrorAt == "build", built);
        foreach (var text in vector.MustContain ?? [])
        {
            Assert.Contains(text, raised.Message, StringComparison.Ordinal);
        }

        foreach (var text in vector.MustNotContain ?? [])
        {
            Assert.DoesNotContain(text, raised.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("https://h/c", "/{x}/pets", "https://h/c//pets")]
    [InlineData("https://h/c", "/pets/{x}/toys", "https://h/c/pets//toys")]
    [InlineData("https://h/c/", "/{x}/pets", "https://h/c//pets")]
    [InlineData("https://h/c", "//pets/{x}", "https://h/c//pets/")]
    [InlineData("https://h/", "/{x}/pets", "https://h//pets")]
    public void An_empty_path_value_keeps_its_segment_wherever_it_sits(string baseText, string template, string expected)
    {
        var descriptor = new OperationDescriptor
        {
            Method = Method.Get,
            PathTemplate = template,
            PathParameters = new Dictionary<string, string> { ["x"] = string.Empty }.ToImmutableDictionary(StringComparer.Ordinal),
        };

        var request = descriptor.BuildRequest(new Uri(baseText, UriKind.Absolute));

        Assert.Equal(expected, request.Url.AbsoluteUri);
    }

    [Fact]
    public void The_built_request_carries_method_headers_and_the_same_body_instance()
    {
        var body = RequestBody.FromString("{}");
        var headers = new Headers.Builder().Add("X-Trace", "abc").Build();
        var descriptor = new OperationDescriptor { Method = Method.Post, PathTemplate = "/pets", Headers = headers, Body = body };

        var request = descriptor.BuildRequest(new Uri("https://host"));

        Assert.Equal(Method.Post, request.Method);
        Assert.Equal(headers, request.Headers);
        Assert.Equal("abc", request.Headers.Get("x-trace"));
        Assert.Same(body, request.Body);
    }

    [Fact]
    public void Each_projection_lands_in_its_request_part()
    {
        // The conformance example: a parameterless GET setting only Method and PathTemplate assembles GET <base>/pets.
        var minimal = new OperationDescriptor { Method = Method.Get, PathTemplate = "/pets" }.BuildRequest(new Uri("https://host/c"));

        Assert.Equal(Method.Get, minimal.Method);
        Assert.Equal("https://host/c/pets", minimal.Url.AbsoluteUri);
        Assert.Empty(minimal.Headers.Names);
        Assert.Null(minimal.Body);

        var full = new OperationDescriptor
        {
            Method = Method.Post,
            PathTemplate = "/pets/{id}",
            PathParameters = ImmutableDictionary<string, string>.Empty.Add("id", "7"),
            Query = new Query.Builder().Add("dry", "true").Build(),
            Headers = new Headers.Builder().Add("X-A", "1").Build(),
            Body = RequestBody.FromString("x"),
        }.BuildRequest(new Uri("https://host/c"));

        Assert.Equal("https://host/c/pets/7?dry=true", full.Url.AbsoluteUri);
        Assert.Equal("1", full.Headers.Get("X-A"));
        Assert.NotNull(full.Body);
    }

    [Fact]
    public void A_body_on_a_method_that_forbids_one_fails_assembly()
    {
        var descriptor = new OperationDescriptor { Method = Method.Get, PathTemplate = "/pets", Body = RequestBody.FromString("x") };

        Assert.Throws<ArgumentException>(() => descriptor.BuildRequest(new Uri("https://host")));
    }

    [Fact]
    public void A_lone_surrogate_value_is_rejected_at_init_without_the_value_in_the_message()
    {
        var descriptor = new OperationDescriptor { Method = Method.Get, PathTemplate = "/things/{id}" };

        var ex = Assert.Throws<ArgumentException>(() => descriptor.WithPathParameter("id", "ok\uD800ok"));

        Assert.Contains("'id'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_placeholder_message_names_the_placeholder_and_never_a_value()
    {
        var descriptor = new OperationDescriptor
        {
            Method = Method.Get,
            PathTemplate = "/a/{first}/{second}",
            PathParameters = ImmutableDictionary<string, string>.Empty.Add("first", "VALUE-ONE"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => descriptor.BuildRequest(new Uri("https://host")));

        Assert.Contains("second", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("VALUE-ONE", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rejected_base_message_carries_the_base_through_UrlRedactor()
    {
        var baseAddress = new Uri("https://user:secret@h/c?token=abc#f");
        var descriptor = new OperationDescriptor { Method = Method.Get, PathTemplate = "/pets" };

        var ex = Assert.Throws<ArgumentException>(() => descriptor.BuildRequest(baseAddress));

        Assert.DoesNotContain("secret", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("abc", ex.Message, StringComparison.Ordinal);
        Assert.Contains(new UrlRedactor().Redact(baseAddress), ex.Message, StringComparison.Ordinal);
        Assert.Equal("baseAddress", ex.ParamName);
    }

    [Fact]
    public void BuildRequest_rejects_a_null_baseAddress()
    {
        var descriptor = new OperationDescriptor { Method = Method.Get, PathTemplate = "/pets" };

        Assert.Equal("baseAddress", Assert.Throws<ArgumentNullException>(() => descriptor.BuildRequest((Uri)null!)).ParamName);
    }

    // --- BuildRequest(DexpaceClientOptions), SEAM-27 ---

    [Fact]
    public void BuildRequest_from_options_reads_BaseAddress()
    {
        var descriptor = new OperationDescriptor { Method = Method.Get, PathTemplate = "/pets" };
        var options = new DexpaceClientOptions { BaseAddress = new Uri("https://host/c?sig=abc") };

        var request = descriptor.BuildRequest(options);

        Assert.Equal("https://host/c/pets?sig=abc", request.Url.AbsoluteUri);
    }

    [Fact]
    public void BuildRequest_from_options_throws_ArgumentException_when_BaseAddress_is_null()
    {
        var descriptor = new OperationDescriptor { Method = Method.Get, PathTemplate = "/pets" };

        var ex = Assert.Throws<ArgumentException>(() => descriptor.BuildRequest(new DexpaceClientOptions()));

        Assert.Equal("options", ex.ParamName);
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("https://host/c#frag")]
    [InlineData("ftp://host/")]
    public void BuildRequest_from_options_applies_the_same_base_rules(string baseAddress)
    {
        var descriptor = new OperationDescriptor { Method = Method.Get, PathTemplate = "/pets" };
        var options = new DexpaceClientOptions { BaseAddress = new Uri(baseAddress, UriKind.RelativeOrAbsolute) };

        Assert.Throws<ArgumentException>(() => descriptor.BuildRequest(options));
    }

    [Fact]
    public void BuildRequest_from_options_rejects_null_options()
    {
        var descriptor = new OperationDescriptor { Method = Method.Get, PathTemplate = "/pets" };

        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => descriptor.BuildRequest((DexpaceClientOptions)null!)).ParamName);
    }
}
