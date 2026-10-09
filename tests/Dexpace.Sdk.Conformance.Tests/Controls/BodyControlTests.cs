// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>Negative controls for the body assertions of plan 2.7: <c>transport-25</c> (three) and <c>transport-26</c>.</summary>
[Trait("Category", "Integration")]
public sealed class BodyControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-25.lazy-body", TransportFace.Async, "buffers the whole body before returning", BrokenTransports.BufferingBody),
        ControlRow.Over("transport-25.lazy-body", TransportFace.Blocking, "buffers the whole body before returning", BrokenTransports.BufferingBody),
        ControlRow.Over("transport-25.large-round-trip", TransportFace.Async, "truncates the body at 1 MiB", () => BrokenTransports.TruncatingBody(1024 * 1024)),
        ControlRow.Over("transport-25.large-round-trip", TransportFace.Blocking, "truncates the body at 1 MiB", () => BrokenTransports.TruncatingBody(1024 * 1024)),
        ControlRow.Over(
            "transport-25.dispose-releases",
            TransportFace.Async,
            "never closes the connection when the response is disposed",
            () => BrokenTransports.Rewriting(response => new Response(
                response.Request,
                response.Status,
                response.Protocol,
                response.Headers,
                ResponseBody.FromStream(new BrokenTransports.LeakyStream(response.Body.OpenRead()), null, response.Body.ContentLength)))),
        ControlRow.Over("transport-26.bodyless-methods", TransportFace.Async, "gives a body-less POST a body", BrokenTransports.SendsBodyForBodyless),
        ControlRow.Over("transport-26.bodyless-methods", TransportFace.Blocking, "gives a body-less POST a body", BrokenTransports.SendsBodyForBodyless),
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
