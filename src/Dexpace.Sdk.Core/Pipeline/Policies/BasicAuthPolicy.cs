// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// An auth-stage pipeline policy that stamps HTTP Basic authentication (RFC 7617) on every
/// outgoing request, replacing any prior <c>Authorization</c> header value.
/// </summary>
/// <remarks>
/// <para>
/// The header value is <c>"Basic &lt;base64(username:password)&gt;"</c> where the token is the
/// UTF-8 Base64 encoding returned by <see cref="BasicCredential.ToBase64"/>.
/// </para>
/// <para>
/// Credentials are withheld when the request has been redirected to a different origin; see
/// <see cref="AuthorizationPolicy"/> for the cross-origin withholding contract.
/// </para>
/// </remarks>
public sealed class BasicAuthPolicy : AuthorizationPolicy
{
    // The header value is computed once by the credential and shared with BasicChallengeHandler (AUTH-14).
    private readonly BasicStamper _stamper;

    /// <summary>
    /// Initializes a <see cref="BasicAuthPolicy"/> with the given credential.
    /// </summary>
    /// <param name="credential">The Basic credential to stamp on every same-origin request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is <see langword="null"/>.</exception>
    public BasicAuthPolicy(BasicCredential credential)
        : base(new AuthDescriptor(new AuthRequirement(AuthScheme.Basic)), [AuthScheme.Basic])
    {
        ArgumentNullException.ThrowIfNull(credential);
        _stamper = new BasicStamper(credential);
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<HttpHeaderName> WithheldHeaderNames { get; } = [HttpHeaderName.WellKnown.Authorization];

    /// <inheritdoc/>
    protected override ValueTask<(string HeaderName, string HeaderValue)?> GetCredentialAsync(
        AuthRequirement requirement,
        Request request,
        PipelineContext context) =>
        new(GetCredential(requirement, request, context));

    /// <inheritdoc/>
    protected override (string HeaderName, string HeaderValue)? GetCredential(
        AuthRequirement requirement,
        Request request,
        PipelineContext context) =>
        _stamper.Stamp();
}
