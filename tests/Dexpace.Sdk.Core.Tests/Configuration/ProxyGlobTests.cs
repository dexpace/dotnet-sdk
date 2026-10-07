// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.TestSupport.Vectors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Configuration;

// CFG-23, CFG-27 (BypassAll), P5a-14. Vectors: tests/vectors/config/proxy-globs.json.
[Trait("Category", "Unit")]
public sealed class ProxyGlobTests
{
    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var vector in VectorFile.Load<GlobCase>("config/proxy-globs.json"))
        {
            data.Add(vector.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void IsBypassed_matches_every_vector(string name)
    {
        var vector = VectorFile.Load<GlobCase>("config/proxy-globs.json").Single(c => c.Name == name);
        var options = new ProxyOptions { Host = "p", Port = 1, NonProxyHosts = vector.Patterns };

        Assert.Equal(vector.Expected, options.IsBypassed(vector.Host));
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("")]
    [InlineData("anything.at.all")]
    public void BypassAll_short_circuits_regardless_of_the_list(string host)
    {
        var options = new ProxyOptions { Host = "p", Port = 1, BypassAll = true };

        Assert.True(options.IsBypassed(host));
    }

    [Fact]
    public void A_null_host_throws_ArgumentNullException()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new ProxyOptions { Host = "p", Port = 1 }.IsBypassed(null!));

        Assert.Equal("host", error.ParamName);
    }

    [Fact]
    public void A_star_dense_pattern_matches_in_linear_time()
    {
        var options = new ProxyOptions { Host = "p", Port = 1, NonProxyHosts = ["*a*a*a*a*a*a*a*a*a*b"] };
        var host = new string('a', 60);

        var watch = Stopwatch.StartNew();
        var matched = options.IsBypassed(host);
        watch.Stop();

        Assert.False(matched);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"took {watch.Elapsed}");
    }

    [Fact]
    public void An_oversized_pattern_is_accepted_and_matches()
    {
        var options = new ProxyOptions { Host = "p", Port = 1, NonProxyHosts = [new string('?', 20_000)] };

        Assert.False(options.IsBypassed("example.com"));
        Assert.True(options.IsBypassed(new string('x', 20_000)));
    }

    [Fact]
    public void Patterns_are_compiled_once_at_init_not_per_call()
    {
        var options = new ProxyOptions { Host = "p", Port = 1, NonProxyHosts = ["a.test"] };
        var before = options.CompiledPatterns;

        _ = options.IsBypassed("a.test");
        _ = options.IsBypassed("b.test");

        Assert.Same(before, options.CompiledPatterns);
        Assert.Single(before);
    }

    [Theory]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("^")]
    [InlineData("$")]
    [InlineData("|")]
    [InlineData("\\")]
    [InlineData("+")]
    public void Glob_metacharacters_other_than_star_and_question_are_literals(string meta)
    {
        var options = new ProxyOptions { Host = "p", Port = 1, NonProxyHosts = ["a" + meta + "b"] };

        Assert.True(options.IsBypassed("a" + meta + "b"));
        Assert.False(options.IsBypassed("ab"));
        Assert.False(options.IsBypassed("aab"));
    }

    private sealed record GlobCase(string Name, List<string> Patterns, string Host, bool Expected, string? Note);
}
