// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// What a <see cref="Configuration.RedirectOptions.Predicate"/> is told about the 3xx it is judging (REDIR-20, REDIR-21).
/// </summary>
/// <remarks>
/// A snapshot taken per decision: the visited list is a copy, so a predicate cannot reach the policy's live state. The
/// predicate may only <em>narrow</em> what the policy would do; it cannot override loop detection, the hop cap, the
/// <c>Location</c> screen, the downgrade guard, the replayability gate or credential stripping (P6b-7). Do not dispose
/// <see cref="Response"/> or read its body. This is a class rather than a record because a record would synthesise value
/// equality and <c>with</c> over a live response.
/// </remarks>
public sealed class RedirectCondition
{
    internal RedirectCondition(Response response, int redirectsFollowed, IReadOnlyList<Uri> visitedUris, Uri? target)
    {
        Response = response;
        RedirectsFollowed = redirectsFollowed;
        VisitedUris = visitedUris;
        Target = target;
    }

    /// <summary>The 3xx response being judged, still open. Read its status and headers; never dispose it or read its body.</summary>
    public Response Response { get; }

    /// <summary>The number of hops already followed: <c>0</c> on the first decision.</summary>
    public int RedirectsFollowed { get; }

    /// <summary>
    /// Every URI visited on this call, in order and distinct, the current request's included (REDIR-16). A copy: writing to
    /// it cannot affect the policy. Documented as a set although it is a list, because .NET has no ordered read-only set.
    /// </summary>
    public IReadOnlyList<Uri> VisitedUris { get; }

    /// <summary>
    /// The resolved, userinfo-free target this hop would go to, or <see langword="null"/> when the <c>Location</c> is
    /// missing or unusable. The policy's own resolution, so a predicate never parses it differently (P6b-8).
    /// </summary>
    public Uri? Target { get; }
}
