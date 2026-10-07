// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Configuration;

/// <summary>
/// Options for the redirect-following policy.
/// </summary>
/// <remarks>
/// A sealed record with <see langword="init"/> accessors (CFG-8): derive a modified copy with <see langword="with"/>
/// (CFG-9). <para><b>Breaking:</b> this was a mutable class; assigning a property after construction no longer
/// compiles, and equality and the hash code are by value (was: by reference).</para>
/// </remarks>
public sealed record RedirectOptions
{
    /// <summary>
    /// The maximum number of redirect hops to follow. Defaults to <c>20</c>,
    /// matching browser and <c>HttpClient</c> norms.
    /// </summary>
    public int MaxRedirects { get; init; } = 20;

    /// <summary>
    /// When <see langword="true"/>, the policy follows <c>https → http</c> downgrade redirects.
    /// Defaults to <see langword="false"/> for security.
    /// </summary>
    public bool AllowHttpsToHttpDowngrade { get; init; }

    /// <summary>
    /// No longer has any effect. <see cref="Pipeline.Policies.RedirectPolicy"/> always strips
    /// <c>Authorization</c> on every hop and <c>Cookie</c> and <c>Proxy-Authorization</c> on a
    /// cross-origin hop, because REDIR-7 and REDIR-9 are MUSTs that a switch must not narrow.
    /// <b>Breaking</b> (phase 1): setting it to <see langword="false"/> used to keep those headers.
    /// Roadmap phase 6b removes the property (design §6.2).
    /// </summary>
    public bool StripSensitiveHeadersOnCrossOrigin { get; init; } = true;
}
