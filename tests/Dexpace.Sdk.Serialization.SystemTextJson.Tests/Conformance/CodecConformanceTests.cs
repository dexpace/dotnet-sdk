// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Linq;
using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Http.Common;
using Xunit;

namespace Dexpace.Sdk.Serialization.SystemTextJson.Tests.Conformance;

/// <summary>
/// The codec suite driven against <see cref="SystemTextJsonSerde"/> (phase 10, P10-8): one case per (assertion, face), plus the whole suite as a
/// report. The codec is built exactly as a consumer builds it, from the default options over the probe context, with no waiver and no assertion
/// left not exercised: both lists are pinned (P10-28).
/// </summary>
[Trait("Category", "Conformance")]
public sealed class CodecConformanceTests
{
    private static readonly SuiteOptions s_options = new() { AssertionTimeout = TimeSpan.FromSeconds(30) };

    public static TheoryData<string, TransportFace> Rows()
    {
        var rows = new TheoryData<string, TransportFace>();
        foreach (var assertion in CodecSuite.Assertions)
        {
            foreach (var face in assertion.Faces)
            {
                rows.Add(assertion.Name, face);
            }
        }

        return rows;
    }

    private static CodecSubject Subject() => new()
    {
        Name = "Dexpace.Sdk.Serialization.SystemTextJson",
        CreateSerde = static () => new SystemTextJsonSerde(SystemTextJsonSerde.CreateDefaultOptions(ProbeContext.Default)),
        ExpectedMediaType = CommonMediaTypes.ApplicationJson,
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task Assertion_holds(string name, TransportFace face)
    {
        var assertion = CodecSuite.Assertions.Single(a => a.Name == name);

        var result = await CodecSuite.RunAsync(Subject(), assertion, face, s_options, TestContext.Current.CancellationToken);

        Assert.True(result.Status == ConformanceStatus.Passed, $"{name} on {face}: {result.Status}: {result.Detail}");
    }

    [Fact]
    public async Task The_whole_suite_is_green_and_its_report_is_written()
    {
        var report = await CodecSuite.RunAllAsync(Subject(), s_options, TestContext.Current.CancellationToken);

        TestContext.Current.TestOutputHelper?.WriteLine(report.ToString());
        Assert.True(report.IsGreen, report.ToString());
        Assert.Equal("codec", report.Suite);
    }

    [Fact]
    public async Task Nothing_is_not_exercised_vacuous_or_waived()
    {
        var report = await CodecSuite.RunAllAsync(Subject(), s_options, TestContext.Current.CancellationToken);

        Assert.Empty(s_options.Waivers);
        Assert.All(report.Results, static r => Assert.Equal(ConformanceStatus.Passed, r.Status));
        Assert.Equal(CodecSuite.Assertions.Sum(static a => a.Faces.Count), report.Results.Count);
    }

    [Fact]
    public async Task Every_lifted_serde_requirement_is_covered_by_a_passing_result()
    {
        var report = await CodecSuite.RunAllAsync(Subject(), s_options, TestContext.Current.CancellationToken);

        foreach (var id in new[] { "SERDE-1", "SERDE-2", "SERDE-3", "SERDE-9", "SERDE-10", "SERDE-12", "SERDE-15", "SERDE-16", "SERDE-17", "SERDE-19", "SERDE-20", "SEAM-20" })
        {
            Assert.Equal(ConformanceStatus.Passed, report.ByRequirement[id]);
        }
    }
}
