// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Ported from nodejs-sdk@c0ff3fd packages/core/src/recovery/status-mapping.test.ts.
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class ErrorMappingStepTests
{
    public static TheoryData<int, bool> NonErrors()
    {
        var data = new TheoryData<int, bool>();
        foreach (var code in new[] { 100, 199, 200, 204, 299, 304, 399, 600, 999 })
        {
            data.Add(code, true);
            data.Add(code, false);
        }

        return data;
    }

    public static TheoryData<int, bool> Errors()
    {
        var data = new TheoryData<int, bool>();
        foreach (var code in new[] { 400, 404, 429, 499, 500, 503, 599 })
        {
            data.Add(code, true);
            data.Add(code, false);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NonErrors))]
    public async Task A_non_error_status_returns_the_same_instance_with_an_unopened_body(int code, bool async)
    {
        var body = new ProbeResponseBody([1, 2, 3]);
        using var response = TestResponses.Create(Status.FromCode(code), body: body);

        var result = await Run.Apply(ErrorMappingStep.Instance, response, async);

        Assert.Same(response, result);
        Assert.Equal(0, body.OpenCount);
        Assert.Equal(0, body.DisposeCount);
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task A_4xx_or_5xx_status_becomes_HttpResponseException_with_the_status(int code, bool async)
    {
        var payload = new byte[] { 9, 8, 7 };
        using var response = TestResponses.Create(Status.FromCode(code), body: new ProbeResponseBody(payload));

        var thrown = await Assert.ThrowsAsync<HttpResponseException>(() => Run.Apply(ErrorMappingStep.Instance, response, async));

        Assert.Equal(code, thrown.Response.Status.Code);
        Assert.Equal(payload, await thrown.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(payload, await thrown.Response.Body.ReadAsBytesAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_step_throws_HttpResponseException_it_does_not_return_it(bool async)
    {
        using var response = TestResponses.Create(Status.NotFound);

        await Assert.ThrowsAsync<HttpResponseException>(() => Run.Apply(ErrorMappingStep.Instance, response, async));
    }

    [Fact]
    public async Task The_step_is_a_singleton_and_stateless()
    {
        Assert.Same(ErrorMappingStep.Instance, ErrorMappingStep.Instance);

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 32),
            TestContext.Current.CancellationToken,
            async (i, ct) =>
            {
                using var response = TestResponses.Create(i % 2 == 0 ? Status.Ok : Status.BadGateway, body: new ProbeResponseBody([1]));
                if (i % 2 == 0)
                {
                    Assert.Same(response, await ErrorMappingStep.Instance.ApplyAsync(response, ct));
                }
                else
                {
                    await Assert.ThrowsAsync<HttpResponseException>(async () => await ErrorMappingStep.Instance.ApplyAsync(response, ct));
                }
            });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_step_runs_inside_a_response_chain_and_the_failure_reaches_recovery_steps(bool async)
    {
        var body = new ProbeResponseBody([1]);
        using var response = TestResponses.Create(Status.ServiceUnavailable, body: body);
        Outcome? seen = null;
        var chain = new ResponseRecoveryChain([ErrorMappingStep.Instance], [new DelegateRecoveryStep((o, _) => seen = o)]);

        await Run.Apply(chain, new Outcome.Success(response), async);

        var failure = Assert.IsType<Outcome.Failure>(seen);
        Assert.IsType<HttpResponseException>(failure.Error);
        Assert.Equal(1, body.DisposeCount);
    }
}
