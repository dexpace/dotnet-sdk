// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// What an auth policy's challenge hook sees when a server answers <c>401</c> with <c>WWW-Authenticate</c> (AUTH-30).
/// </summary>
/// <remarks>
/// Constructed only by <see cref="AuthorizationPolicy"/>. It is a class, not a record: it holds a disposable
/// <see cref="Response"/>, and structural equality over one would be a trap.
/// </remarks>
public sealed class AuthChallengeContext
{
    internal AuthChallengeContext(
        AuthRequirement requirement,
        Request request,
        Response response,
        IReadOnlyList<AuthenticationChallenge> challenges,
        PipelineContext context)
    {
        Requirement = requirement;
        Request = request;
        Response = response;
        Challenges = challenges;
        Context = context;
    }

    /// <summary>The requirement the call was resolved to.</summary>
    public AuthRequirement Requirement { get; }

    /// <summary>The request that drew the <c>401</c>, as stamped.</summary>
    public Request Request { get; }

    /// <summary>The <c>401</c> response. The hook must not dispose it.</summary>
    public Response Response { get; }

    /// <summary>Every challenge in every <c>WWW-Authenticate</c> field, in wire order.</summary>
    public IReadOnlyList<AuthenticationChallenge> Challenges { get; }

    /// <summary>The call-scoped pipeline context.</summary>
    public PipelineContext Context { get; }
}
