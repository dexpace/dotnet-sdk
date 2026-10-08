// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.Immutable;
using Dexpace.Sdk.Core.Auth;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;

namespace Dexpace.Sdk.Core.Pipeline.Policies;

/// <summary>
/// Abstract base class for all auth policies placed at <see cref="PipelineStage.Auth"/>: it owns the whole
/// authorization lifecycle, and a subclass supplies the credential and, optionally, the answer to a challenge.
/// </summary>
/// <remarks>
/// <para>
/// For each drive the base, in this order: (1) on a cross-origin hop (a redirect moved the request off the origin of
/// <see cref="PipelineContext.SeedRequest"/>) removes every <see cref="WithheldHeaderNames"/> header, drives once and
/// returns the response without any challenge handling (AUTH-29, P6c-12); (2) resolves the requirement from
/// <see cref="RequestOptions.Auth"/>, <see cref="RequestOptions.OperationAuth"/> and the client descriptor
/// (AUTH-4 to AUTH-6); (3) sends a <see cref="AuthScheme.NoAuth"/> call anonymously; (4) refuses a non-<c>https</c> URL
/// with <see cref="HttpsRequiredException"/> before any credential is resolved (AUTH-28); (5) stamps the credential, if
/// the subclass supplies one; (6) drives; (7) on a <c>401</c> with <c>WWW-Authenticate</c> parses every field and calls
/// <see cref="OnChallenge"/> / <see cref="OnChallengeAsync"/> (AUTH-30, AUTH-33); (8) checks the replacement is
/// same-origin and replayable (AUTH-31); (9) disposes the <c>401</c> and drives the replacement once, returning whatever
/// comes back with no second hook call.
/// </para>
/// <para>
/// A hook that throws, synchronously or asynchronously, has the <c>401</c> disposed before the exception propagates
/// (AUTH-32). A replacement with a non-replayable body is not sent and the original <c>401</c> is returned, undisposed
/// (AUTH-31). Both entry points are sealed, so a subclass cannot skip the guard on either path (AUTH-28), and
/// <see cref="ProcessAsync"/> reports every failure through the returned task, never by throwing (AUTH-38).
/// </para>
/// <para>
/// <b>HTTPS only.</b> There is no loopback exemption, and the guard also covers the outbound pass of a reactive scheme
/// (Digest, Basic on challenge), which attaches a credential on the first <c>401</c> (P6c-8).
/// </para>
/// <para>
/// <b>Breaking:</b> the protected surface is a constructor taking the client descriptor and available schemes,
/// <see cref="WithheldHeaderNames"/> (was <c>WithheldHeaderName</c>), and abstract <see cref="GetCredentialAsync"/> and
/// <see cref="GetCredential"/> that take the resolved requirement and the request and may return <see langword="null"/>
/// (the sync-over-async default is gone). The HTTPS refusal is an <see cref="HttpsRequiredException"/> (was
/// <see cref="SdkException"/>). Every auth policy now honours the per-call and operation tiers and answers a
/// <c>401</c> per its hook.
/// </para>
/// </remarks>
public abstract class AuthorizationPolicy : HttpPipelinePolicy
{
    private readonly AuthDescriptor _clientDescriptor;
    private readonly ImmutableArray<AuthScheme> _availableSchemes;

    /// <summary>Initializes the base.</summary>
    /// <param name="clientDescriptor">The client tier: what this policy authenticates with by default.</param>
    /// <param name="availableSchemes">The schemes this policy can serve.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    protected AuthorizationPolicy(AuthDescriptor clientDescriptor, IEnumerable<AuthScheme> availableSchemes)
    {
        ArgumentNullException.ThrowIfNull(clientDescriptor);
        ArgumentNullException.ThrowIfNull(availableSchemes);
        _clientDescriptor = clientDescriptor;
        _availableSchemes = [.. availableSchemes];
    }

    /// <inheritdoc/>
    public sealed override PipelineStage Stage => PipelineStage.Auth;

    /// <summary>
    /// The names of the credential headers to remove when a request leaves the seed origin.
    /// </summary>
    /// <remarks>Read only on the cross-origin branch; it must not trigger credential resolution.</remarks>
    protected abstract IReadOnlyList<HttpHeaderName> WithheldHeaderNames { get; }

    /// <inheritdoc/>
    public sealed override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
        ProcessCoreAsync(request, context, continuation, async: true);

    /// <inheritdoc/>
    public sealed override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
        SyncPath.GetCompletedResult(ProcessCoreAsync(request, context, continuation, async: false), nameof(AuthorizationPolicy));

    /// <summary>Resolves the credential header to stamp, asynchronously.</summary>
    /// <param name="requirement">The requirement the call resolved to.</param>
    /// <param name="request">The request about to be sent.</param>
    /// <param name="context">The call-scoped context.</param>
    /// <returns>The header name and value, or <see langword="null"/> when there is nothing to attach on this pass.</returns>
    protected abstract ValueTask<(string HeaderName, string HeaderValue)?> GetCredentialAsync(
        AuthRequirement requirement,
        Request request,
        PipelineContext context);

    /// <summary>Resolves the credential header to stamp, synchronously.</summary>
    /// <param name="requirement">The requirement the call resolved to.</param>
    /// <param name="request">The request about to be sent.</param>
    /// <param name="context">The call-scoped context.</param>
    /// <returns>The header name and value, or <see langword="null"/> when there is nothing to attach on this pass.</returns>
    protected abstract (string HeaderName, string HeaderValue)? GetCredential(
        AuthRequirement requirement,
        Request request,
        PipelineContext context);

