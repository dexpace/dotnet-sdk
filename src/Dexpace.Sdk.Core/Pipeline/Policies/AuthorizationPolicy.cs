// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// Abstract base class for all auth policies placed at <see cref="PipelineStage.Auth"/>.
/// </summary>
/// <remarks>
/// <para>
/// This base class implements the cross-origin withholding contract: credentials are stamped only
/// when the current request's origin (scheme + host + port) matches the origin of the first
/// invocation. If a redirect has moved the request to a different origin, the credential header is
/// actively removed from the request before the continuation is called — providing defense-in-depth
/// independent of <see cref="RedirectPolicy"/>. A consumer who composes an auth policy without
/// <see cref="RedirectPolicy"/>, or with a custom redirect policy, cannot accidentally forward a
/// stale credential to a foreign origin.
/// </para>
/// <para>
/// The recorded origin is stored in <see cref="PipelineContext"/>'s property bag under the key
/// <c>"dexpace.auth.origin"</c>. On the very first invocation for a given context, the key is
/// absent: the base class records the current origin and proceeds to stamp. On each subsequent
/// invocation (redirect loop, retry) the base class compares the current origin to the recorded
/// one before stamping.
/// </para>
/// <para>
/// <b>HTTPS only.</b> Whenever a credential would be attached, a request URL whose scheme is not <c>https</c>
/// (compared case-insensitively) is rejected with an <see cref="SdkException"/> naming the policy and the scheme,
/// before <see cref="GetCredentialAsync"/> runs — so no token is fetched and no header is written. There is no
/// loopback exemption. A cross-origin hop, which carries no credential, is not checked.
/// </para>
/// <para>
/// Derived classes must implement <see cref="GetCredentialAsync"/> to supply the header name
/// and value to stamp, and <see cref="WithheldHeaderName"/> to identify the header to remove on a
/// cross-origin hop. The base class performs the <c>Headers.Set</c> / <c>Headers.Without</c>
/// writes and the <c>continuation.RunAsync</c> call. <see cref="WithheldHeaderName"/> is
/// accessed only on the cross-origin branch; it must not trigger credential resolution (e.g. a
/// token-cache lookup).
/// </para>
/// </remarks>
public abstract class AuthorizationPolicy : HttpPipelinePolicy
{
    // Property-bag key used to record the origin seen on the first invocation.
    private const string OriginKey = "dexpace.auth.origin";

    /// <inheritdoc/>
    public sealed override PipelineStage Stage => PipelineStage.Auth;

    /// <summary>
    /// The name of the HTTP header that this policy stamps on same-origin requests.
    /// On a cross-origin hop the base class removes this header from the outgoing request
    /// before calling the continuation — no credential resolution is performed.
    /// </summary>
    protected abstract HttpHeaderName WithheldHeaderName { get; }

    /// <inheritdoc/>
    /// <exception cref="SdkException">
    /// A credential would be attached to a request whose URL scheme is not <c>https</c>.
    /// </exception>
    public sealed override async ValueTask ProcessAsync(PipelineContext context, PipelineRunner continuation)
    {
        ArgumentNullException.ThrowIfNull(context);

        var currentOrigin = GetOrigin(context.Request.Url);

        var recordedOrigin = context.GetProperty<string>(OriginKey);
        if (recordedOrigin is null)
        {
            // First invocation for this context: record the origin, then stamp.
            context.SetProperty(OriginKey, currentOrigin);
        }
        else if (!string.Equals(recordedOrigin, currentOrigin, StringComparison.OrdinalIgnoreCase))
        {
            // Request has been redirected to a different origin — strip the credential header
            // (defense-in-depth: removes any stale value carried over from the original request)
            // and forward the request without credential.
            context.Request = context.Request with
            {
                Headers = context.Request.Headers.Without(WithheldHeaderName.Original)
            };

            await continuation.RunAsync(context).ConfigureAwait(false);
            return;
        }

        // AUTH-28 / XCUT-16: a credential is about to be attached, so refuse plaintext before any credential is
        // resolved. The cross-origin branch above attaches none and returns first (AUTH-29). No loopback exemption
        // (design §11 item 25).
        EnsureHttps(context.Request.Url);

        var (headerName, headerValue) = await GetCredentialAsync(context).ConfigureAwait(false);

        context.Request = context.Request with
        {
            Headers = context.Request.Headers.Set(headerName, headerValue)
        };

        await continuation.RunAsync(context).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the header name (as a string suitable for <see cref="Headers.Set(string,string)"/>)
    /// and the header value to stamp on the outgoing request.
    /// </summary>
    /// <param name="context">The current pipeline context.</param>
    /// <returns>
    /// A <see cref="ValueTask{T}"/> that resolves to a <c>(headerName, headerValue)</c> pair.
    /// </returns>
    protected abstract ValueTask<(string HeaderName, string HeaderValue)> GetCredentialAsync(
        PipelineContext context);

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
