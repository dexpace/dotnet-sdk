// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>transport-1</c>, <c>-4</c>, <c>-5</c>, <c>-6</c>, <c>-8</c> and <c>-9</c> (plan 2.10); the rows are 8b's, the assertions 8a's.</summary>
[Trait("Category", "Integration")]
public sealed class RedirectAndTimeoutControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-1.redirect-not-followed", TransportFace.Async, "follows a 302 to the second server", BrokenTransports.FollowsRedirects),
        ControlRow.Over("transport-1.redirect-not-followed", TransportFace.Blocking, "follows a 302 to the second server", BrokenTransports.FollowsRedirects),
        ControlRow.Over("transport-4.timeout-is-retryable", TransportFace.Async, "reports the timeout as a cancellation", BrokenTransports.TimeoutAsCancellation),
        ControlRow.Over("transport-4.timeout-is-retryable", TransportFace.Blocking, "ignores the Timeout option", BrokenTransports.IgnoresTimeout),
        ControlRow.Over("transport-5.per-call-timeout", TransportFace.Async, "applies the first call's timeout to every later call", BrokenTransports.LeaksFirstTimeout),
        ControlRow.Over("transport-6.sub-resolution-timeout", TransportFace.Async, "truncates 100 microseconds to no timeout", BrokenTransports.TruncatesSubMillisecondTimeouts),
        new(
            "transport-8.internal-cancel-is-terminal",
            TransportFace.Async,
            "reports an internal cancel as a retryable timeout",
            () => new TransportSubject
            {
                Name = "internal cancel as timeout",
                CreateAsync = _ => BrokenTransports.Plain(),
                CreateWithInternalCancel = _ => ConformingHooks.InternalCancel(mapsToTimeout: true),
            }),
        ControlRow.Over("transport-9.settle-race-releases", TransportFace.Async, "orphans the response that arrives after a cancel", BrokenTransports.OrphansLateResponses),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => ControlRow.PositiveCases(Rows, "transport-8.internal-cancel-is-terminal");

    public static TheoryData<string, TransportFace> HookCases => new() { { "transport-8.internal-cancel-is-terminal", TransportFace.Async } };

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => ControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_raw_socket_client_passes(string assertion, TransportFace face) => ControlRow.RunPositiveAsync(assertion, face);

    [Theory]
    [MemberData(nameof(HookCases))]
    public Task The_conforming_hook_passes(string assertion, TransportFace face) => ControlRow.RunPositiveAsync(assertion, face, ConformingHooks.Create());
}
