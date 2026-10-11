// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for <c>transport-15</c> (two) and <c>transport-19</c> (plan 2.9).</summary>
[Trait("Category", "Integration")]
public sealed class LifecycleControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        new(
            "transport-15.borrowed-survives",
            TransportFace.Async,
            "disposing the transport also disposes the borrowed native client",
            () =>
            {
                var plain = RawSocketSubject.Create();
                return new TransportSubject
                {
                    Name = "disposes the borrowed client",
                    CreateAsync = plain.CreateAsync,
                    CreateBorrowed = _ =>
                    {
                        var native = new ConformingHooks.NativeClient();
                        return new BorrowedTransport(new ConformingHooks.OverNative(native, disposesNative: true), native.SendAsync, native);
                    },
                };
            }),
        ControlRow.Over("transport-15.owned-released", TransportFace.Async, "keeps an idle pooled connection open after dispose", () => new BrokenTransports.LeakyPooledTransport()),
        ControlRow.Over("transport-19.abandoned-body-unblocks", TransportFace.Async, "ignores the token while the body source is parked", BrokenTransports.IgnoresTokenWhileReadingBody),
        ControlRow.Over("transport-19.abandoned-body-unblocks", TransportFace.Async, "keeps a file handle open", BrokenTransports.LeaksFileHandles),
    ];

    public static IReadOnlyCollection<string> Covered => ControlRow.CoveredBy(Rows);

    public static TheoryData<string, TransportFace, string> Cases => ControlRow.Cases(Rows);

    public static TheoryData<string, TransportFace> PositiveCases => ControlRow.PositiveCases(Rows, "transport-15.borrowed-survives");

    public static TheoryData<string, TransportFace> HookCases => new() { { "transport-15.borrowed-survives", TransportFace.Async } };

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
