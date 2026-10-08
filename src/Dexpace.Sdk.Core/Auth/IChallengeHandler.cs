// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// Answers authentication challenges: given what a server asked for, produces the request that answers it (AUTH-23,
/// AUTH-25).
/// </summary>
/// <remarks>
/// <para>
/// One method serves as both the "can handle" check and the "build" step (P6c-16): selecting twice would run Digest's
/// selection twice and advance its counter twice. The result is exactly what an auth policy's challenge hook returns
/// (AUTH-30): a replacement for the request, with the answer set under <c>Authorization</c> (or
/// <c>Proxy-Authorization</c> when <c>proxy</c> is <see langword="true"/>), or <see langword="null"/> when the handler
/// cannot answer.
/// </para>
/// <para>
/// Implementations are stateless apart from bounded counters and safe to call concurrently (AUTH-24). A forward proxy's
/// <c>407</c> never reaches the pipeline (P6c-13); the flag exists for callers composing their own proxy handling.
/// </para>
/// </remarks>
public interface IChallengeHandler
{
    /// <summary>Answers <paramref name="challenges"/> for <paramref name="request"/>.</summary>
    /// <param name="challenges">The parsed challenges, in wire order.</param>
    /// <param name="request">The request that drew the challenge. It is never mutated.</param>
    /// <param name="proxy">
    /// <see langword="true"/> to set <c>Proxy-Authorization</c> instead of <c>Authorization</c> (AUTH-25).
    /// </param>
    /// <returns>The replacement request, or <see langword="null"/> when the handler cannot answer.</returns>
    Request? Authorize(IReadOnlyList<AuthenticationChallenge> challenges, Request request, bool proxy);
}
