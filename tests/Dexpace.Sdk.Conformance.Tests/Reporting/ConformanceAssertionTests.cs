// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Reporting;

[Trait("Category", "Unit")]
public sealed class ConformanceAssertionTests
{
    [Fact]
    public void An_assertion_exposes_its_identity_and_renders_as_its_name()
    {
        var assertion = new ConformanceAssertion(
            "transport-24.vendor-status-readable",
            ["TRANSPORT-24"],
            RequirementLevel.Must,
            [TransportFace.Async, TransportFace.Blocking],
            (_, _) => Task.CompletedTask);

        Assert.Equal("transport-24.vendor-status-readable", assertion.Name);
        Assert.Equal(["TRANSPORT-24"], assertion.RequirementIds);
        Assert.Equal(RequirementLevel.Must, assertion.Level);
        Assert.Equal([TransportFace.Async, TransportFace.Blocking], assertion.Faces);
        Assert.Equal("transport-24.vendor-status-readable", assertion.ToString());
    }

    [Fact]
    public void An_assertion_copies_its_lists()
    {
        var ids = new[] { "TRANSPORT-1" };
        var faces = new[] { TransportFace.Async };
        var assertion = new ConformanceAssertion("a.b", ids, RequirementLevel.Must, faces, (_, _) => Task.CompletedTask);

        ids[0] = "TRANSPORT-2";
        faces[0] = TransportFace.Blocking;

        Assert.Equal(["TRANSPORT-1"], assertion.RequirementIds);
        Assert.Equal([TransportFace.Async], assertion.Faces);
    }

    [Fact]
    public void An_assertion_needs_a_name_an_id_and_a_face()
    {
        Assert.Throws<ArgumentNullException>(() => new ConformanceAssertion(null!, ["TRANSPORT-1"], RequirementLevel.Must, [TransportFace.Async], (_, _) => Task.CompletedTask));
        Assert.Throws<ArgumentException>(() => new ConformanceAssertion(" ", ["TRANSPORT-1"], RequirementLevel.Must, [TransportFace.Async], (_, _) => Task.CompletedTask));
        Assert.Throws<ArgumentException>(() => new ConformanceAssertion("a.b", [], RequirementLevel.Must, [TransportFace.Async], (_, _) => Task.CompletedTask));
        Assert.Throws<ArgumentException>(() => new ConformanceAssertion("a.b", ["TRANSPORT-1"], RequirementLevel.Must, [], (_, _) => Task.CompletedTask));
    }

    [Fact]
    public void An_assertion_cannot_cite_a_requirement_the_catalogue_does_not_hold()
    {
        Assert.Throws<ArgumentException>(() => new ConformanceAssertion("a.b", ["TRANSPORT-99"], RequirementLevel.Must, [TransportFace.Async], (_, _) => Task.CompletedTask));
    }
}
