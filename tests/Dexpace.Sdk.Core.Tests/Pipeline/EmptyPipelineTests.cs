// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

// Case list from the phase 4c plan, which cites nodejs-sdk@c0ff3fd (the sibling source was not re-read while writing these): packages/core/src/pipeline/runtime.test.ts (empty pipeline, options threading).

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public sealed class EmptyPipelineTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_empty_pipeline_passes_request_options_and_token_to_the_transport(bool useAsync)
    {
        using var cts = new CancellationTokenSource();
        var request = Request.Get("https://api.example.com/v1/resource");
        var options = new RequestOptions { MaxRetries = 1 };
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder().Build(transport);

        using var response = useAsync
            ? await pipeline.SendAsync(request, options, cts.Token)
            : pipeline.Send(request, options, cts.Token);

        Assert.Same(request, transport.LastCall!.Request);
        Assert.Same(options, transport.LastCall!.Options);
        Assert.Equal(cts.Token, transport.LastCall!.CancellationToken);
    }
}
