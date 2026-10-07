// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// Base class for every policy in the HTTP pipeline.
/// </summary>
/// <remarks>
/// <para>
/// A policy receives the request, may call <c>continuation</c> any number of times, and returns the response it chooses
/// (PIPE-12). The request it passes to <c>continuation</c> is the request every downstream policy and the transport receive
/// (PIPE-14). A short-circuit is returning a synthetic <see cref="Response"/> without calling <c>continuation</c>. A policy that
/// re-drives (retry, redirect) calls <c>continuation</c> once per drive with the request it holds, so a downstream stamp can never
/// reach the continuation attempt (RETRY-44, PIPE-16).
/// </para>
/// <para>
/// <b>Two entry points.</b> <see cref="ProcessAsync"/> is the async path; <see cref="Process"/> is its synchronous twin.
/// The shipped policies implement both through one private core taking a <c>bool async</c> flag, so there is one body of
/// logic (PIPE-28). A third-party policy that overrides only <see cref="ProcessAsync"/> is bridged by the default
/// <see cref="Process"/>, which blocks on it; override both to keep the synchronous path non-blocking.
/// </para>
/// <para>
/// <b>Breaking:</b> was <c>ProcessAsync(PipelineContext, PipelineRunner) -&gt; ValueTask</c>, communicating through
/// mutable <c>PipelineContext.Request</c> and <c>Response</c>. It is now
/// <c>ProcessAsync(Request, PipelineContext, PipelineRunner) -&gt; ValueTask&lt;Response&gt;</c>.
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> there was no synchronous entry point and <c>HttpPipeline.Send</c> blocked on the async
/// chain; <see cref="Process"/> now exists and <c>Send</c> drives it.
/// </para>
/// </remarks>
public abstract class HttpPipelinePolicy
{
    /// <summary>
    /// The stage at which this policy is inserted in the pipeline. The builder reads it once, at insertion.
    /// </summary>
    public abstract PipelineStage Stage { get; }

    /// <summary>
    /// Asynchronously processes <paramref name="request"/> and returns the response.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="context">The call-scoped context.</param>
    /// <param name="continuation">The remainder of the pipeline; call it to forward a request, omit the call to short-circuit.</param>
    /// <returns>The response this policy chooses to return; it must not be <see langword="null"/>.</returns>
    public abstract ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation);

    /// <summary>
    /// Synchronously processes <paramref name="request"/> and returns the response.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="context">The call-scoped context.</param>
    /// <param name="continuation">The remainder of the pipeline.</param>
    /// <returns>The response this policy chooses to return.</returns>
    /// <remarks>
    /// The default is the documented blocking bridge over <see cref="ProcessAsync"/> (design §5.3); the shipped policies
    /// override it with a genuinely synchronous drive.
    /// </remarks>
    public virtual Response Process(Request request, PipelineContext context, PipelineRunner continuation)
    {
        // The documented sync bridge for a third-party policy that only implements the async path (design §5.3).
#pragma warning disable RS0030
        return ProcessAsync(request, context, continuation).AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030
    }
}
