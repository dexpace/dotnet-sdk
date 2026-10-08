// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// The pure per-hop redirect judgement (REDIR-1 to REDIR-21; P6b-6). It does no I/O, reads no clock and has no side effect
/// beyond calling the caller's predicate, so a matrix row needs no pipeline. The gates run in a fixed order, one method each:
/// recognised status, <c>Location</c> resolution, eligibility, unusable target, loop, hop cap, downgrade, replay gate, build.
/// </summary>
internal static class RedirectDecider
{
    /// <summary>Decides what to do with <paramref name="response"/>, the answer to <c>chain.Current</c>.</summary>
    /// <param name="response">The response of the hop just driven.</param>
    /// <param name="chain">The call's redirect state.</param>
    /// <param name="options">The redirect options.</param>
    /// <returns>Follow, return the response open, or fail.</returns>
    internal static RedirectDecision Decide(Response response, RedirectChain chain, RedirectOptions options)
    {
        var status = response.Status.Code;

        // Gate 1 (REDIR-1, REDIR-2): nothing is allocated and the predicate is not called.
        if (!IsRecognised(status))
        {
            return RedirectDecision.ReturnCurrent(RedirectStopReason.NotARedirect);
        }

        // Gate 2 (REDIR-12, REDIR-14, REDIR-18, REDIR-19): resolve before the predicate so the snapshot can carry it.
        var outcome = RedirectLocation.TryResolve(chain.Current.Url, response.Headers, out var target, out var raw);

        // Gate 3 (REDIR-3 to REDIR-5, REDIR-20, REDIR-21): the snapshot is always allocated.
        var condition = chain.Snapshot(response, target);
        if (!IsEligible(status, chain, options, condition))
        {
            return RedirectDecision.ReturnCurrent(RedirectStopReason.NotEligible);
        }

        // Gate 4: a predicate is heard even with no usable Location, but can never force one (P6b-7).
        if (target is null)
        {
            return RedirectDecision.ReturnCurrent(
                RedirectStopReason.MalformedLocation,
                malformedRaw: outcome == LocationOutcome.Malformed ? raw : null);
        }

        return Gates5To9(response, status, target, chain, options);
    }

    private static RedirectDecision Gates5To9(Response response, int status, Uri target, RedirectChain chain, RedirectOptions options)
    {
        // Gate 5 (REDIR-16).
        if (chain.Contains(RedirectChain.KeyOf(target)))
        {
            return RedirectDecision.ReturnCurrent(RedirectStopReason.LoopDetected, target);
        }

        // Gate 6 (REDIR-17): with MaxRedirects = 0 this fails at hop 0, so "follow nothing" needs no branch of its own.
        if (chain.Followed >= options.MaxRedirects)
        {
            return RedirectDecision.ReturnCurrent(RedirectStopReason.HopCap, target);
        }

        // Gate 7 (REDIR-15; P6b-14): judged on this hop's transition, so https -> http -> https flags only the middle hop.
        var downgraded = IsDowngrade(chain.Current.Url, target);
        if (downgraded && !options.AllowHttpsToHttpDowngrade)
        {
            return RedirectDecision.Fail(
                new RedirectSchemeDowngradeException(RedirectMessages.Downgrade(status, chain.Current.Url, target)),
                RedirectFailureKind.SchemeDowngrade,
                target);
        }

        // Gate 8 (REDIR-6, BODY-4): a 303 drops the body so it is exempt. No idempotency is consulted (BODY-5).
        if (status != 303 && chain.Current.Body is { IsReplayable: false })
        {
            return RedirectDecision.Fail(
                new RedirectBodyNotReplayableException(RedirectMessages.NotReplayable(status, target)),
                RedirectFailureKind.BodyNotReplayable,
                target);
        }

        // Gate 9 (REDIR-3 to REDIR-5, REDIR-7 to REDIR-10): cross-origin is judged against the seed.
        var crossOrigin = HttpOrigin.From(target) != chain.SeedOrigin;
        var next = RedirectReissue.Build(chain.Current, target, status, crossOrigin);
        return RedirectDecision.Follow(next, target, crossOrigin, downgraded);
    }

    private static bool IsRecognised(int status) => status is 301 or 302 or 303 or 307 or 308;

    private static bool IsEligible(int status, RedirectChain chain, RedirectOptions options, RedirectCondition condition)
    {
        if (options.Predicate is { } predicate)
        {
            return predicate(condition);
        }

        return status == 303 ? options.FollowSeeOther : options.AllowedMethods.Contains(chain.OriginalMethod);
    }

    private static bool IsDowngrade(Uri current, Uri target) =>
        current.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && target.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
}
