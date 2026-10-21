// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>xcut-4</c> to <c>xcut-8</c> (plan 1.5).</summary>
[Trait("Category", "Unit")]
public sealed class InvariantTaxonomyControlTests
{
    private static bool Classified(int code) => code is 408 or 429 or (>= 500 and <= 599 and not 501 and not 505);

    internal static IReadOnlyList<InvariantControlRow> Rows { get; } =
    [
        .. Both("xcut-4.two-branches", "turns a refused connection into a non-retryable error", "always retryable", () => BrokenPipelines.OverDefault("refusal not retryable", _ => new BrokenPipelines.RefusalNotRetryable())),
        .. Both("xcut-4.two-branches", "turns a refused connection into a bare SDK exception", "IOException", () => BrokenPipelines.OverDefault("refusal not an IOException", _ => new BrokenPipelines.RefusalNotAnIoError())),
        .. Both("xcut-4.two-branches", "surfaces an error status as a bare error", "response-carrying error", () => BrokenPipelines.OverDefault("status as a bare error", _ => new BrokenPipelines.StatusAsBareError())),
        new("xcut-5.one-classifier", TransportFace.None, "bakes false for 507", () => new InvariantSubject { Name = "broken: 507", CreateResponseException = r => new BrokenPipelines.BakedException(r.Status.Code != 507 && Classified(r.Status.Code)) }, Because: "507"),
        new("xcut-5.one-classifier", TransportFace.None, "bakes true for 501", () => new InvariantSubject { Name = "broken: 501", CreateResponseException = r => new BrokenPipelines.BakedException(r.Status.Code == 501 || Classified(r.Status.Code)) }, Because: "501"),
        new("xcut-5.one-classifier", TransportFace.None, "hardcodes true for every status", () => new InvariantSubject { Name = "broken: hardcoded", CreateResponseException = _ => new BrokenPipelines.BakedException(true) }, Because: "400"),
        .. Both("xcut-6.capability-queried", "retries only the concrete types it knows", "declaring itself retryable", () => BrokenPipelines.Subject("concrete types", _ => [new BrokenPipelines.RetriesConcreteTypes()])),
        .. Both("xcut-7.configured-set-authoritative", "reads the baked flag instead of the configured set", "501 in the configured set", () => BrokenPipelines.Subject("baked flag", _ => [new BrokenPipelines.RetriesOnBakedFlag()])),
        new("xcut-8.strict-factory-rejects", TransportFace.None, "maps a 200", () => new InvariantSubject { Name = "broken: maps a 200", MapStrict = _ => new SdkException("mapped a 200") }, Because: "argument error"),
        new("xcut-8.strict-factory-rejects", TransportFace.None, "returns null-like for an error status", () => new InvariantSubject { Name = "broken: refuses everything", MapStrict = _ => throw new ArgumentException("no") }, Because: "must map the error status"),
        .. Both("xcut-8.convenience-returns-response", "throws for a 3xx", "non-error status", () => BrokenPipelines.OverDefault("throws for a 3xx", _ => new BrokenPipelines.ThrowsForRedirects())),
    ];

    public static IReadOnlyCollection<string> Covered => InvariantControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => InvariantControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => InvariantControlRow.PositiveCases(Rows, "xcut-8.strict-factory-rejects");

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => InvariantControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_default_subject_passes(string assertion, TransportFace face) => InvariantControlRow.RunPositiveAsync(assertion, face);

    [Fact]
    public async Task The_strict_factory_assertion_is_NotExercised_naming_its_hook_when_none_is_supplied()
    {
        var result = await InvariantSuite.RunAsync(new InvariantSubject(), InvariantControlRow.Find("xcut-8.strict-factory-rejects"), TransportFace.None, InvariantControlRow.Options, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.NotExercised, result.Status);
        Assert.Contains(nameof(InvariantSubject.MapStrict), result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public Task A_conforming_strict_factory_passes() =>
        InvariantControlRow.RunPositiveAsync(
            "xcut-8.strict-factory-rejects",
            TransportFace.None,
            new InvariantSubject
            {
                Name = "conforming strict factory",
                MapStrict = response => response.Status.IsError ? new HttpResponseException(response) : throw new ArgumentException("not an error status"),
            });

    private static InvariantControlRow[] Both(string assertion, string name, string because, Func<InvariantSubject> subject) =>
        [
            new(assertion, TransportFace.Async, name, subject, Because: because),
            new(assertion, TransportFace.Blocking, name, subject, Because: because),
        ];
}
