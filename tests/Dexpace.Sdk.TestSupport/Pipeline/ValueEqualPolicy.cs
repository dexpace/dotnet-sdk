// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.TestSupport.Pipeline;

/// <summary>
/// A pass-through policy with value-based <see cref="Equals(object?)"/>: two distinct instances for one stage are
/// <see cref="Equals(object?)"/> and not <see cref="object.ReferenceEquals"/> (PIPE-6). It is a class, not a record, because a
/// record cannot inherit from the non-record <see cref="HttpPipelinePolicy"/>.
/// </summary>
public sealed class ValueEqualPolicy(PipelineStage stage) : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public override PipelineStage Stage => stage;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        continuation.RunAsync(request, context);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ValueEqualPolicy other && other.Stage == stage;

    /// <inheritdoc/>
    public override int GetHashCode() => (int)stage;
}
