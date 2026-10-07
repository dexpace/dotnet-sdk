// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.TestSupport.Recovery;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Recovery;

[Trait("Category", "Unit")]
public sealed class StepFailureTests
{
    private static async Task<Outcome> Convert(Exception thrown, Outcome current, bool async) =>
        async ? await StepFailure.ConvertAsync(thrown, current, async: true) : StepFailure.Convert(thrown, current);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_success_in_hand_is_released_exactly_once_and_the_thrown_exception_is_the_failure(bool async)
    {
        var body = new DisposalCountingBody();
        using var response = TestResponses.Create(Status.Ok, body: body);
        var thrown = new InvalidOperationException("boom");

        var outcome = await Convert(thrown, new Outcome.Success(response), async);

        var failure = Assert.IsType<Outcome.Failure>(outcome);
        Assert.Same(thrown, failure.Error);
        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_failing_release_lands_on_the_thrown_exceptions_trail_and_never_replaces_it(bool async)
    {
        var releaseFailure = new IOException("close failed");
        var body = new ProbeResponseBody(disposeFailure: releaseFailure);
        using var response = TestResponses.Create(Status.Ok, body: body);
        var thrown = new InvalidOperationException("boom");

        var outcome = await Convert(thrown, new Outcome.Success(response), async);

        Assert.Same(thrown, Assert.IsType<Outcome.Failure>(outcome).Error);
        Assert.Same(releaseFailure, Assert.Single(ExceptionTrail.GetSuppressed(thrown)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_failure_in_hand_releases_nothing(bool async)
    {
        var thrown = new InvalidOperationException("boom");

        var outcome = await Convert(thrown, new Outcome.Failure(new IOException("earlier")), async);

        Assert.Same(thrown, Assert.IsType<Outcome.Failure>(outcome).Error);
        Assert.Empty(ExceptionTrail.GetSuppressed(thrown));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_second_conversion_over_the_same_response_does_not_release_twice(bool async)
    {
        var body = new DisposalCountingBody();
        using var response = TestResponses.Create(Status.Ok, body: body);
        var success = new Outcome.Success(response);

        await Convert(new InvalidOperationException("one"), success, async);
        await Convert(new InvalidOperationException("two"), success, async);

        Assert.Equal(1, body.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_fatal_dispose_exception_propagates(bool async)
    {
        var body = new ProbeResponseBody(disposeFailure: new InsufficientMemoryException());
        using var response = TestResponses.Create(Status.Ok, body: body);

        await Assert.ThrowsAsync<InsufficientMemoryException>(
            () => Convert(new InvalidOperationException("boom"), new Outcome.Success(response), async));
    }
}
