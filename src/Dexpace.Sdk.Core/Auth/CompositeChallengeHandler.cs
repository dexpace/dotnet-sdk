// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Auth;

/// <summary>
/// Tries several <see cref="IChallengeHandler"/>s in order and returns the first answer (AUTH-23).
/// </summary>
/// <remarks>
/// Callers order the stronger scheme first: <c>new CompositeChallengeHandler(digest, basic)</c>. The handlers are copied
/// at construction.
/// </remarks>
public sealed class CompositeChallengeHandler : IChallengeHandler
{
    private readonly ImmutableArray<IChallengeHandler> _handlers;

    /// <summary>Initializes the composite.</summary>
    /// <param name="handlers">The handlers, strongest first.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handlers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are none, or one is <see langword="null"/>.</exception>
    public CompositeChallengeHandler(params IEnumerable<IChallengeHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        var builder = ImmutableArray.CreateBuilder<IChallengeHandler>();
        foreach (var handler in handlers)
        {
            if (handler is null)
            {
                throw new ArgumentException("A handler must not be null.", nameof(handlers));
            }

            builder.Add(handler);
        }

        if (builder.Count == 0)
        {
            throw new ArgumentException("A composite needs at least one handler.", nameof(handlers));
        }

        _handlers = builder.ToImmutable();
    }

    /// <inheritdoc/>
    public Request? Authorize(IReadOnlyList<AuthenticationChallenge> challenges, Request request, bool proxy)
    {
        foreach (var handler in _handlers)
        {
            var answer = handler.Authorize(challenges, request, proxy);
            if (answer is not null)
            {
                return answer;
            }
        }

        return null;
    }
}
