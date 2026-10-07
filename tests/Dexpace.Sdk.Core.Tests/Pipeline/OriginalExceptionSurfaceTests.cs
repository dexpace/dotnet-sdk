// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

// CFG-19 (P5a-10): a failure surfaces as the original exception, never an AggregateException wrapper, through both
// entry points. Free on `await` and on a completed task's read; `.Result` and `.Wait()` are banned, the only producers.
[Trait("Category", "Unit")]
public sealed class OriginalExceptionSurfaceTests
{
    public static TheoryData<Exception> Failures => new()
    {
        new IOException("io"),
        new InvalidOperationException("invalid"),
        new TimeoutException("timeout"),
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task A_transport_failure_surfaces_as_the_original_exception_through_Send_and_SendAsync(Exception failure)
    {
        var transport = new RecordingTransport(_ => throw failure);
        var pipeline = new PipelineBuilder().Build(transport);
        var request = Request.Get("https://api.example.com/v1/items");
        var token = TestContext.Current.CancellationToken;

        var sync = Assert.Throws(failure.GetType(), () => pipeline.Send(request, token));
        var async = await Assert.ThrowsAsync(failure.GetType(), async () => await pipeline.SendAsync(request, token));

        Assert.Same(failure, sync);
        Assert.Same(failure, async);
    }
}