    /// <summary>Answers a <c>401</c> challenge, asynchronously.</summary>
    /// <param name="challenge">What the server asked for.</param>
    /// <returns>
    /// The replacement request to send once, or <see langword="null"/> to return the <c>401</c> unchanged. The default is
    /// <see langword="null"/>.
    /// </returns>
    protected virtual ValueTask<Request?> OnChallengeAsync(AuthChallengeContext challenge) => new((Request?)null);

    /// <summary>Answers a <c>401</c> challenge, synchronously.</summary>
    /// <param name="challenge">What the server asked for.</param>
    /// <returns>
    /// The replacement request to send once, or <see langword="null"/> to return the <c>401</c> unchanged. The default is
    /// <see langword="null"/>.
    /// </returns>
    protected virtual Request? OnChallenge(AuthChallengeContext challenge) => null;

    private static async ValueTask<Response> DriveAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async) =>
        async
            ? await continuation.RunAsync(request, context).ConfigureAwait(false)
            : continuation.Run(request, context);

    private static async ValueTask ReleaseAsync(Response response, PipelineContext context, bool async, Exception? primary = null)
    {
        if (async)
        {
            await Disposal.DisposeQuietlyAsync(response, primary, context.State.Logger).ConfigureAwait(false);
        }
        else
        {
            Disposal.DisposeQuietly(response, primary, context.State.Logger);
        }
    }

    private static List<AuthenticationChallenge> ReadChallenges(IReadOnlyList<string> fields)
    {
        // P6c-14: each field is parsed on its own, so an unterminated quote in one cannot swallow the next.
        var challenges = new List<AuthenticationChallenge>();
        foreach (var field in fields)
        {
            challenges.AddRange(AuthenticationChallenge.Parse(field));
        }

        return challenges;
    }

    private async ValueTask<Response> ProcessCoreAsync(Request request, PipelineContext context, PipelineRunner continuation, bool async)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        // AUTH-29 / REDIR-24: the origin is the seed request's, fixed at call entry and out of any policy's reach. A
        // foreign-origin hop carries no credential and is never challenge-handled (P6c-12).
        if (!AuthOrigin.Same(context.SeedRequest.Url, request.Url))
        {
            var stripped = request;
            foreach (var name in WithheldHeaderNames)
            {
                stripped = stripped.WithHeaders(stripped.Headers.Without(name));
            }

            return await DriveAsync(stripped, context, continuation, async).ConfigureAwait(false);
        }

        var requirement = AuthResolver.Resolve(
            context.RequestOptions.Auth,
            context.RequestOptions.OperationAuth,
            _clientDescriptor,
            _availableSchemes);
        if (requirement.Scheme == AuthScheme.NoAuth)
        {
            return await DriveAsync(request, context, continuation, async).ConfigureAwait(false);
        }

        EnsureHttps(request.Url);
        var stamped = await StampAsync(requirement, request, context, async).ConfigureAwait(false);
        var response = await DriveAsync(stamped, context, continuation, async).ConfigureAwait(false);
        return await HandleChallengeAsync(requirement, stamped, response, context, continuation, async).ConfigureAwait(false);
    }

    private async ValueTask<Request> StampAsync(AuthRequirement requirement, Request request, PipelineContext context, bool async)
    {
        var credential = async
            ? await GetCredentialAsync(requirement, request, context).ConfigureAwait(false)
            : GetCredential(requirement, request, context);
        return credential is { } header ? request.WithHeaders(request.Headers.Set(header.HeaderName, header.HeaderValue)) : request;
    }

    private async ValueTask<Response> HandleChallengeAsync(
        AuthRequirement requirement,
        Request sent,
        Response response,
        PipelineContext context,
        PipelineRunner continuation,
        bool async)
    {
        // AUTH-33: only a 401 that carries a WWW-Authenticate field is challenge-handled.
        if (response.Status.Code != 401)
        {
            return response;
        }

        var fields = response.Headers.GetAll(HttpHeaderName.WellKnown.WwwAuthenticate);
        if (fields.Count == 0)
        {
            return response;
        }

        var challenge = new AuthChallengeContext(requirement, sent, response, ReadChallenges(fields), context);
        var replacement = await RunHookAsync(challenge, response, context, async).ConfigureAwait(false);
        if (replacement is null)
        {
            return response;
        }

        if (!AuthOrigin.Same(context.SeedRequest.Url, replacement.Url))
        {
            await ReleaseAsync(response, context, async).ConfigureAwait(false);
            throw new InvalidOperationException(
                "An authentication challenge hook returned a replacement for a different origin; a credential must not be redirected.");
        }

        // AUTH-31, P6c-11: a replacement whose body cannot be written again is not sent; the original 401 stays with the caller.
        if (replacement.Body is { IsReplayable: false })
        {
            return response;
        }

        await ReleaseAsync(response, context, async).ConfigureAwait(false);
        return await DriveAsync(replacement, context, continuation, async).ConfigureAwait(false);
    }

    private async ValueTask<Request?> RunHookAsync(AuthChallengeContext challenge, Response response, PipelineContext context, bool async)
    {
        try
        {
            return async
                ? await OnChallengeAsync(challenge).ConfigureAwait(false)
                : OnChallenge(challenge);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // AUTH-32: the 401 is released before the failure propagates; a failing dispose is attached to it.
            await ReleaseAsync(response, context, async, primary: ex).ConfigureAwait(false);
            throw;
        }
    }

    // Not a ServiceRequestException, so no retry policy re-drives it: the request was refused, not failed.
    private void EnsureHttps(Uri url)
    {
        if (!string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpsRequiredException(GetType().Name, url.Scheme);
        }
    }
}
