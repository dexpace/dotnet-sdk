// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance.Tests.Drivers;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Controls;

/// <summary>
/// Negative controls for the response-shape assertions (plan 2.5): <c>transport-21</c>, <c>transport-23</c>,
/// <c>async-1</c> and <c>transport-24</c>. Each control breaks exactly the clause; the raw-socket client is the positive
/// counterpart.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ResponseShapeControlTests
{
    internal static IReadOnlyList<ControlRow> Rows { get; } =
    [
        ControlRow.Over("transport-21.pre-dispatch-failure-via-task", TransportFace.Async, "throws synchronously on a null request", () => new BrokenTransports.SyncThrowingTransport()),
        ControlRow.Over("transport-23.never-null", TransportFace.Async, "completes with a null response", () => new BrokenTransports.NullResponseTransport()),
        ControlRow.Over(
            "async-1.single-non-null-response",
            TransportFace.Async,
            "answers every call with a response for another request",
            () => BrokenTransports.Rewriting(response => new Response(
                Dexpace.Sdk.Core.Http.Request.Request.Get("http://elsewhere.invalid/other"),
                Status.Ok,
                Protocol.Http11,
                null,
                ResponseBody.FromBytes("fixed"u8.ToArray())))),
        ControlRow.Over(
            "transport-24.vendor-status-readable",
            TransportFace.Async,
            "maps a 5xx status to a failure",
            () => BrokenTransports.Rewriting(response => response.Status.Code >= 500 ? throw new ServiceResponseException("5xx") : response)),
        ControlRow.Over(
            "transport-24.vendor-status-readable",
            TransportFace.Blocking,
            "returns the right status with an empty body",
            () => BrokenTransports.Rewriting(response => new Response(response.Request, response.Status, response.Protocol, response.Headers, ResponseBody.FromBytes(Array.Empty<byte>())))),
        ControlRow.Over(
            "transport-24.vendor-status-readable",
            TransportFace.Async,
            "never releases the connection on dispose",
            () => BrokenTransports.Rewriting(response => new Response(
                response.Request,
                response.Status,
                response.Protocol,
                response.Headers,
                ResponseBody.FromStream(new BrokenTransports.LeakyStream(response.Body.OpenRead()), null, response.Body.ContentLength)))),
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
