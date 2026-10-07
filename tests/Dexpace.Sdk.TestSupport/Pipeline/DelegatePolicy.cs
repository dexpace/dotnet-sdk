// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.TestSupport.Pipeline;

/// <summary>
/// A policy built from delegates, for tests that need one behaviour in one slot. The synchronous delegate defaults to
/// the asynchronous one's blocking bridge (the base <see cref="HttpPipelinePolicy.Process"/>).
/// </summary>
public sealed class DelegatePolicy(
    PipelineStage stage,
    Func<Request, PipelineContext, PipelineRunner, ValueTask<Response>> async,
    Func<Request, PipelineContext, PipelineRunner, Response>? sync = null) : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public override PipelineStage Stage => stage;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        async(request, context, continuation);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        sync is null ? base.Process(request, context, continuation) : sync(request, context, continuation);
}
