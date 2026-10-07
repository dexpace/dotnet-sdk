// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/recovery/outcome.test.ts.
using System.Reflection;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class OutcomeTests
{
    private static Outcome.Success Success(int code = 200) => new(TestResponses.Create(Status.FromCode(code)));

    private static Outcome.Failure Failure() => new(new IOException("io"));

    public static TheoryData<int> Seeds()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < 64; i++)
        {
            data.Add(i);
        }

        return data;
    }

    private static Outcome Generate(int seed) => new Random(seed).Next(2) == 0 ? Success(200 + (seed % 5)) : Failure();

    [Fact]
    public void Outcome_has_no_non_private_constructor()
    {
        var constructors = typeof(Outcome).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotEmpty(constructors);
        Assert.All(constructors, c => Assert.True(c.IsPrivate));
    }

    [Fact]
    public void The_assembly_holds_exactly_two_types_deriving_from_Outcome()
    {
        var derived = typeof(Outcome).Assembly.GetTypes().Where(t => t != typeof(Outcome) && typeof(Outcome).IsAssignableFrom(t)).ToList();

        Assert.Equal(2, derived.Count);
        Assert.Contains(typeof(Outcome.Success), derived);
        Assert.Contains(typeof(Outcome.Failure), derived);
        Assert.All(derived, t => Assert.True(t.IsSealed));
    }

    [Fact]
    public void A_success_reports_IsSuccess_and_TryGetResponse()
    {
        var response = TestResponses.Create(Status.Ok);
        Outcome outcome = new Outcome.Success(response);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.IsFailure);
        Assert.True(outcome.TryGetResponse(out var found));
        Assert.Same(response, found);
        Assert.False(outcome.TryGetError(out var error));
        Assert.Null(error);
    }

    [Fact]
    public void A_failure_reports_IsFailure_and_TryGetError()
    {
        var exception = new IOException("io");
        Outcome outcome = new Outcome.Failure(exception);

        Assert.True(outcome.IsFailure);
        Assert.False(outcome.IsSuccess);
        Assert.True(outcome.TryGetError(out var found));
        Assert.Same(exception, found);
        Assert.False(outcome.TryGetResponse(out var response));
        Assert.Null(response);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void The_accessor_pairs_are_never_both_true(int seed)
    {
        var outcome = Generate(seed);

        Assert.NotEqual(outcome.IsSuccess, outcome.IsFailure);
        Assert.NotEqual(outcome.TryGetResponse(out _), outcome.TryGetError(out _));
    }

    [Fact]
    public void Match_invokes_exactly_one_branch_exactly_once()
    {
        foreach (var outcome in new Outcome[] { Success(), Failure() })
        {
            int successCalls = 0, failureCalls = 0;

            var result = outcome.Match(
                _ =>
                {
                    successCalls++;
                    return "s";
                },
                _ =>
                {
                    failureCalls++;
                    return "f";
                });

            Assert.Equal(1, successCalls + failureCalls);
            Assert.Equal(outcome.IsSuccess ? "s" : "f", result);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Match_folds_agree_with_the_predicates(int seed)
    {
        var outcome = Generate(seed);

        var folded = outcome.Match(_ => true, _ => false);

        Assert.Equal(outcome.IsSuccess, folded);
    }

    [Fact]
    public void Constructors_reject_null()
    {
        Assert.Equal("response", Assert.Throws<ArgumentNullException>(() => new Outcome.Success(null!)).ParamName);
        Assert.Equal("error", Assert.Throws<ArgumentNullException>(() => new Outcome.Failure(null!)).ParamName);
    }

    [Fact]
    public void ToString_names_the_status_or_the_exception_type_and_never_a_message()
    {
        Assert.Equal("Success(NOT_FOUND(404))", Success(404).ToString());
        Assert.Equal("Failure(System.IO.IOException)", Failure().ToString());
        Assert.DoesNotContain(
            "secret.example",
            new Outcome.Failure(new IOException("https://secret.example/token")).ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TryGet_out_values_are_non_null_when_true()
    {
        Outcome outcome = Success();

        if (outcome.TryGetResponse(out var response))
        {
            Assert.Equal(200, response.Status.Code);
        }
        else
        {
            Assert.Fail("expected a success");
        }
    }
}
