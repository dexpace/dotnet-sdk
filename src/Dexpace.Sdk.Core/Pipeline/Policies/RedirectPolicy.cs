// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Common;

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
/// <b>Cross-origin header stripping:</b> when
/// <see cref="Configuration.RedirectOptions.StripSensitiveHeadersOnCrossOrigin"/> is
/// <see langword="true"/> and the new URL has a different origin (scheme/host/port), the
/// <c>Authorization</c> and <c>Cookie</c> headers are removed from the forwarded request.
/// </para>
/// </remarks>
public sealed class RedirectPolicy : HttpPipelinePolicy
{
    private static readonly HashSet<int> s_redirectStatuses = [301, 302, 303, 307, 308];

    /// <inheritdoc/>
    public override PipelineStage Stage => PipelineStage.Redirect;

    /// <inheritdoc/>
    public override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.Options.Redirect;
        var redirectCount = 0;

        while (true)
        {
            await continuation.RunAsync(context).ConfigureAwait(false);

            var response = context.Response;

            // No response (shouldn't happen) or non-redirect: hand off to caller.
            if (response is null || !s_redirectStatuses.Contains(response.Status.Code))
            {
                return;
            }

            // Redirect count exhausted: leave the 3xx for the caller.
            if (redirectCount >= options.MaxRedirects)
            {
                return;
            }

            // Extract Location header.
            var location = response.Headers.Get(HttpHeaderName.WellKnown.Location.Original);
            if (string.IsNullOrEmpty(location))
            {
                return;
            }

            // Resolve Location (handles relative URIs) against current request URL.
            var newUrl = new Uri(context.Request.Url, location);

            // HTTPS → HTTP downgrade guard.
            if (context.Request.Url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && newUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !options.AllowHttpsToHttpDowngrade)
            {
                return;
            }

            // Determine whether to preserve or drop method/body.
            var statusCode = response.Status.Code;
            var currentMethod = context.Request.Method;
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
            if (!dropBody && context.Request.Body is { IsReplayable: false })
            {
                return;
            }

            // Cross-origin header stripping.
            var newHeaders = context.Request.Headers;
            if (options.StripSensitiveHeadersOnCrossOrigin && IsCrossOrigin(context.Request.Url, newUrl))
            {
                newHeaders = newHeaders
                    .Without(HttpHeaderName.WellKnown.Authorization.Original)
                    .Without("Cookie");
            }

            // Dispose the current redirect response before issuing the next request.
            await response.DisposeAsync().ConfigureAwait(false);
            context.Response = null;

            context.Request = context.Request with
            {
                Url = newUrl,
                Method = newMethod,
                Headers = newHeaders,
                Body = dropBody ? null : context.Request.Body,
            };

            redirectCount++;
        }
    }

    private static bool IsCrossOrigin(Uri current, Uri redirected)
    {
        // Origins differ when scheme, host, or port differ.
        return !current.Scheme.Equals(redirected.Scheme, StringComparison.OrdinalIgnoreCase)
            || !current.Host.Equals(redirected.Host, StringComparison.OrdinalIgnoreCase)
            || current.Port != redirected.Port;
    }
}
