// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Microsoft.Extensions.Logging;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// Factory for the default Dexpace SDK HTTP pipeline.
/// </summary>
/// <remarks>
/// <see cref="CreateDefault"/> assembles the standard policy set in the correct stage order:
/// <see cref="OperationPolicy"/> → <see cref="RedirectPolicy"/> → <see cref="IdempotencyPolicy"/>
/// → <see cref="ClientIdentityPolicy"/> → <see cref="RetryPolicy"/> → <see cref="SetDatePolicy"/>
/// → optional auth policy → <see cref="InstrumentationPolicy"/> → transport.
/// Because <see cref="PipelineBuilder"/> sorts by <see cref="HttpPipelinePolicy.Stage"/> at build
/// time, the insertion order of the <c>Add</c> calls here does not affect the final ordering.
/// </remarks>
public static class DexpacePipeline
{
    /// <summary>
    /// Builds the default HTTP pipeline with all standard policies wired in the correct stage order.
    /// </summary>
    /// <param name="transport">
    /// The transport that executes HTTP requests. Called after all policies have run.
    /// </param>
    /// <param name="authPolicy">
    /// An optional authentication policy inserted at <see cref="PipelineStage.Auth"/>. When
    /// <see langword="null"/> no auth policy is added.
    /// </param>
    /// <param name="logger">
    /// An optional logger forwarded to <see cref="InstrumentationPolicy"/>. When
    /// <see langword="null"/> the policy falls back to <c>NullLogger.Instance</c>.
    /// </param>
    /// <param name="timeProvider">
    /// An optional <see cref="TimeProvider"/> forwarded to <see cref="RetryPolicy"/> and
    /// <see cref="SetDatePolicy"/>. Defaults to <see cref="TimeProvider.System"/> when
    /// <see langword="null"/>.
    /// </param>
    /// <returns>A fully assembled <see cref="HttpPipeline"/> ready for use.</returns>
    public static HttpPipeline CreateDefault(
        IAsyncHttpClient transport,
        HttpPipelinePolicy? authPolicy = null,
        ILogger? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(transport);

        var builder = new PipelineBuilder()
            .Add(new OperationPolicy())
            .Add(new RedirectPolicy())
            .Add(new IdempotencyPolicy())
            .Add(new ClientIdentityPolicy())
            .Add(new RetryPolicy(timeProvider))
            .Add(new SetDatePolicy(timeProvider))
            .Add(new InstrumentationPolicy(logger));

        if (authPolicy is not null)
        {
            builder.Add(authPolicy);
        }

        return builder.Build(transport);
    }
}
