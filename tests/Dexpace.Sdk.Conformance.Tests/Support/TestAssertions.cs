// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>Builds hand-made assertions and results for the reporting and runner tests, which must not depend on the real catalogue.</summary>
internal static class TestAssertions
{
    /// <summary>An assertion with no body: enough for the report, which never runs one.</summary>
    internal static ConformanceAssertion Make(
        string name,
        string[] ids,
        RequirementLevel level = RequirementLevel.Must,
        TransportFace[]? faces = null) =>
        new(name, ids, level, faces ?? [TransportFace.Async]);

    /// <summary>A result of <paramref name="assertion"/> on the async face.</summary>
    internal static ConformanceResult Result(
        ConformanceAssertion assertion,
        ConformanceStatus status,
        string detail = "detail",
        TransportFace face = TransportFace.Async) =>
        new(assertion, face, status, detail);
}
