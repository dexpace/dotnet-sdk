// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Security.Cryptography;
using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>xcut-20</c> to <c>xcut-24</c> and <c>nfr-11</c> (plan 1.9).</summary>
[Trait("Category", "Unit")]
public sealed class InvariantHostControlTests
{
    private static readonly string[] s_shortSequence = Enumerable.Range(0, 8).Select(n => n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    internal static IReadOnlyList<InvariantControlRow> Rows { get; } =
    [
        .. Both("xcut-20.observability-never-throws", "throws on a malformed URL", "must not throw", () => new InvariantSubject { Name = "broken: renderer", RedactUrl = url => new Uri(url).Host }),
        .. Both("xcut-20.observability-never-throws", "writes to the sink without a guard", "must not fail the request", () => Logging(logger => new BrokenPipelines.LogsHeadersVerbatim(logger))),
        new("xcut-21.cnonce-unpredictable", TransportFace.None, "draws 64 bits", () => Drawing(() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))), Because: "128 bits"),
        new("xcut-21.cnonce-unpredictable", TransportFace.None, "draws 120 bits as base64url", () => Drawing(() => Base64Url(15)), Because: "128 bits"),
        new("xcut-21.cnonce-unpredictable", TransportFace.None, "draws a string the suite cannot count", () => Drawing(() => "nonce:" + RandomNumberGenerator.GetHexString(32, lowercase: true)), Because: "128 bits"),
        new("xcut-21.cnonce-unpredictable", TransportFace.None, "cycles through eight values", () => Drawing(new Cycle().Next), Because: "must not repeat"),
        .. Both("xcut-22.core-borrows", "closes the asynchronous bridge's client", "asynchronous bridge", () => new InvariantSubject { Name = "broken: async bridge", AsAsync = client => new BrokenPipelines.DisposingAsyncBridge(client) }),
        .. Both("xcut-22.core-borrows", "closes the blocking bridge's client", "blocking bridge", () => new InvariantSubject { Name = "broken: blocking bridge", AsBlocking = client => new BrokenPipelines.DisposingBlockingBridge(client) }),
        .. Both("xcut-22.core-borrows", "closes the caller's transport after a call", "call through a pipeline", () => new InvariantSubject
        {
            Name = "broken: pipeline",
            CreatePipeline = (transport, options, time, logger) =>
                PipelineBuilder.Flatten(new InvariantSubject().CreatePipeline(transport, options, time, logger)).Add(new BrokenPipelines.DisposesTransportAfterCall(transport)).Build(),
        }),
        new("xcut-23.resolution-deterministic", TransportFace.None, "picks the last of two", () => Seam(new SeamResolution(Throws, () => { }, () => true)), Because: "two candidates"),
        new("xcut-23.resolution-deterministic", TransportFace.None, "picks nothing silently", () => Seam(new SeamResolution(() => { }, Throws, () => true)), Because: "no candidate"),
        new("xcut-23.resolution-deterministic", TransportFace.None, "lets a discoverable candidate beat the explicit one", () => Seam(new SeamResolution(Throws, Throws, () => false)), Because: "explicit install must win"),
        .. Both("xcut-24.preview-capped-non-consuming", "buffers the whole body to preview it", "materialize the payload", () => Previewing(new BrokenPipelines.ReadsWholeBody(onlyErrors: false))),
        .. Both("xcut-24.preview-capped-non-consuming", "buffers the whole error body for its snapshot", "error-body snapshot must be byte-capped", () => Previewing(new BrokenPipelines.ReadsWholeBody(onlyErrors: true))),
        .. Both("xcut-24.preview-capped-non-consuming", "hands back an error with an empty body", "snapshot must carry the truncated body", () => Previewing(new BrokenPipelines.ReplacesErrorBody([]))),
        .. Both("xcut-24.preview-capped-non-consuming", "hands back an error whose body is not the server's", "prefix of the body", () => Previewing(new BrokenPipelines.ReplacesErrorBody([0xFF, 0xFE, 0xFD]))),
        .. Both("nfr-11.scheduler-agnostic", "blocks on the caller's synchronization context", "single-threaded synchronization context", () => BrokenPipelines.Subject("blocks on the context", _ => [new BrokenPipelines.BlocksOnTheCallersContext()])),
    ];

    public static IReadOnlyCollection<string> Covered => InvariantControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => InvariantControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => InvariantControlRow.PositiveCases(Rows, "xcut-23.resolution-deterministic");

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => InvariantControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_default_subject_passes(string assertion, TransportFace face) => InvariantControlRow.RunPositiveAsync(assertion, face);

    [Fact]
    public async Task The_resolution_assertion_is_NotExercised_naming_its_hook_when_none_is_supplied()
    {
        var result = await InvariantSuite.RunAsync(new InvariantSubject(), InvariantControlRow.Find("xcut-23.resolution-deterministic"), TransportFace.None, InvariantControlRow.Options, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.NotExercised, result.Status);
        Assert.Contains(nameof(InvariantSubject.SeamResolution), result.Detail, StringComparison.Ordinal);
    }

    // A subject that takes no body preview has no preview to cap: the assertion says so (Vacuous) and never passes it.
    [Theory]
    [InlineData(TransportFace.Async)]
    [InlineData(TransportFace.Blocking)]
    public async Task A_pipeline_that_logs_no_body_preview_is_vacuous_and_never_passed(TransportFace face)
    {
        var silent = BrokenPipelines.Subject("logs nothing", _ => [new ErrorMappingPolicy()]);

        var result = await InvariantSuite.RunAsync(silent, InvariantControlRow.Find("xcut-24.preview-capped-non-consuming"), face, InvariantControlRow.Options, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Vacuous, result.Status);
        Assert.Contains("no body preview", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public Task A_conforming_resolution_passes() =>
        InvariantControlRow.RunPositiveAsync("xcut-23.resolution-deterministic", TransportFace.None, Seam(new SeamResolution(Throws, Throws, () => true)));

    [Fact]
    public Task A_supplied_cryptographic_draw_passes() =>
        InvariantControlRow.RunPositiveAsync("xcut-21.cnonce-unpredictable", TransportFace.None, Drawing(() => RandomNumberGenerator.GetHexString(32, lowercase: true)));

    // XCUT-21 asks for at least 128 bits, not for one encoding of them: a longer draw, upper-case hex and base64 / base64url all count.
    [Fact]
    public Task A_longer_hex_draw_passes() =>
        InvariantControlRow.RunPositiveAsync("xcut-21.cnonce-unpredictable", TransportFace.None, Drawing(() => RandomNumberGenerator.GetHexString(64, lowercase: true)));

    [Fact]
    public Task An_upper_case_hex_draw_of_128_bits_passes() =>
        InvariantControlRow.RunPositiveAsync("xcut-21.cnonce-unpredictable", TransportFace.None, Drawing(() => RandomNumberGenerator.GetHexString(32, lowercase: false)));

    [Fact]
    public Task A_base64url_draw_of_128_bits_passes() =>
        InvariantControlRow.RunPositiveAsync("xcut-21.cnonce-unpredictable", TransportFace.None, Drawing(() => Base64Url(16)));

    [Fact]
    public Task A_padded_base64_draw_of_128_bits_passes() =>
        InvariantControlRow.RunPositiveAsync("xcut-21.cnonce-unpredictable", TransportFace.None, Drawing(() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))));

    private static string Base64Url(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static void Throws() => throw new InvalidOperationException("no unambiguous candidate: install one explicitly");

    private static InvariantSubject Drawing(Func<string> draw) => new() { Name = "broken: draw", DrawCnonce = draw };

    private static InvariantSubject Seam(SeamResolution resolution) => new() { Name = "broken: seam", SeamResolution = resolution };

    private static InvariantSubject Logging(Func<Microsoft.Extensions.Logging.ILogger, HttpPipelinePolicy> extra) =>
        new()
        {
            Name = "broken: logging",
            CreatePipeline = (transport, options, time, logger) =>
                PipelineBuilder.Flatten(new InvariantSubject().CreatePipeline(transport, options, time, logger)).Add(extra(logger)).Build(),
        };

    private static InvariantSubject Previewing(HttpPipelinePolicy extra) =>
        new()
        {
            Name = "broken: preview",
            CreatePipeline = (transport, options, time, logger) =>
                PipelineBuilder.Flatten(new InvariantSubject().CreatePipeline(transport, options, time, logger)).Add(extra).Build(),
        };

    private static InvariantControlRow[] Both(string assertion, string name, string because, Func<InvariantSubject> subject) =>
        [
            new(assertion, TransportFace.Async, name, subject, Because: because),
            new(assertion, TransportFace.Blocking, name, subject, Because: because),
        ];

    private sealed class Cycle
    {
        private int _next;

        public string Next() => s_shortSequence[_next++ % s_shortSequence.Length];
    }
}
