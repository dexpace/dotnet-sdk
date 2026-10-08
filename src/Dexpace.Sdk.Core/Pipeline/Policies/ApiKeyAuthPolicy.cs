// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// An auth-stage pipeline policy that stamps an API-key credential header on every outgoing
/// request, replacing any prior value for that header.
/// </summary>
/// <remarks>
/// <para>
/// The header and optional scheme prefix are taken directly from the
/// <see cref="ApiKeyCredential"/> supplied at construction time:
/// </para>
/// <list type="bullet">
///   <item>
///     When <see cref="ApiKeyCredential.Scheme"/> is <see langword="null"/> the header value is
///     exactly <see cref="ApiKeyCredential.Key"/>.
///   </item>
///   <item>
///     When <see cref="ApiKeyCredential.Scheme"/> is non-<see langword="null"/> the header value
///     is <c>"&lt;Scheme&gt; &lt;Key&gt;"</c> (e.g. <c>"Bearer sk-abc123"</c>).
///   </item>
/// </list>
/// <para>
/// The value is computed once by the credential (AUTH-26). Per-call <c>RequestOptions.Auth</c> tiers are honoured: a
/// <see cref="AuthScheme.NoAuth"/> descriptor sends the call anonymously. Credentials are withheld when the request has been redirected to a different origin; see
/// <see cref="AuthorizationPolicy"/> for the cross-origin withholding contract.
/// </para>
/// </remarks>
public sealed class ApiKeyAuthPolicy : AuthorizationPolicy
{
    private readonly ApiKeyStamper _stamper;
    private readonly HttpHeaderName[] _withheld;

    /// <summary>
    /// Initializes an <see cref="ApiKeyAuthPolicy"/> with the given credential.
    /// </summary>
    /// <param name="credential">The API-key credential to stamp on every same-origin request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is <see langword="null"/>.</exception>
    public ApiKeyAuthPolicy(ApiKeyCredential credential)
        : base(new AuthDescriptor(new AuthRequirement(AuthScheme.ApiKey)), [AuthScheme.ApiKey])
    {
        ArgumentNullException.ThrowIfNull(credential);
        _stamper = new ApiKeyStamper(credential);
        _withheld = [credential.HeaderName];
    }

    /// <inheritdoc/>
    protected override IReadOnlyList<HttpHeaderName> WithheldHeaderNames => _withheld;

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
