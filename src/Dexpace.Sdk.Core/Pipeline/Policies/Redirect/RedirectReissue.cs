// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// Builds the request for the next hop: credential stripping and the 303 rebuild (REDIR-5, REDIR-7, REDIR-9, REDIR-10,
/// REDIR-11; P6b-5, P6b-13). Adds nothing to the request: no marker, no <c>Referer</c>.
/// </summary>
internal static class RedirectReissue
{
    /// <summary>Builds the hop request from the policy's own copy of the current one.</summary>
    /// <param name="current">The request of the hop being left, as this policy holds it.</param>
    /// <param name="target">The resolved, userinfo-free target.</param>
    /// <param name="status">The redirect status.</param>
    /// <param name="crossOrigin">Whether <paramref name="target"/> leaves the seed origin.</param>
    /// <returns>The request to send next.</returns>
    internal static Request Build(Request current, Uri target, int status, bool crossOrigin)
    {
        // REDIR-7: every Authorization value, always. REDIR-9: Cookie and Proxy-Authorization, cross-origin. REDIR-10: a
        // same-origin hop keeps the last two. Re-attaching a credential is the auth policy's job, per hop.
        var headers = current.Headers.Without(HttpHeaderName.WellKnown.Authorization);
        if (crossOrigin)
        {
            headers = headers.Without("Cookie").Without("Proxy-Authorization");
        }

        if (status != 303)
        {
            return new Request(current.Method, target, headers, current.Body);
        }

        // A 303 becomes a body-less GET whatever the method (REDIR-5). One constructor call, never a With* chain:
        // WithMethod(Get) before the body is cleared would throw (HTTP-7). Every Content-* header goes with the body.
        foreach (var name in headers.Names.Where(IsContentHeader).ToArray())
        {
            headers = headers.Without(name);
        }

        return new Request(Method.Get, target, headers, body: null);
    }

    private static bool IsContentHeader(string name) => name.StartsWith("content-", StringComparison.OrdinalIgnoreCase);
}
