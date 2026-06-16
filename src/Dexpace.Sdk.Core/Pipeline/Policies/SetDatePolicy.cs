// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A per-attempt pipeline policy that stamps a fresh RFC 1123 <c>Date</c> header on each
/// outgoing request, replacing any value already present.
/// </summary>
/// <remarks>
/// Placed at <see cref="PipelineStage.PerAttempt"/>, this policy runs inside the retry loop so
/// that every attempt carries the current wall-clock time rather than the time at which the
/// original call was initiated.
/// </remarks>
public sealed class SetDatePolicy : HttpPipelinePolicy
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new <see cref="SetDatePolicy"/>.
    /// </summary>
    /// <param name="timeProvider">
    /// The time source used to obtain the current UTC instant. Defaults to
    /// <see cref="TimeProvider.System"/> when <see langword="null"/>.
    /// </param>
    public SetDatePolicy(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.PerAttempt;

    /// <inheritdoc/>
    public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
    {
        ArgumentNullException.ThrowIfNull(context);

        var dateValue = _timeProvider.GetUtcNow().ToString("r");
        context.Request = context.Request with
        {
            Headers = context.Request.Headers.Set(HttpHeaderName.WellKnown.Date.Original, dateValue)
        };

        await continuation.RunAsync(context).ConfigureAwait(false);
    }
}
