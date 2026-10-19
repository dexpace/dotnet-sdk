// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// The credential-stamping policies the invariant suite drives for <c>XCUT-16</c> and <c>XCUT-17</c>: one that stamps a
/// credential it already holds and one that fetches a token first. <c>XCUT-16</c> sends through each over <c>http://</c> and over
/// a permitted https-to-http hop; <c>XCUT-17</c> composes each under the redirect loop and requires it to stamp the seed's origin
/// again after a hop and to put no credential on a foreign host or on a retry of a foreign hop. A negative control replaces one
/// with a policy that stamps (or fetches) before it checks the scheme, or that stamps across origins.
/// </summary>
public sealed record AuthPolicyFactories
{
    /// <summary>Builds a policy that stamps the Basic credential <c>(username, password)</c>. Default: <see cref="BasicAuthPolicy"/>.</summary>
    public Func<string, string, HttpPipelinePolicy> Basic { get; init; } = static (user, password) => new BasicAuthPolicy(new BasicCredential(user, password));

    /// <summary>Builds a policy that fetches a bearer token from <see cref="TokenCredential"/> and stamps it. Default: <see cref="BearerTokenAuthPolicy"/>.</summary>
    public Func<TokenCredential, HttpPipelinePolicy> Bearer { get; init; } = static credential => new BearerTokenAuthPolicy(credential, "scope");
}
