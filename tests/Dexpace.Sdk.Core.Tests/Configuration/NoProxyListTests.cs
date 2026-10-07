// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Text.Json;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-26, CFG-27; P5a-15 item 4. Vectors: tests/vectors/config/no-proxy-split.json.
[Trait("Category", "Unit")]
public sealed class NoProxyListTests
{
    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var vector in VectorFile.Load<SplitCase>("config/no-proxy-split.json"))
        {
            data.Add(vector.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Splits_every_vector(string name)
    {
        var vector = VectorFile.Load<SplitCase>("config/no-proxy-split.json").Single(c => c.Name == name);

        var result = NoProxyList.Parse(vector.Value);

        if (vector.Expected.ValueKind == JsonValueKind.String)
        {
            Assert.Equal("bypass-all", vector.Expected.GetString());
            Assert.True(result.BypassAll);
            Assert.Empty(result.Tokens);
        }
        else
        {
            Assert.False(result.BypassAll);
            Assert.Equal(vector.Expected.EnumerateArray().Select(e => e.GetString()!).ToList(), result.Tokens);
        }
    }

    [Fact]
    public void Each_token_round_trips_through_the_escape()
    {
        var random = new Random(5144);
        const string Alphabet = "abcdefghijklmnopqrstuvwxyz,.*-";
        for (var i = 0; i < 5_000; i++)
        {
            var count = random.Next(2, 6);
            var tokens = Enumerable.Range(0, count)
                .Select(_ => new string(Enumerable.Range(0, random.Next(1, 11)).Select(_ => Alphabet[random.Next(Alphabet.Length)]).ToArray()))
                .Where(t => t.Trim().Length > 0)
                .ToList();
            if (tokens.Count < 2)
            {
                continue;
            }

            var joined = string.Join(',', tokens.Select(t => t.Replace(",", "\\,", StringComparison.Ordinal)));

            var result = NoProxyList.Parse(joined);

            Assert.Equal(tokens, result.Tokens);
        }
    }

    [Fact]
    public void A_lone_star_is_reported_as_bypass_all_not_as_a_token()
    {
        var result = NoProxyList.Parse("*");

        Assert.True(result.BypassAll);
        Assert.Empty(result.Tokens);
        Assert.False(NoProxyList.Parse("*,x.test").BypassAll);
    }

    private sealed record SplitCase(string Name, string Value, JsonElement Expected);
}
