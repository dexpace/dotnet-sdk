// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.TestSupport.Pipeline;

/// <summary>
/// A policy that returns a synthetic response without calling the downstream (PIPE-12).
/// </summary>
public sealed class ShortCircuitPolicy(PipelineStage stage, Response response) : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public override PipelineStage Stage => stage;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ValueTask.FromResult(response);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) => response;
}
