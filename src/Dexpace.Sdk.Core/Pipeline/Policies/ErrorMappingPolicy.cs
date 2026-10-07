// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.Recovery;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A <see cref="PipelineStage.PerCall"/> policy that turns a 400..599 response into an
/// <see cref="HttpResponseException"/> carrying a replayable, bounded copy of the error body (PIPE-37).
/// </summary>
/// <remarks>
/// <para>
/// It is a thin wrapper that installs <see cref="ErrorMappingStep"/> in the stage pipeline through the response chain
/// fold. Because it sits at <see cref="PipelineStage.PerCall"/>, outside the redirect and retry loops, it observes only
/// the terminal response. A response whose status is outside 400..599 is returned untouched: its body is not read,
/// consumed or disposed (BODY-31). A response whose body is the empty replayable body (how an absent body is
/// represented) holds no connection, so it is mapped without a drain and the exception carries the response as it is
/// (BODY-30, P4c-18). A failure to buffer surfaces unchanged (RECOV-10).
/// </para>
/// <para>
/// The policy is not part of <see cref="DexpacePipeline.CreateDefault"/>: returning the <see cref="Response"/> for any
/// status is the default, and a caller who wants throw-by-default adds this policy. The same mapping is available per
/// response as <see cref="Response.EnsureSuccess"/> and <see cref="Response.EnsureSuccessAsync"/>.
/// </para>
/// </remarks>
public sealed class ErrorMappingPolicy : HttpPipelinePolicy
{
    private static readonly ResponseRecoveryChain s_chain = new([ErrorMappingStep.Instance], []);

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.PerCall;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(ErrorMappingPolicy));

    private static async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var response = async
            ? await continuation.RunAsync(request, context).ConfigureAwait(false)
            : continuation.Run(request, context);

        // PIPE-37 / BODY-31: any other status is returned untouched, and the fold is not entered.
        if (!response.Status.IsError)
        {
            return response;
        }

        // BODY-30 / P4c-18: a response with no body holds no connection; map it without a drain.
        if (response.Body.IsEmptyReplayable)
        {
            throw ErrorMapping.ToException(response);
        }

        var outcome = async
            ? await s_chain.ApplyAsync(new Outcome.Success(response), context.CancellationToken).ConfigureAwait(false)
            : s_chain.Apply(new Outcome.Success(response), context.CancellationToken);

        return Unwrap(outcome);
    }

    // RECOV-10: a success is the response; a failure is rethrown as the same instance with its stack.
    private static Response Unwrap(Outcome outcome)
    {
        if (outcome.TryGetResponse(out var response))
        {
            return response;
        }

        if (outcome.TryGetError(out var error))
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }

        throw new UnreachableException();
    }
}
