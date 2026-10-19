// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Codec;
using Dexpace.Sdk.Conformance.Tests.Controls;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Suite;

/// <summary>Plan 2.1 (P10-8): the codec suite's shell and its probe models.</summary>
[Trait("Category", "Unit")]
public sealed class CodecSuiteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void The_preamble_says_what_a_green_run_does_not_prove()
    {
        Assert.Contains("metadata for models it was not given", CodecSuite.Preamble, StringComparison.Ordinal);
        Assert.Contains("configuration it was not handed", CodecSuite.Preamble, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_report_records_the_codec_suite_and_its_preamble_and_a_conforming_codec_is_green()
    {
        var report = await CodecSuite.RunAllAsync(new CodecSubject { Name = "reference", CreateSerde = () => new ReferenceSerde() }, new SuiteOptions { AssertionTimeout = TimeSpan.FromSeconds(10) }, Ct);

        Assert.True(report.IsGreen, report.ToString());
        Assert.Equal("codec", report.Suite);
        Assert.Equal(CodecSuite.Preamble, report.PreambleText);
        Assert.Equal(report.Results.Count, report.Results.Count(r => r.Status == ConformanceStatus.Passed));
        Assert.Equal(CodecSuite.Assertions.Sum(a => a.Faces.Count), report.Results.Count);
    }

    [Fact]
    public async Task A_codec_factory_that_throws_is_a_red_result_not_an_exception()
    {
        // The factory runs inside the assertion, so a crash is a result: Failed where the assertion guards the codec's calls, Errored where it reads the codec itself.
        var report = await CodecSuite.RunAllAsync(new CodecSubject { CreateSerde = () => throw new InvalidOperationException("no codec") }, null, Ct);

        Assert.False(report.IsGreen);
        Assert.All(report.Results, r => Assert.True(r.Status is ConformanceStatus.Failed or ConformanceStatus.Errored, r.Assertion.Name + ": " + r.Status));
        Assert.Contains(report.Results, r => r.Detail.Contains("no codec", StringComparison.Ordinal) || r.Detail.Contains("InvalidOperationException", StringComparison.Ordinal));
    }

    [Fact]
    public void The_subject_defaults_to_a_malformed_json_payload_and_rejects_null_hooks()
    {
        var subject = new CodecSubject();

        Assert.Equal("codec", subject.Name);
        Assert.Equal("{\"name\": }"u8.ToArray(), subject.MalformedPayload.ToArray());
        Assert.Null(subject.ExpectedMediaType);
        Assert.Throws<ArgumentNullException>(() => new CodecSubject { CreateSerde = null! });
        Assert.Throws<ArgumentException>(() => new CodecSubject { Name = " " });
        Assert.Throws<InvalidOperationException>(subject.CreateSerde);
    }

    [Fact]
    public async Task RunAsync_rejects_a_foreign_assertion_a_face_it_does_not_declare_and_null()
    {
        var subject = new CodecSubject { CreateSerde = () => new ReferenceSerde() };
        var foreign = TransportSuite.Assertions[0];
        var codec = CodecSuite.Assertions.Single(a => a.Name == "serde-1.round-trip");

        await Assert.ThrowsAsync<ArgumentNullException>(() => CodecSuite.RunAsync(null!, codec, TransportFace.Async, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CodecSuite.RunAsync(subject, null!, TransportFace.Async, null, Ct));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CodecSuite.RunAllAsync(null!, null, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => CodecSuite.RunAsync(subject, foreign, TransportFace.Async, null, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => CodecSuite.RunAsync(subject, codec, TransportFace.None, null, Ct));
    }

    [Fact]
    public void The_probe_models_are_plain_records_with_absent_as_the_default_of_every_tri_state()
    {
        var patch = new ProbePatch(default, default, default);

        Assert.True(patch.Name.IsAbsent && patch.Age.IsAbsent && patch.Address.IsAbsent);
        Assert.Equal(new ProbeAddress("a", "b"), new ProbeAddress("a", "b"));
        Assert.Equal(["x"], new ProbeContact("n", 1, new ProbeAddress("a", "b"), ["x"]).Tags);
        Assert.Single(new ProbeSequence([default]).Items);
        Assert.NotNull(new ProbeUnencodable(static () => { }).Callback);
    }
}
