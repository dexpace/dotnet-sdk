// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>transport-17</c>, <c>transport-18</c> and <c>transport-28</c> (plan 2.11). The rows are 8b's, the assertions 8a's.</summary>
[Trait("Category", "Integration")]
public sealed class ResendControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-17.single-use-written-once", TransportFace.Async, "writes the body twice", BrokenTransports.WritesBodyTwice),
        ControlRow.Over("transport-17.single-use-written-once", TransportFace.Blocking, "writes the body twice", BrokenTransports.WritesBodyTwice),
        new(
            "transport-18.native-resend-identical",
            TransportFace.Async,
            "re-sends an empty body natively",
            () => new TransportSubject
            {
                Name = "re-sends an empty body",
                CreateAsync = _ => BrokenTransports.Plain(),
                CreateWithNativeResend = _ => BrokenTransports.ResendsAnEmptyBody(),
            }),
        ControlRow.Over("transport-28.file-range-replayable", TransportFace.Async, "ignores the offset and count", BrokenTransports.IgnoresFileRange),
        ControlRow.Over("transport-28.file-range-replayable", TransportFace.Blocking, "ignores the offset and count", BrokenTransports.IgnoresFileRange),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => ControlRow.PositiveCases(Rows, "transport-18.native-resend-identical");

    public static TheoryData<string, TransportFace> HookCases => new() { { "transport-18.native-resend-identical", TransportFace.Async } };

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
