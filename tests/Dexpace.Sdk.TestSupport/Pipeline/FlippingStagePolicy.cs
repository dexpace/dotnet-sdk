// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.TestSupport.Pipeline;

/// <summary>
/// A policy whose <see cref="Stage"/> getter returns a different stage on each read, cycling through the stages it was
/// given (a virtual getter may legally do so).
/// </summary>
public sealed class FlippingStagePolicy(params PipelineStage[] stages) : HttpPipelinePolicy
{
    private int _reads;

    /// <summary>The number of times <see cref="Stage"/> was read.</summary>
    public int Reads => Volatile.Read(ref _reads);

    /// <inheritdoc/>
    public override PipelineStage Stage => stages[(Interlocked.Increment(ref _reads) - 1) % stages.Length];

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        continuation.RunAsync(request, context);
}
