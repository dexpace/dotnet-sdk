// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// Abstract base class for all auth policies placed at <see cref="PipelineStage.Auth"/>.
/// </summary>
/// <remarks>
/// <para>
/// This base class implements the cross-origin withholding contract: credentials are stamped only
/// when the current request's origin (scheme + host + port) matches the origin of the
/// <see cref="PipelineContext.SeedRequest"/>. If a redirect has moved the request to a different origin, the credential
/// header is actively removed from the request before the continuation policy is called — providing defense-in-depth
/// independent of <see cref="RedirectPolicy"/>. A consumer who composes an auth policy without
/// <see cref="RedirectPolicy"/>, or with a custom redirect policy, cannot accidentally forward a
/// stale credential to a foreign origin.
/// </para>
/// <para>
/// The origin is the one of the request handed to <c>Send</c>/<c>SendAsync</c>, fixed on the call-scoped context at
/// entry (REDIR-24, AUTH-29, design §6.2); no policy can overwrite it.
/// </para>
/// <para>
/// <b>HTTPS only.</b> Whenever a credential would be attached, a request URL whose scheme is not <c>https</c>
/// (compared case-insensitively) is rejected with an <see cref="SdkException"/> naming the policy and the scheme,
/// before <see cref="GetCredentialAsync"/> or <see cref="GetCredential"/> runs — so no token is fetched and no header is
/// written. There is no loopback exemption. A cross-origin hop, which carries no credential, is not checked. Both entry
/// points are sealed, so a subclass cannot skip the guard on either path (AUTH-28).
/// </para>
/// <para>
/// Derived classes must implement <see cref="GetCredentialAsync"/> to supply the header name
/// and value to stamp, and <see cref="WithheldHeaderName"/> to identify the header to remove on a
/// cross-origin hop. The base class performs the <c>Headers.Set</c> / <c>Headers.Without</c>
/// writes and the <c>continuation</c> call. <see cref="WithheldHeaderName"/> is
/// accessed only on the cross-origin branch; it must not trigger credential resolution (e.g. a
/// token-cache lookup). A subclass with a synchronous credential overrides <see cref="GetCredential"/> too.
/// </para>
/// <para>
/// <b>Breaking (behaviour):</b> the origin compared against was the first origin the policy saw on the context, stored
/// under the public string key <c>"dexpace.auth.origin"</c> that any policy could overwrite; it is now the seed
/// request's. <b>Breaking:</b> the signature moved to <c>ProcessAsync(Request, PipelineContext, PipelineRunner)</c>, and
/// <see cref="HttpPipelinePolicy.Process"/> is sealed here as well.
/// </para>
/// </remarks>
public abstract class AuthorizationPolicy : HttpPipelinePolicy
{
    /// <inheritdoc/>
    public sealed override PipelineStage Stage => PipelineStage.Auth;

    /// <summary>
    /// The name of the credential header to remove when a request leaves the seed origin.
    /// </summary>
    protected abstract HttpHeaderName WithheldHeaderName { get; }

    /// <inheritdoc/>
    public sealed override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public sealed override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(AuthorizationPolicy));

    /// <summary>
    /// Resolves the credential header to stamp, asynchronously.
    /// </summary>
    /// <param name="context">The call-scoped context.</param>
    /// <returns>The header name and value.</returns>
    protected abstract ValueTask<(string HeaderName, string HeaderValue)> GetCredentialAsync(
        PipelineContext context);

    /// <summary>
    /// Resolves the credential header to stamp, synchronously.
    /// </summary>
    /// <param name="context">The call-scoped context.</param>
    /// <returns>The header name and value.</returns>
    /// <remarks>
    /// The default is the documented blocking bridge over <see cref="GetCredentialAsync"/> (design §5.3), because the
    /// token cache has no synchronous path until phase 6c; a credential that is available without I/O overrides it.
    /// </remarks>
    protected virtual (string HeaderName, string HeaderValue) GetCredential(PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The documented sync bridge until 6c gives AccessTokenCache a synchronous path (design §5.3, P4c-13).
#pragma warning disable RS0030
        return GetCredentialAsync(context).AsTask().GetAwaiter().GetResult();
#pragma warning restore RS0030
    }

    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        // REDIR-24 / AUTH-29: the origin is the seed request's, fixed at call entry and out of any policy's reach.
        if (!string.Equals(GetOrigin(context.SeedRequest.Url), GetOrigin(request.Url), StringComparison.OrdinalIgnoreCase))
        {
            // The request has been redirected to a different origin: strip the credential header (defense-in-depth,
            // removing any stale value carried over) and forward the request without credential.
            var stripped = request.WithHeaders(request.Headers.Without(WithheldHeaderName));
            return async
                ? await continuation.RunAsync(stripped, context).ConfigureAwait(false)
                : continuation.Run(stripped, context);
        }

        // AUTH-28 / XCUT-16: a credential is about to be attached, so refuse plaintext before any credential is
        // resolved. The cross-origin branch above attaches none and returns first (AUTH-29). No loopback exemption
        // (design §11 item 25).
        EnsureHttps(request.Url);

        var (headerName, headerValue) = async
            ? await GetCredentialAsync(context).ConfigureAwait(false)
            : GetCredential(context);

        var stamped = request.WithHeaders(request.Headers.Set(headerName, headerValue));
        return async
            ? await continuation.RunAsync(stamped, context).ConfigureAwait(false)
            : continuation.Run(stamped, context);
    }

    // Not ServiceRequestException, so no retry policy re-drives it: the request was refused, not failed.
    private void EnsureHttps(Uri url)
    {
        if (!string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new SdkException(
                $"{GetType().Name} refused to attach a credential to a request over the '{url.Scheme}' scheme; " +
                "credentials are only sent over https.");
        }
    }

    // Derives a canonical origin string: "<lower-scheme>://<lower-host>:<port>".
    // Port is always included. For a URL written without a port, Uri.Port returns the scheme's
    // default (443 for https, 80 for http), not -1, so "https://a/" and "https://a:443/" yield
    // the same origin whether or not the caller supplied the port explicitly. Uri.Port is -1
    // only for a scheme with no known default port.
    private static string GetOrigin(Uri uri)
    {
        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var port = uri.Port; // the scheme default (443, 80) when the URL omits the port
        return $"{scheme}://{host}:{port}";
    }
}
