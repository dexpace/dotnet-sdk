// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>seam-15</c> and <c>transport-30</c> (plan 2.11), and the Vacuous and NotExercised outcomes the raw-socket driver reports for them.</summary>
[Trait("Category", "Integration")]
public sealed class DisposeControlTests
{
    private static readonly ProxyOptions s_proxy = new() { Host = "127.0.0.1", Port = 1, UserName = "proxy-user", Password = "proxy-secret" };

    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        new(
            "seam-15.after-dispose",
            TransportFace.Async,
            "declares ObjectDisposedException but keeps serving",
            () => new TransportSubject
            {
                Name = "keeps serving",
                CreateAsync = _ => BrokenTransports.KeepsServingAfterDispose(),
                AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,
            }),
        new(
            "seam-15.after-dispose",
            TransportFace.Blocking,
            "declares ObjectDisposedException but keeps serving",
            () => new TransportSubject
            {
                Name = "keeps serving",
                CreateBlocking = _ => BrokenTransports.Plain().AsBlocking(),
                AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,
            }),
        new(
            "seam-15.after-dispose",
            TransportFace.Async,
            "throws after dispose on the owned transport but not on the borrowed one",
            () => new TransportSubject
            {
                Name = "borrowed keeps serving",
                CreateAsync = _ => new BrokenTransports.ThrowsAfterDispose(BrokenTransports.Plain()),
                CreateBorrowed = _ => ConformingHooks.Borrowed(),
                AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,
            }),
        new(
            "transport-30.proxy-discoverable-no-leak",
            TransportFace.Async,
            "offers the proxy credential to the origin's 401",
            () => new TransportSubject
            {
                Name = "leaks the proxy credential",
                CreateAsync = _ => BrokenTransports.Plain(),
                CreateWithProxy = (_, proxy) => BrokenTransports.LeaksProxyCredentialToOrigin(proxy),
            }),
        new(
            "transport-30.proxy-discoverable-no-leak",
            TransportFace.Async,
            "never answers the proxy's 407",
            () => new TransportSubject
            {
                Name = "ignores the proxy challenge",
                CreateAsync = _ => BrokenTransports.Plain(),
                CreateWithProxy = (_, proxy) => BrokenTransports.Around((request, options, ct, inner) =>
                    inner.ExecuteAsync(request.WithUrl(new Uri($"http://{proxy.Host}:{proxy.Port}{request.Url.PathAndQuery}")), options, ct)),
            }),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> ConformingCases => new()
    {
        { "seam-15.after-dispose", TransportFace.Async },
        { "seam-15.after-dispose", TransportFace.Blocking },
        { "transport-30.proxy-discoverable-no-leak", TransportFace.Async },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => ControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(ConformingCases))]
    public Task The_conforming_subject_passes(string assertion, TransportFace face)
    {
        var subject = ConformingHooks.Create();
        var conforming = new TransportSubject
        {
            Name = "conforming after dispose",
            CreateAsync = _ => new BrokenTransports.ThrowsAfterDispose(BrokenTransports.Plain()),
            CreateBlocking = _ => new BrokenTransports.ThrowsAfterDisposeBlocking(BrokenTransports.Plain().AsBlocking()),
            CreateBorrowed = _ => ConformingHooks.BorrowedThatThrowsAfterDispose(),
            CreateWithProxy = subject.CreateWithProxy,
            AfterDispose = AfterDisposeBehavior.ThrowsObjectDisposedException,
        };
        return ControlRow.RunPositiveAsync(assertion, face, conforming);
    }

    [Theory]
    [InlineData(TransportFace.Async)]
    [InlineData(TransportFace.Blocking)]
    public async Task A_subject_that_declares_nothing_about_dispose_is_vacuous_not_passing(TransportFace face)
    {
        var result = await AssertionControl.RunAsync("seam-15.after-dispose", face, RawSocketSubject.Create(), RawSocketSubject.Options);

        Assert.Equal(ConformanceStatus.Vacuous, result.Status);
        Assert.Contains("SEAM-15 is a MAY", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_subject_without_the_proxy_hook_is_not_exercised_not_vacuous()
    {
        var result = await AssertionControl.RunAsync("transport-30.proxy-discoverable-no-leak", TransportFace.Async, RawSocketSubject.Create(), RawSocketSubject.Options);

        Assert.Equal(ConformanceStatus.NotExercised, result.Status);
        Assert.Contains(nameof(TransportSubject.CreateWithProxy), result.Detail, StringComparison.Ordinal);
        Assert.NotNull(s_proxy);
    }
}
