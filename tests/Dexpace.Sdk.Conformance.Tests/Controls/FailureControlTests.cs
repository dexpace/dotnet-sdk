// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>transport-20</c> (two) and <c>transport-22</c> (plan 2.9).</summary>
[Trait("Category", "Integration")]
public sealed class FailureControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-20.no-response-is-retryable", TransportFace.Async, "wraps a missing response in a failure that is not retryable", BrokenTransports.NonRetryableNoResponse),
        ControlRow.Over("transport-20.no-response-is-retryable", TransportFace.Blocking, "wraps a missing response in a failure that is not retryable", BrokenTransports.NonRetryableNoResponse),
        ControlRow.Over("transport-20.no-response-is-retryable", TransportFace.Async, "reports a missing response as a cancellation", BrokenTransports.NoResponseAsCancellation),
        ControlRow.Over("transport-20.retried-by-the-pipeline", TransportFace.Async, "reports a missing response as a failure the pipeline does not retry", BrokenTransports.NonRetryableNoCause),
        new(
            "transport-22.adaptation-failure-releases",
            TransportFace.Async,
            "fails the adaptation and leaks the native response",
            () => new TransportSubject
            {
                Name = "leaks on faulting adaptation",
                CreateAsync = _ => BrokenTransports.Plain(),
                CreateWithFaultingAdaptation = _ => new ConformingHooks.FaultsAndLeaks(),
            }),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => ControlRow.PositiveCases(Rows, "transport-22.adaptation-failure-releases");

    public static TheoryData<string, TransportFace> HookCases => new() { { "transport-22.adaptation-failure-releases", TransportFace.Async } };

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
