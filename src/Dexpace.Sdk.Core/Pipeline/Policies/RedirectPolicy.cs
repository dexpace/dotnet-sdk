// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// A redirect-following pipeline policy that processes 3xx responses according to the
/// configured <see cref="Configuration.RedirectOptions"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Followed status codes:</b> 301, 302, 303, 307, 308. All other responses are returned
/// to the caller unchanged.
/// </para>
/// <para>
/// <b>Method and body handling:</b>
/// <list type="bullet">
///   <item>303 always becomes <c>GET</c> with no body.</item>
///   <item>301 or 302 on a <c>POST</c> request becomes <c>GET</c> with no body (legacy browser behavior).</item>
///   <item>307 and 308, and 301/302 on non-POST methods, preserve the original method and body.</item>
/// </list>
/// </para>
/// <para>
/// <b>Non-replayable body guard:</b> when the redirect would preserve the body and the body
/// is non-null with <see cref="Http.Request.RequestBody.IsReplayable"/> <see langword="false"/>,
/// the redirect is <em>not</em> followed; the 3xx response is returned to the caller.
/// </para>
/// <para>
/// <b>HTTPS → HTTP downgrade:</b> refused unless
/// <see cref="Configuration.RedirectOptions.AllowHttpsToHttpDowngrade"/> is
/// <see langword="true"/>.
/// </para>
/// <para>
/// <b>Request isolation:</b> each hop is driven with the request this policy holds, and the continuation hop is built from
/// it, so a hop never carries what a downstream policy wrote during the previous one (PIPE-16). The seed origin is
/// <see cref="PipelineContext.SeedRequest"/>'s (REDIR-11), and a superseded response is disposed before the continuation drive
/// while every path that stops following returns the in-flight response undisposed (PIPE-40).
/// </para>
/// <para>
/// <b>Credential hygiene (REDIR-7, REDIR-8, REDIR-9, REDIR-12, XCUT-17):</b> <c>Authorization</c> is removed before
/// <em>every</em> re-issue, same-origin included; re-attaching a credential is the auth policy's job, which runs per
/// hop. When the target's origin (scheme, case-insensitive host, effective port) differs from the <em>seed</em>
/// request's — the request this policy received, not the previous hop — <c>Cookie</c> and
/// <c>Proxy-Authorization</c> are removed too. Userinfo in the <c>Location</c> target is dropped before re-issue.
/// These always apply; <see cref="Configuration.RedirectOptions.StripSensitiveHeadersOnCrossOrigin"/> no longer turns
/// them off. <b>Breaking</b> (phase 1): a same-origin hop no longer keeps <c>Authorization</c>.
/// </para>
/// </remarks>
public sealed class RedirectPolicy : HttpPipelinePolicy
{
    private static readonly HashSet<int> s_redirectStatuses = [301, 302, 303, 307, 308];

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.Redirect;

    /// <inheritdoc/>
    public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(RedirectPolicy));

    // MA0051 waiver: Roadmap phase 6b rewrites the redirect policy (hop cap, loop detection, allowed-method set,
    // downgrade and replayability errors); splitting it now would be rewritten there.
#pragma warning disable MA0051
    private static async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Options.Redirect;
        var redirectCount = 0;

        // REDIR-8 / REDIR-11: cross-origin is judged against the seed request on the call-scoped context, never the
        // previous hop and never a request a policy above rewrote.
        var seedUrl = context.SeedRequest.Url;

        // RETRY-44 / PIPE-16: each hop is driven with, and the continuation hop built from, the request this policy holds; a
        // downstream stamp (auth) is on the callee's copy and never reaches the continuation hop.
        while (true)
        {
            var hop = context.ForHop(redirectCount);
            var response = async
                ? await continuation.RunAsync(request, hop).ConfigureAwait(false)
                : continuation.Run(request, hop);

            // PIPE-40: every path below that does not follow the redirect returns the in-flight response undisposed.
            if (!s_redirectStatuses.Contains(response.Status.Code))
            {
                return response;
            }

            // Redirect count exhausted: leave the 3xx for the caller.
            if (redirectCount >= options.MaxRedirects)
            {
                return response;
            }

            // Extract Location header.
            var location = response.Headers.Get(HttpHeaderName.WellKnown.Location);
            if (string.IsNullOrEmpty(location))
            {
                return response;
            }

            // Resolve Location (handles relative URIs) against current request URL.
            // Use TryCreate so a malformed Location value from the server doesn't throw a raw
            // UriFormatException through the pipeline — treat it as non-followable instead.
            if (!Uri.TryCreate(request.Url, location, out var newUrl))
            {
                return response;
            }

            // REDIR-12: server-supplied credentials in the target are never used.
            newUrl = WithoutUserInfo(newUrl);

            // A hop must be http(s): a Location such as ftp: or mailto: cannot become a Request (HTTP-47), so the 3xx
            // is returned unfollowed, as for a malformed Location (REDIR-18), before the response is disposed.
            if (!newUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !newUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return response;
            }

            // HTTPS → HTTP downgrade guard.
            if (request.Url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && newUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !options.AllowHttpsToHttpDowngrade)
            {
                return response;
            }

            // Determine whether to preserve or drop method/body.
            var statusCode = response.Status.Code;
            var currentMethod = request.Method;
            bool dropBody;
            Method newMethod;

            if (statusCode == 303)
            {
                // 303 → always GET + drop body.
                newMethod = Method.Get;
                dropBody = true;
            }
            else if ((statusCode == 301 || statusCode == 302) && currentMethod == Method.Post)
            {
                // 301/302 on POST → GET + drop body (legacy browser behavior).
                newMethod = Method.Get;
                dropBody = true;
            }
            else
            {
                // 307, 308, and 301/302 on non-POST: preserve method + body.
                newMethod = currentMethod;
                dropBody = false;
            }

            // Non-replayable body guard: if body must be kept but cannot be replayed, stop.
            if (!dropBody && request.Body is { IsReplayable: false })
            {
                return response;
            }

            // REDIR-7: Authorization is removed before every hop. REDIR-9: Cookie and Proxy-Authorization, cross-origin.
            var newHeaders = request.Headers.Without(HttpHeaderName.WellKnown.Authorization);
            if (IsCrossOrigin(seedUrl, newUrl))
            {
                newHeaders = newHeaders.Without("Cookie").Without("Proxy-Authorization");
            }

            // PIPE-40: release the superseded response before the continuation drive. A throwing dispose is suppressed so it
            // cannot mask the continuation drive's outcome.
            if (async)
            {
                await Disposal.DisposeQuietlyAsync(response, logger: context.State.Logger).ConfigureAwait(false);
            }
            else
            {
                Disposal.DisposeQuietly(response, logger: context.State.Logger);
            }

            // One constructor call, never a With* chain: WithMethod(Method.Get) before the body is cleared would throw
            // (HTTP-7) on a 303 or a 301/302-on-POST.
            request = new Request(newMethod, newUrl, newHeaders, dropBody ? null : request.Body);

            redirectCount++;
        }
    }
#pragma warning restore MA0051

    private static Uri WithoutUserInfo(Uri url) =>
        url.UserInfo.Length == 0
            ? url
            : new Uri(url.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.UserInfo, UriFormat.UriEscaped));

    private static bool IsCrossOrigin(Uri current, Uri redirected)
    {
        // Origins differ when scheme, host, or port differ.
        return !current.Scheme.Equals(redirected.Scheme, StringComparison.OrdinalIgnoreCase)
            || !current.Host.Equals(redirected.Host, StringComparison.OrdinalIgnoreCase)
            || current.Port != redirected.Port;
    }
}
