// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Xunit;

namespace Dexpace.Sdk.Conformance.Tests.Reporting;

/// <summary>P8a-8: waivers are by requirement ID, carry a reason and an optional owner, and reject unknown targets.</summary>
[Trait("Category", "Unit")]
public sealed class ConformanceWaiverTests
{
    [Fact]
    public void A_waiver_keeps_its_fields_and_defaults_the_narrowing_to_all()
    {
        var waiver = new ConformanceWaiver("TRANSPORT-8", "the F3 defect") { Owner = "8b" };

        Assert.Equal("TRANSPORT-8", waiver.RequirementId);
        Assert.Equal("the F3 defect", waiver.Reason);
        Assert.Equal("8b", waiver.Owner);
        Assert.Null(waiver.Assertion);
        Assert.Null(waiver.Face);
    }

    [Fact]
    public void A_waiver_can_be_narrowed_to_one_assertion_and_one_face()
    {
        var waiver = new ConformanceWaiver("TRANSPORT-11", "no log latch")
        {
            Assertion = "transport-11.drop-logged",
            Face = TransportFace.Async,
        };

        Assert.Equal("transport-11.drop-logged", waiver.Assertion);
        Assert.Equal(TransportFace.Async, waiver.Face);
    }

    [Fact]
    public void Waivers_compare_by_value()
    {
        Assert.Equal(new ConformanceWaiver("TRANSPORT-8", "r") { Owner = "8b" }, new ConformanceWaiver("TRANSPORT-8", "r") { Owner = "8b" });
        Assert.NotEqual(new ConformanceWaiver("TRANSPORT-8", "r"), new ConformanceWaiver("TRANSPORT-8", "r") { Owner = "8b" });
        Assert.NotEqual(new ConformanceWaiver("TRANSPORT-8", "r"), new ConformanceWaiver("TRANSPORT-8", "r") { Face = TransportFace.Blocking });
    }

    [Fact]
    public void A_null_requirement_id_or_reason_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new ConformanceWaiver(null!, "r"));
        Assert.Throws<ArgumentNullException>(() => new ConformanceWaiver("TRANSPORT-8", null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_requirement_id_or_reason_is_rejected(string blank)
    {
        Assert.Throws<ArgumentException>(() => new ConformanceWaiver(blank, "r"));
        Assert.Throws<ArgumentException>(() => new ConformanceWaiver("TRANSPORT-8", blank));
    }

    [Fact]
    public void An_empty_assertion_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new ConformanceWaiver("TRANSPORT-8", "r") { Assertion = "" });
    }

    [Theory]
    [InlineData("TRANSPORT-99")]
    [InlineData("NOPE-1")]
    [InlineData("transport-8")]
    public void An_unknown_requirement_id_is_rejected_without_echoing_an_oversized_input(string unknown)
    {
        var error = Assert.Throws<ArgumentException>(() => new ConformanceWaiver(unknown, "r"));

        Assert.Contains(unknown, error.Message, StringComparison.Ordinal);
        var oversized = new string('X', 500);
        var long_ = Assert.Throws<ArgumentException>(() => new ConformanceWaiver(oversized, "r"));
        Assert.DoesNotContain(oversized, long_.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('X', 65), long_.Message, StringComparison.Ordinal);
    }
}
