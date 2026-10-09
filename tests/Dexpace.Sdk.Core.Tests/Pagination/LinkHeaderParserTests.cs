// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// The vector file tests/vectors/pagination/link-header.json holds the 15 cases of nodejs-sdk@c0ff3fd
// packages/core/src/pagination/link-header.test.ts plus the .NET-specific cases of phase 7c; its note lists the two
// divergences (Node reads every rel parameter of a link-value and follows a target that is not a URL).

using System.Text;
using Dexpace.Sdk.Core.Pagination;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pagination;

/// <summary>PAGE-18 and PAGE-20: the internal RFC 8288 <c>Link</c> scanner.</summary>
[Trait("Category", "Unit")]
public sealed class LinkHeaderParserTests
{
    private const string VectorPath = "pagination/link-header.json";

    private sealed record LinkCase(string Name, string Header, string? Expected);

    public static TheoryData<string> Names() => [.. VectorFile.Load<LinkCase>(VectorPath).Select(c => c.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void FindNext_matches_the_vector(string name)
    {
        var vector = VectorFile.Load<LinkCase>(VectorPath).Single(c => c.Name == name);
        Assert.Equal(vector.Expected, LinkHeaderParser.FindNext(vector.Header));
    }

    [Fact]
    public void The_vector_file_names_are_unique_and_cover_both_page_18_and_page_20()
    {
        var cases = VectorFile.Load<LinkCase>(VectorPath);
        Assert.Equal(cases.Count, cases.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(cases, c => c.Name.StartsWith("PAGE-18:", StringComparison.Ordinal));
        Assert.Contains(cases, c => c.Name.StartsWith("PAGE-20:", StringComparison.Ordinal));
        Assert.True(cases.Count >= 15, "the 15 Node cases are a floor");
    }

    [Fact]
    public void A_null_header_yields_no_link()
    {
        Assert.Null(LinkHeaderParser.FindNext(null));
    }

    [Fact]
    public void FindNext_never_throws_for_arbitrary_input_seed_20261009()
    {
        string[] alphabet = ["<", ">", "\"", ";", ",", "=", " ", "\t", "\\", "a", "rel", "next", "/", "%", "\0"];
        var rng = new Random(20261009);
        for (var i = 0; i < 500; i++)
        {
            var length = rng.Next(0, 61);
            var builder = new StringBuilder();
            for (var c = 0; c < length; c++)
            {
                builder.Append(alphabet[rng.Next(alphabet.Length)]);
            }

            var input = builder.ToString();
            string? found = null;
            var thrown = Record.Exception(() => found = LinkHeaderParser.FindNext(input));

            Assert.True(thrown is null, $"seed 20261009, case {i}: {thrown?.GetType().Name} for input '{input.Replace("\0", "\\0", StringComparison.Ordinal)}'");
            Assert.True(
                found is null || input.Contains(found, StringComparison.Ordinal),
                $"seed 20261009, case {i}: result '{found}' is not a substring of the input");
        }
    }
}
