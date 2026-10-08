// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A redirect-following pipeline policy: each recognised 3xx is judged by a pure decision function over the configured
/// <see cref="Configuration.RedirectOptions"/> and either followed, returned to the caller open, or refused with a
/// <see cref="Errors.RedirectException"/> (REDIR-1 to REDIR-28).
/// </summary>
/// <remarks>
/// <para>
/// <b>What is followed.</b> Only 301, 302, 303, 307 and 308. A 301, 302, 307 or 308 is followed when the <em>original</em>
/// request method is in <see cref="Configuration.RedirectOptions.AllowedMethods"/> (default <c>{GET, HEAD}</c>), with the
/// method and the body preserved: a <c>POST</c> is never rewritten to a <c>GET</c> (REDIR-3). A 303 is followed only when
/// <see cref="Configuration.RedirectOptions.FollowSeeOther"/> is set, as a body-less <c>GET</c> that has lost every
/// <c>Content-*</c> header (REDIR-5). A <see cref="Configuration.RedirectOptions.Predicate"/> replaces that eligibility
/// decision and nothing else: it cannot defeat loop detection, the hop cap, the <c>Location</c> screen, the downgrade guard,
/// the replayability gate or credential stripping (P6b-7). Every other response is returned unchanged.
/// </para>
/// <para>
/// <b>When it stops quietly.</b> A missing, empty, unparseable, multi-valued, non-http(s) or host-less <c>Location</c>, a
/// target already visited on the call (loop), and the hop cap (<see cref="Configuration.RedirectOptions.MaxRedirects"/>,
/// default 3) each return the current 3xx response open. <c>MaxRedirects = 0</c> follows nothing: the async standard
/// pipeline from <see cref="DexpacePipeline"/> follows redirects, and this is how a caller asks for the 3xx itself
/// (REDIR-25, design section 10 entry 14).
/// </para>
/// <para>
/// <b>When it fails.</b> An https to http hop without <see cref="Configuration.RedirectOptions.AllowHttpsToHttpDowngrade"/>
/// throws <see cref="Errors.RedirectSchemeDowngradeException"/>, and a method-preserving hop over a body that cannot be
/// re-sent throws <see cref="Errors.RedirectBodyNotReplayableException"/> (buffer it with
/// <see cref="Http.Request.RequestBody.ToReplayableAsync(CancellationToken)"/>). Both dispose the response first.
/// </para>
/// <para>
/// <b>Request isolation.</b> Each hop is driven with the request this policy holds, and the continuation hop is built from
/// it, so a hop never carries what a downstream policy wrote during the previous one (PIPE-16). The seed origin is
/// <see cref="PipelineContext.SeedRequest"/>'s (REDIR-11). A superseded response is disposed before the next drive, and every
/// stop returns the in-flight response undisposed (PIPE-40, REDIR-22).
/// </para>
/// <para>
/// <b>Credential hygiene (REDIR-7 to REDIR-12, XCUT-17).</b> <c>Authorization</c> is removed before <em>every</em> re-issue,
/// same-origin included; re-attaching a credential is the auth policy's job, which runs per hop. When the target's origin
/// (scheme, host, effective port) differs from the <em>seed</em> request's, <c>Cookie</c> and <c>Proxy-Authorization</c> are
/// removed too. Userinfo in the target is dropped. These always apply and no option turns them off. A secret the caller
/// put in a custom header is not recognised and crosses origins; use <see cref="ApiKeyAuthPolicy"/> for such a key.
/// </para>
/// <para>
/// <b>Breaking:</b> a 301 or 302 on a <c>POST</c> was rewritten to a body-less <c>GET</c>; it is now followed only for a
/// method in <c>AllowedMethods</c>, preserving method and body, and otherwise returned.
/// </para>
/// <para>
/// <b>Breaking:</b> a 303 was followed by default; it now needs <c>FollowSeeOther</c>, and every <c>Content-*</c> header is
/// removed from the re-issued request.
/// </para>
/// <para>
/// <b>Breaking:</b> an https to http hop without the opt-in returned the 3xx; it now throws
/// <see cref="Errors.RedirectSchemeDowngradeException"/>. A method-preserving hop over a non-replayable body returned the 3xx;
/// it now throws <see cref="Errors.RedirectBodyNotReplayableException"/>.
/// </para>
/// <para>
/// <b>Breaking:</b> a redirect to a URI already visited on the call is returned instead of followed until the cap, a response
/// with more than one <c>Location</c> value is returned unfollowed, and the default cap is 3 (was 20).
/// </para>
/// <para>
/// <b>Cancellation.</b> The call's token is checked between hops, after the superseded response is disposed and before the
/// next drive, so a cancelled call never starts another hop.
/// </para>
/// </remarks>
public sealed class RedirectPolicy : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.Redirect;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(RedirectPolicy));

    private static async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Options.Redirect;
        var chain = new RedirectChain(request, context);

        // One loop, no recursion (REDIR-23). RETRY-44 / PIPE-16: each hop is driven with, and the next hop built from, the
        // request this policy holds, so a downstream stamp (auth) never reaches the continuation hop.
        while (true)
        {
            var hop = context.ForHop(chain.Followed);
            var response = async
                ? await continuation.RunAsync(chain.Current, hop).ConfigureAwait(false)
                : continuation.Run(chain.Current, hop);

            var decision = await DecideOrDisposeAsync(response, chain, options, context, async).ConfigureAwait(false);
            switch (decision.Kind)
            {
                case RedirectDecisionKind.ReturnCurrent:
                    // REDIR-22(c): every stop returns the in-flight response open.
                    return response;
                case RedirectDecisionKind.Fail:
                    // REDIR-22(b): nobody receives the response, so it is released first; a dispose failure rides the
                    // exception's suppressed trail and never replaces it.
                    await DisposeAsync(response, decision.Exception, context, async).ConfigureAwait(false);
                    throw decision.Exception!;
                default:
                    // REDIR-22(a): the superseded response is disposed before the next drive, never after it.
                    await DisposeAsync(response, primary: null, context, async).ConfigureAwait(false);

                    // P6b-18: a cancelled call never starts another hop.
                    context.CancellationToken.ThrowIfCancellationRequested();
                    chain.Advance(decision.Next!, decision.Target!);
                    break;
            }
        }
    }

    // P6b-9: the predicate is caller code. If it throws, the response is released and the exception propagates unchanged.
    private static async ValueTask<RedirectDecision> DecideOrDisposeAsync(
        Response response,
        RedirectChain chain,
        RedirectOptions options,
        PipelineContext context,
        bool async)
    {
        try
        {
            return RedirectDecider.Decide(response, chain, options);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            await DisposeAsync(response, ex, context, async).ConfigureAwait(false);
            throw;
        }
    }

    private static async ValueTask DisposeAsync(Response response, Exception? primary, PipelineContext context, bool async)
    {
        if (async)
        {
            await Disposal.DisposeQuietlyAsync(response, primary, context.State.Logger).ConfigureAwait(false);
        }
        else
        {
            Disposal.DisposeQuietly(response, primary, context.State.Logger);
        }
    }
}
