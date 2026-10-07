// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-7, OBS-17, OBS-18 and XCUT-19(c): the header renderer.</summary>
[Trait("Category", "Unit")]
public sealed class HeaderLogRendererTests
{
    private const string Prefix = "http.request.header.";

    private static List<KeyValuePair<string, object?>> Render(Headers headers, HttpLoggingOptions? options = null)
    {
        options ??= new HttpLoggingOptions();
        var renderer = new HeaderLogRenderer(options, new UrlRedactor(options.AllowedQueryParameters));
        var into = new List<KeyValuePair<string, object?>>();
        renderer.Render(headers, Prefix, into);
        return into;
    }

    private static Headers Of(params (string Name, string Value)[] pairs)
    {
        var builder = new Headers.Builder();
        foreach (var (name, value) in pairs)
        {
            builder.Add(name, value);
        }

        return builder.Build();
    }

    [Fact]
    public void An_allowed_header_is_rendered_verbatim()
    {
        var rendered = Render(Of(("Content-Type", "application/json")));

        Assert.Equal([new KeyValuePair<string, object?>(Prefix + "content-type", "application/json")], rendered);
    }

    [Fact]
    public void A_disallowed_header_is_REDACTED_by_default()
    {
        var rendered = Render(Of(("Authorization", "Bearer SECRET"), ("Cookie", "s=SECRET")));

        Assert.Equal(
            [
                new KeyValuePair<string, object?>(Prefix + "authorization", "REDACTED"),
                new KeyValuePair<string, object?>(Prefix + "cookie", "REDACTED"),
            ],
            rendered);
        Assert.DoesNotContain(rendered, p => p.Key.Contains("SECRET", StringComparison.Ordinal) || (p.Value as string)!.Contains("SECRET", StringComparison.Ordinal));
    }

    [Fact]
    public void A_disallowed_header_is_omitted_when_OmitDisallowedHeaders_is_true()
    {
        var rendered = Render(
            Of(("Authorization", "Bearer SECRET"), ("Accept", "*/*")),
            new HttpLoggingOptions { OmitDisallowedHeaders = true });

        Assert.Equal([new KeyValuePair<string, object?>(Prefix + "accept", "*/*")], rendered);
    }

    [Fact]
    public void A_url_valued_header_goes_through_RedactHeaderValue()
    {
        var absolute = Render(Of(("Location", "https://h/cb?code=S")));
        var relative = Render(Of(("location", "/cb?code=SECRET")));

        Assert.Equal("https://h/cb?code=***", absolute[0].Value);
        Assert.Equal("/cb?***", relative[0].Value);
    }

    [Fact]
    public void A_second_location_value_meets_the_redactor_on_its_own()
    {
        var rendered = Render(Of(("Location", "/a?x=1"), ("Location", "https://u:p@h/b?y=2")));

        var value = Assert.Single(rendered).Value;
        Assert.Equal("/a?***, https://***:***@h/b?y=***", value);
    }

    [Fact]
    public void Header_names_are_matched_case_insensitively_and_keyed_lower_case()
    {
        var rendered = Render(Of(("Content-TYPE", "text/plain")));

        Assert.Equal(Prefix + "content-type", Assert.Single(rendered).Key);
        Assert.Equal("text/plain", rendered[0].Value);

        var custom = Render(Of(("X-Mine", "v")), new HttpLoggingOptions { AllowedHeaderNames = ["x-MINE"] });
        Assert.Equal("v", Assert.Single(custom).Value);
    }

    [Fact]
    public void Insertion_order_is_preserved()
    {
        var rendered = Render(Of(("Vary", "a"), ("Accept", "b"), ("Date", "c")));

        Assert.Equal([Prefix + "vary", Prefix + "accept", Prefix + "date"], rendered.Select(p => p.Key));
    }

    [Fact]
    public void An_empty_value_is_kept_as_empty()
    {
        var rendered = Render(Of(("Accept", string.Empty)));

        Assert.Equal(string.Empty, Assert.Single(rendered).Value);
    }

    [Fact]
    public void A_very_long_value_is_truncated_after_redaction()
    {
        var rendered = Render(Of(("Accept", new string('a', 9000)), ("Location", "https://h/p?" + new string('k', 9000) + "=v")));

        Assert.Equal(new string('a', 8192) + "…[truncated]", rendered[0].Value);
        var location = (string)rendered[1].Value!;
        Assert.EndsWith("…[truncated]", location, StringComparison.Ordinal);
        Assert.Equal(8192 + "…[truncated]".Length, location.Length);
    }

    [Fact]
    public void A_custom_URL_valued_name_is_honoured()
    {
        var options = new HttpLoggingOptions { AllowedHeaderNames = ["x-callback"], UrlValuedHeaderNames = ["x-callback"] };

        var rendered = Render(Of(("X-Callback", "/cb?code=S")), options);

        Assert.Equal("/cb?***", Assert.Single(rendered).Value);
    }

    [Fact]
    public void The_renderer_is_stateless_across_calls_and_threads()
    {
        var options = new HttpLoggingOptions();
        var renderer = new HeaderLogRenderer(options, new UrlRedactor());
        var headers = Of(("Location", "/cb?code=S"), ("Authorization", "x"), ("Accept", "*/*"));
        var results = new string[50];

        Parallel.For(0, results.Length, i =>
        {
            var into = new List<KeyValuePair<string, object?>>();
            renderer.Render(headers, Prefix, into);
            results[i] = string.Join("|", into.Select(p => $"{p.Key}={p.Value}"));
        });

        Assert.Single(results.Distinct());
        Assert.Equal($"{Prefix}location=/cb?***|{Prefix}authorization=REDACTED|{Prefix}accept=*/*", results[0]);
    }
}
