// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.TestSupport.Pipeline;

/// <summary>
/// A re-driving policy: it drives the downstream <paramref name="drives"/> times with the request it holds, disposing each
/// superseded response, and returns the last, unclosed (the fork contract, PIPE-15, PIPE-40).
/// </summary>
public sealed class ForkingProbe(PipelineStage stage, int drives) : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public override PipelineStage Stage => stage;

    /// <inheritdoc/>
    public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
    {
        Response? last = null;
        for (var i = 0; i < drives; i++)
        {
            if (last is not null)
            {
                await last.DisposeAsync().ConfigureAwait(false);
            }

            last = await continuation.RunAsync(request, context.ForHop(i)).ConfigureAwait(false);
        }

        return last!;
    }

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation)
    {
        Response? last = null;
        for (var i = 0; i < drives; i++)
        {
            last?.Dispose();
            last = continuation.Run(request, context.ForHop(i));
        }

        return last!;
    }
}
