// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported header-value cases (roadmap constraint 10) live in tests/vectors/redaction/header-values.json; each names its
// origin in "note". Not ported: Ruby's cases that exercise a stubbed policy object (exploding allow-list, URL-header
// lookup), nil/Integer stringification, and the raw-byte (.b) cases, which have no counterpart for a .NET string.
// Sources: ruby-sdk@5b17395 gems/dexpace-core/test/dexpace/instrumentation/redactor_test.rb,
// nodejs-sdk@54aeed4 packages/core/src/observability/redaction.test.ts.

using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-16 and the header route of OBS-11, OBS-15: <see cref="UrlRedactor.RedactHeaderValue"/>.</summary>
[Trait("Category", "Unit")]
public sealed class UrlRedactorHeaderValueTests
{
    private const string Sentinel = "[malformed url]";

    private static readonly UrlRedactor s_redactor = new();

    public sealed record HeaderCase(string Input, string Expected, string Note);

    public static TheoryData<string, string, string> Vectors()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var c in VectorFile.Load<HeaderCase>("redaction/header-values.json"))
        {
            data.Add(c.Input, c.Expected, c.Note);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void Vectors_match(string input, string expected, string note)
    {
        Assert.True(note.Length > 0);
        Assert.Equal(expected, s_redactor.RedactHeaderValue(input));
    }

    [Fact]
    public void An_absolute_value_is_redacted_like_a_request_url()
    {
        const string Value = "https://u:p@h/cb?code=S&api-version=1";

        Assert.Equal(s_redactor.Redact(new Uri(Value)), s_redactor.RedactHeaderValue(Value));
    }

    [Theory]
    [InlineData("/cb?code=x")]
    [InlineData("/cb")]
    [InlineData("/")]
    public void The_scheme_prefix_decides_not_IsAbsoluteUri(string value)
    {
        // On Unix System.Uri reads a rooted path as an absolute file: URI; the header route must not follow it.
        var redacted = s_redactor.RedactHeaderValue(value);

        Assert.StartsWith("/", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("file:", redacted, StringComparison.Ordinal);
        Assert.Equal(value.Contains('?', StringComparison.Ordinal) ? "/cb?***" : value, redacted);
    }

    [Theory]
    [InlineData("\u0001\u0002 control chars")]
    [InlineData("%")]
    [InlineData("%zz?secret=1")]
    [InlineData("http://[bad")]
    [InlineData("http://[bad?secret=1")]
    [InlineData("http://user:secret@[bad/x")]
    [InlineData("\0http://user:secret@h/p")]
    public void The_malformed_sentinel_is_never_returned(string value)
    {
        var redacted = s_redactor.RedactHeaderValue(value);

        Assert.NotEqual(Sentinel, redacted);
        Assert.DoesNotContain("secret", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_huge_hostile_value_does_not_yield_the_sentinel()
    {
        var redacted = s_redactor.RedactHeaderValue(new string('?', 100_000));

        Assert.Equal("?***", redacted);
    }

    [Theory]
    [InlineData("/x/user:secret@h", false)]
    [InlineData("//user:secret@h/x", true)]
    [InlineData("http://user:secret@h/a b", true)]
    [InlineData("junk //user:secret@h/x", true)]
    public void Userinfo_is_masked_on_every_route(string value, bool masked)
    {
        var redacted = s_redactor.RedactHeaderValue(value);

        Assert.Equal(!masked, redacted.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void The_method_is_total()
    {
        var random = new Random(16);
        for (var i = 0; i < 2000; i++)
        {
            var chars = new char[random.Next(0, 40)];
            for (var j = 0; j < chars.Length; j++)
            {
                chars[j] = "ab:/?#@%\\ \t[]&=.é😀"[random.Next(0, 19)];
            }

            var redacted = s_redactor.RedactHeaderValue(new string(chars));
            Assert.NotEqual(Sentinel, redacted);
        }

        Assert.Throws<ArgumentNullException>(() => s_redactor.RedactHeaderValue(null!));
    }

    [Fact]
    public void Existing_members_are_unchanged()
    {
        Assert.Equal("https://h/p?a=***", s_redactor.Redact("https://h/p?a=1"));
        Assert.Equal(["api-version"], UrlRedactor.DefaultQueryAllowList);
        Assert.Equal(Sentinel, s_redactor.Redact("not a url \u0001"));
    }
}
