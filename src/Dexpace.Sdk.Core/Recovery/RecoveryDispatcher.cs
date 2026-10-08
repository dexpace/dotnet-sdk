// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.Resilience;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// Runs one call: the request chain, the transport, then the response chain, and unwraps the terminal
/// <see cref="Outcome"/> (RECOV-2, RECOV-10, RECOV-11).
/// </summary>
/// <remarks>
/// <para>
/// Every non-fatal exception from the request chain or the transport becomes an <see cref="Outcome.Failure"/> that the
/// recovery steps observe (RECOV-2, P4b-8), including <see cref="OperationCanceledException"/>: the token stays
/// cancelled (RECOV-11) and the same exception instance is rethrown at the end. A transport that returns
/// <see langword="null"/> is a failure with <see cref="InvalidOperationException"/>. A terminal failure is rethrown with
/// <see cref="ExceptionDispatchInfo"/>: the same instance, no wrapper (RECOV-10, P4b-10).
/// </para>
/// <para>
/// The transport is a per-call argument: the dispatcher owns none, disposes none, and holds no state (P4b-25), so one
/// instance serves every transport and both forms.
/// </para>
/// </remarks>
public sealed class RecoveryDispatcher
{
    /// <summary>Creates a dispatcher over the two chains, with no retry.</summary>
    /// <param name="requestChain">The chain applied before the send.</param>
    /// <param name="responseChain">The chain applied to the outcome.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public RecoveryDispatcher(RequestRecoveryChain requestChain, ResponseRecoveryChain responseChain)
    {
        ArgumentNullException.ThrowIfNull(requestChain);
        ArgumentNullException.ThrowIfNull(responseChain);
        RequestChain = requestChain;
        ResponseChain = responseChain;
    }

    /// <summary>
    /// Creates a dispatcher over the two chains that retries every call with <paramref name="retry"/> (RETRY-13, RECOV-19).
    /// </summary>
    /// <param name="requestChain">The chain applied once, before the first send.</param>
    /// <param name="responseChain">The chain whose response steps run after every send and whose recovery steps run once.</param>
    /// <param name="retry">The retry configuration: options, total-time budget and clock.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public RecoveryDispatcher(RequestRecoveryChain requestChain, ResponseRecoveryChain responseChain, RetryRecovery retry)
        : this(requestChain, responseChain)
    {
        ArgumentNullException.ThrowIfNull(retry);
        Retry = retry;
    }

    /// <summary>Gets the retry configuration, or <see langword="null"/> when this dispatcher sends each call once.</summary>
    public RetryRecovery? Retry { get; }

    /// <summary>The chain applied before the send.</summary>
    public RequestRecoveryChain RequestChain { get; }

    /// <summary>The chain applied to the outcome.</summary>
    public ResponseRecoveryChain ResponseChain { get; }

