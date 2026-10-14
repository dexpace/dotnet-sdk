// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>xcut-13</c> to <c>xcut-15</c> (plan 1.7).</summary>
[Trait("Category", "Unit")]
public sealed class InvariantLifecycleControlTests
{
    internal static IReadOnlyList<InvariantControlRow> Rows { get; } =
    [
        .. Both("xcut-13.dispose-latched", "releases the resource on every close", "closed twice", () => WithDisposable(body => new DisposableUnderTest(
            "naive closer",
            () => body.ReleaseResource(),
            () =>
            {
                body.ReleaseResource();
                return ValueTask.CompletedTask;
            }))),
        .. Both("xcut-13.dispose-non-blocking", "waits for the read parked on the body", "must not wait", () => WithDisposable(body => new DisposableUnderTest(
            "closer that waits",
            () => body.Parked!.ReadFinished.Wait(),
            async () => await body.Parked!.ReadFinished))),
        new("xcut-14.bounded-under-burst", TransportFace.None, "never evicts", () => Mapping(BoundedProbes.Unbounded), Because: "never hold more than its cap"),
        new("xcut-14.bounded-under-burst", TransportFace.None, "checks above the cap instead of at it", () => Mapping(BoundedProbes.OffByOne), Because: "never hold more than its cap"),
        new("xcut-14.drains-after-insert", TransportFace.None, "never evicts", () => Mapping(BoundedProbes.Unbounded), Because: "drain back"),
        new("xcut-14.drains-after-insert", TransportFace.None, "checks once before it inserts", () => Mapping(BoundedProbes.PreInsertCheckThenEvict), Because: "drain back"),
        new("xcut-15.no-external-alias", TransportFace.None, "keeps the caller's media-type parameter map", () => Modelling(new ModelFactories { MediaTypeParameters = input => input }), Because: "media type's parameters"),
        new("xcut-15.no-external-alias", TransportFace.None, "keeps the caller's challenge parameter map", () => Modelling(new ModelFactories { ChallengeParameters = input => input }), Because: "challenge's parameters"),
        new("xcut-15.no-external-alias", TransportFace.None, "keeps the caller's byte array", () => Modelling(new ModelFactories { BytesBody = input => () => input }), Because: "request body sends"),
        new("xcut-15.no-external-alias", TransportFace.None, "shows a builder's later fields", () => Modelling(new ModelFactories { HeadersAfterBuilderMutation = () => new Headers.Builder().Add("A", "1").Add("B", "2").Build() }), Because: "before a builder"),
        new("xcut-15.no-external-alias", TransportFace.None, "returns the same headers from a setter", () => Modelling(new ModelFactories { WithHeader = (headers, _, _) => headers }), Because: "header \"setter\""),
        new("xcut-15.no-external-alias", TransportFace.None, "returns the same request from a setter", () => Modelling(new ModelFactories { RequestWithHeader = (request, _, _) => request }), Because: "request \"setter\""),
    ];

    public static IReadOnlyCollection<string> Covered => InvariantControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => InvariantControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => InvariantControlRow.PositiveCases(Rows, "xcut-14.bounded-under-burst", "xcut-14.drains-after-insert");

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => InvariantControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_default_subject_passes(string assertion, TransportFace face) => InvariantControlRow.RunPositiveAsync(assertion, face);

    [Theory]
    [InlineData("xcut-14.bounded-under-burst")]
    [InlineData("xcut-14.drains-after-insert")]
    public async Task The_bounded_map_assertions_are_NotExercised_naming_their_hook_when_none_is_supplied(string assertion)
    {
        var result = await InvariantSuite.RunAsync(new InvariantSubject(), InvariantControlRow.Find(assertion), TransportFace.None, InvariantControlRow.Options, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.NotExercised, result.Status);
        Assert.Contains(nameof(InvariantSubject.CreateBoundedMap), result.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("xcut-14.bounded-under-burst")]
    [InlineData("xcut-14.drains-after-insert")]
    public Task A_conforming_bounded_map_passes(string assertion) =>
        InvariantControlRow.RunPositiveAsync(assertion, TransportFace.None, new InvariantSubject { Name = "conforming map", CreateBoundedMap = BoundedProbes.Conforming });

    private static InvariantSubject WithDisposable(Func<ReleaseCountingBody, DisposableUnderTest> make) =>
        new() { Name = "broken: disposable", CreateDisposables = body => [make((ReleaseCountingBody)body)] };

    private static InvariantSubject Mapping(Func<int, BoundedMapProbe> create) =>
        new() { Name = "broken: bounded map", CreateBoundedMap = create };

    private static InvariantSubject Modelling(ModelFactories models) =>
        new() { Name = "broken: models", Models = models };

    private static InvariantControlRow[] Both(string assertion, string name, string because, Func<InvariantSubject> subject) =>
        [
            new(assertion, TransportFace.Async, name, subject, Because: because),
            new(assertion, TransportFace.Blocking, name, subject, Because: because),
        ];
}
