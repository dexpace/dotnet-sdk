// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>transport-3</c>, <c>transport-7</c> (both), <c>transport-16</c> and <c>async-20</c> (plan 2.6).</summary>
[Trait("Category", "Integration")]
public sealed class CancellationControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-3.cancel-is-terminal", TransportFace.Async, "reports cancellation as a retryable timeout", BrokenTransports.CancellationAsTimeout),
        ControlRow.Over("transport-3.cancel-is-terminal", TransportFace.Blocking, "reports cancellation as a retryable timeout", BrokenTransports.CancellationAsTimeout),
        ControlRow.Over("transport-3.cancel-is-terminal", TransportFace.Async, "ignores the token", () => BrokenTransports.IgnoringCancellation(reportCancellation: false)),
        ControlRow.Over("transport-7.cancel-releases-exchange", TransportFace.Async, "reports the cancel but leaves the socket open", () => BrokenTransports.IgnoringCancellation(reportCancellation: true)),
        ControlRow.Over("transport-7.attempt-timeout-aborts", TransportFace.Async, "ignores the attempt token", () => BrokenTransports.IgnoringCancellation(reportCancellation: false)),
        ControlRow.Over("transport-16.close-idempotent-nonblocking", TransportFace.Async, "throws on the second disposal", () => new BrokenTransports.ThrowsOnSecondDispose()),
        ControlRow.Over("transport-16.close-idempotent-nonblocking", TransportFace.Async, "disposal waits for the call in flight", () => new BrokenTransports.DisposeWaitsForInFlight()),
        ControlRow.Over("async-20.late-cancel-leaves-response-open", TransportFace.Async, "disposes the response on a late cancel", BrokenTransports.DisposesResponseOnLateCancel),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => ControlRow.PositiveCases(Rows);

    [Theory]
    [MemberData(nameof(Cases))]
    public Task The_broken_subject_is_caught(string assertion, TransportFace face, string control) => ControlRow.RunAsync(Rows, assertion, face, control);

    [Theory]
    [MemberData(nameof(PositiveCases))]
    public Task The_raw_socket_client_passes(string assertion, TransportFace face) => ControlRow.RunPositiveAsync(assertion, face);
}