    /// <summary>Dispatches <paramref name="request"/> over a synchronous transport.</summary>
    /// <param name="transport">The transport; not disposed.</param>
    /// <param name="request">The request.</param>
    /// <param name="options">The per-call options handed to the transport.</param>
    /// <param name="cancellationToken">The call's token.</param>
    /// <returns>The terminal success's response.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public Response Dispatch(
        IHttpClient transport,
        Request request,
        RequestOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        return SyncPath.GetResult(
            DispatchCoreAsync(new TransportCall(transport, null), request, options, async: false, cancellationToken));
    }

    /// <summary>Dispatches <paramref name="request"/> over an asynchronous transport.</summary>
    /// <param name="transport">The transport; not disposed.</param>
    /// <param name="request">The request.</param>
    /// <param name="options">The per-call options handed to the transport.</param>
    /// <param name="cancellationToken">The call's token.</param>
    /// <returns>The terminal success's response.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public ValueTask<Response> DispatchAsync(
        IAsyncHttpClient transport,
        Request request,
        RequestOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        return DispatchCoreAsync(new TransportCall(null, transport), request, options, async: true, cancellationToken);
    }

    private ValueTask<Response> DispatchCoreAsync(
        TransportCall transport,
        Request request,
        RequestOptions options,
        bool async,
        CancellationToken cancellationToken) =>
        Retry is null
            ? DispatchOnceAsync(transport, request, options, async, cancellationToken)
            : DispatchWithRetryAsync(Retry, transport, request, options, async, cancellationToken);

    // 6a (P6a-5): the request chain runs once, each send is the transport plus the response steps, the engine retries, and the
    // recovery steps run once on the terminal outcome.
    private async ValueTask<Response> DispatchWithRetryAsync(
        RetryRecovery retry,
        TransportCall transport,
        Request request,
        RequestOptions options,
        bool async,
        CancellationToken cancellationToken)
    {
        Outcome terminal;
        try
        {
            var prepared = async
                ? await RequestChain.ApplyAsync(request, cancellationToken).ConfigureAwait(false)
                : RequestChain.Apply(request, cancellationToken);
            var run = new RetryRun(
                prepared,
                retry.Options,
                (req, _, isAsync, token) => SendOnceAsync(retry, transport, req, options, isAsync, token))
            {
                MaxRetries = options.MaxRetries ?? retry.Options.MaxRetryAttempts,
                Budget = RetryBudget.For(retry.TotalTimeout, retry.Clock),
                HonorPacing = true,
            };
            terminal = await retry.Engine.RunAsync(run, async, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            terminal = new Outcome.Failure(ex);
        }

        terminal = await ResponseChain.ApplyRecoveryPhaseAsync(terminal, async, cancellationToken).ConfigureAwait(false);
        return Unwrap(terminal);
    }

    // One send: the transport, the response steps over its outcome, then (RECOV-19, RETRY-36) a surviving response whose
    // status is in the configured set is buffered once (at most 1 MiB) and mapped to the HttpResponseException it is.
    private async ValueTask<Outcome> SendOnceAsync(
        RetryRecovery retry,
        TransportCall transport,
        Request prepared,
        RequestOptions options,
        bool async,
        CancellationToken cancellationToken)
    {
        Outcome outcome;
        try
        {
            var response = async
                ? await transport.Async!.ExecuteAsync(prepared, options, cancellationToken).ConfigureAwait(false)
                : transport.Sync!.Execute(prepared, options, cancellationToken);
            outcome = response is null
                ? new Outcome.Failure(new InvalidOperationException($"The transport {(async ? transport.Async!.GetType() : transport.Sync!.GetType()).FullName} returned a null response."))
                : new Outcome.Success(response);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            outcome = new Outcome.Failure(ex);
        }

        outcome = await ResponseChain.ApplyResponsePhaseAsync(outcome, async, cancellationToken).ConfigureAwait(false);
        if (outcome is not Outcome.Success success || !retry.Options.RetryableStatusCodes.Contains(success.Response.Status.Code))
        {
            return outcome;
        }

        try
        {
            // The buffered copy is owned by the exception that carries it (a replayable in-memory body).
#pragma warning disable CA2000
            var buffered = async
                ? await ErrorBodyBuffer.CaptureAsync(success.Response, cancellationToken).ConfigureAwait(false)
                : ErrorBodyBuffer.Capture(success.Response, cancellationToken);
#pragma warning restore CA2000
            return new Outcome.Failure(ErrorMapping.ToException(buffered));
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            return new Outcome.Failure(ex);
        }
    }

    private static Response Unwrap(Outcome terminal)
    {
        switch (terminal)
        {
            case Outcome.Success success:
                return success.Response;
            case Outcome.Failure failure:
                ExceptionDispatchInfo.Capture(failure.Error).Throw();
                throw new UnreachableException();
            default:
                throw new UnreachableException();
        }
    }

    private async ValueTask<Response> DispatchOnceAsync(
        TransportCall transport,
        Request request,
        RequestOptions options,
        bool async,
        CancellationToken cancellationToken)
    {
        Outcome outcome;
        try
        {
            var prepared = async
                ? await RequestChain.ApplyAsync(request, cancellationToken).ConfigureAwait(false)
                : RequestChain.Apply(request, cancellationToken);
            var response = async
                ? await transport.Async!.ExecuteAsync(prepared, options, cancellationToken).ConfigureAwait(false)
                : transport.Sync!.Execute(prepared, options, cancellationToken);
            outcome = response is null
                ? new Outcome.Failure(new InvalidOperationException($"The transport {(async ? transport.Async!.GetType() : transport.Sync!.GetType()).FullName} returned a null response."))
                : new Outcome.Success(response);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            outcome = new Outcome.Failure(ex);
        }

        var terminal = async
            ? await ResponseChain.ApplyAsync(outcome, cancellationToken).ConfigureAwait(false)
            : ResponseChain.Apply(outcome, cancellationToken);
        return Unwrap(terminal);
    }

    private readonly record struct TransportCall(IHttpClient? Sync, IAsyncHttpClient? Async);
}
