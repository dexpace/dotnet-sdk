// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.TestSupport.Pipeline;

/// <summary>
/// A policy whose <see cref="ProcessAsync"/> is not <c>async</c> and throws before it returns a task: the shape a runtime
/// must turn into a faulted task (PIPE-30).
/// </summary>
public sealed class ThrowingSynchronouslyPolicy(PipelineStage stage, Exception exception) : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public override PipelineStage Stage => stage;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        throw exception;

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        throw exception;
}
