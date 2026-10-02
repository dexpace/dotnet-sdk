// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Http.Responses;

/// <summary>HTTP-10, HTTP-11 (Status half) and HTTP-12.</summary>
[Trait("Category", "Unit")]
public class StatusTests
{
    // code, informational, success, redirect, client error, server error, error
    public static TheoryData<int, bool, bool, bool, bool, bool, bool> Boundaries => new()
    {
        { 99, false, false, false, false, false, false },
        { 100, true, false, false, false, false, false },
        { 199, true, false, false, false, false, false },
        { 200, false, true, false, false, false, false },
        { 299, false, true, false, false, false, false },
        { 300, false, false, true, false, false, false },
        { 399, false, false, true, false, false, false },
        { 400, false, false, false, true, false, true },
        { 499, false, false, false, true, false, true },
        { 500, false, false, false, false, true, true },
        { 599, false, false, false, false, true, true },
        { 600, false, false, false, false, false, false },
    };

    [Fact]
    public void FromCode_maps_200_to_the_named_Ok_and_599_799_minus1_to_nameless_values_without_throwing()
    {
        Assert.Equal(Status.Ok, Status.FromCode(200));
        Assert.Equal("OK", Status.FromCode(200).Name);
        foreach (var code in new[] { 599, 799, -1 })
        {
            var status = Status.FromCode(code);
            Assert.Null(status.Name);
            Assert.Equal(code, status.Code);
        }
    }

    [Fact]
    public void TryGetKnown_is_true_for_200_and_false_for_599()
    {
        Assert.True(Status.TryGetKnown(200, out var ok));
        Assert.Equal(Status.Ok, ok);
        Assert.Equal("OK", ok.Name);

        Assert.False(Status.TryGetKnown(599, out var unknown));
        Assert.Equal(default, unknown);
    }

    [Theory]
    [MemberData(nameof(Boundaries))]
    public void Classification_by_boundary_code(
        int code, bool informational, bool success, bool redirect, bool clientError, bool serverError, bool error)
    {
        var status = Status.FromCode(code);

        Assert.Equal(informational, status.IsInformational);
        Assert.Equal(success, status.IsSuccess);
        Assert.Equal(redirect, status.IsRedirect);
        Assert.Equal(clientError, status.IsClientError);
        Assert.Equal(serverError, status.IsServerError);
        Assert.Equal(error, status.IsError);
    }

    [Fact]
    public void Equality_is_by_code_and_the_hash_agrees()
    {
        // HTTP-12 pin, with the hash assertion added.
        Assert.Equal(Status.NotFound, Status.FromCode(404));
        Assert.Equal(Status.NotFound.GetHashCode(), Status.FromCode(404).GetHashCode());
        Assert.True(Status.NotFound == Status.FromCode(404));
        Assert.NotEqual(Status.NotFound, Status.FromCode(405));
    }

    [Fact]
    public void A_default_Status_is_code_zero_and_unrecognised()
    {
        // Pin: Status stays a readonly record struct, so default(Status) is a legitimate unrecognised status.
        var status = default(Status);

        Assert.Equal(0, status.Code);
        Assert.Null(status.Name);
        Assert.False(status.IsError);
        Assert.False(status.IsSuccess);
    }
}
