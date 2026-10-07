// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

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
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(SetDatePolicy));

    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var dateValue = HttpDate.Format(_timeProvider.GetUtcNow());
        var stamped = request.WithHeaders(request.Headers.Set(HttpHeaderName.WellKnown.Date, dateValue));

        return async
            ? await continuation.RunAsync(stamped, context).ConfigureAwait(false)
            : continuation.Run(stamped, context);
    }
}
