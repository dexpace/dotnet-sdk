// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// The call-scoped, read-mostly context every policy receives beside the request.
/// </summary>
/// <remarks>
/// <para>
/// A context is made of one shared, call-scoped state object and a few per-drive values. The call-scoped values
/// (<see cref="SeedRequest"/>, <see cref="RequestOptions"/>, <see cref="Options"/>, <see cref="CallKey"/>,
/// <see cref="Instrumentation"/> and the property bag) are shared by reference with every copy. The per-drive values
/// (<see cref="CancellationToken"/>, <see cref="Activity"/>, <see cref="AttemptNumber"/>, <see cref="HopNumber"/>) travel
/// by copy: <see cref="ForAttempt"/>, <see cref="ForHop"/>, <see cref="WithActivity"/> and
/// <see cref="WithCancellationToken"/> return a new instance, so a downstream policy's write can never reach an
/// upstream one (PIPE-16, S6). The context carries no request and no response: a request flows in through the policy's
/// parameter and a response flows out through its return value.
/// </para>
/// <para>
/// <b>Breaking:</b> was a mutable carrier with a public constructor, <c>Request</c>, <c>Response</c>, a string-keyed
/// property bag and four internal setters. It now has no public constructor, no <c>Request</c> or <c>Response</c>, a
/// bag keyed by <see cref="PipelinePropertyKey{T}"/> and per-drive copies. The retired string key
/// <c>"dexpace.auth.origin"</c> was overwritable by any policy (design §6.2); the origin is now the
/// <see cref="SeedRequest"/>'s.
/// </para>
/// </remarks>
public sealed class PipelineContext
{
    private readonly CallState _state;

    private PipelineContext(CallState state, Activity? activity, int attemptNumber, int hopNumber, CancellationToken cancellationToken)
    {
        _state = state;
        CancellationToken = cancellationToken;
        Activity = activity;
        AttemptNumber = attemptNumber;
        HopNumber = hopNumber;
    }

    /// <summary>
    /// The request handed to <c>Send</c>/<c>SendAsync</c>, fixed at call entry. It is the origin the redirect and
    /// authorization policies compare against (REDIR-11, REDIR-24); a policy that rewrites the URL above them does not
    /// move it.
    /// </summary>
    public Request SeedRequest => _state.SeedRequest;

    /// <summary>
    /// The caller's per-call options, carried by reference across every fork and passed to the transport (PIPE-17).
    /// </summary>
    public RequestOptions RequestOptions => _state.RequestOptions;

    /// <summary>
    /// The client options that apply to this call: the pipeline's build-time options, or the per-call override.
    /// </summary>
    public DexpaceClientOptions Options => _state.Options;

    /// <summary>
    /// The key minted for this call (CTX-4).
    /// </summary>
    public CallKey CallKey => _state.Dispatch.Key;

    /// <summary>
    /// The instrumentation bundle of this call (CTX-14, CTX-15).
    /// </summary>
    public InstrumentationContext Instrumentation => _state.Dispatch.Instrumentation;

    /// <summary>
    /// A token that can cancel the in-flight operation: the caller's, or the deadline-linked token the operation policy
    /// passes downstream.
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// The SDK tracing span the instrumentation policy opened for its downstream, or <see langword="null"/>.
    /// </summary>
    public Activity? Activity { get; }

    /// <summary>
    /// The zero-based retry attempt counter: <c>0</c> on the initial send.
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// The zero-based redirect hop counter: <c>0</c> on the initial request.
    /// </summary>
    public int HopNumber { get; }

    internal static PipelineContext Create(
        Request seedRequest,
        DexpaceClientOptions options,
        RequestOptions requestOptions,
        DispatchContext dispatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seedRequest);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(requestOptions);
        ArgumentNullException.ThrowIfNull(dispatch);
        return new PipelineContext(new CallState(seedRequest, options, requestOptions, dispatch), null, 0, 0, cancellationToken);
    }

    internal CallState State => _state;

    /// <summary>
    /// Retrieves a value from the call-scoped property bag.
    /// </summary>
    /// <typeparam name="T">The type of the stored value.</typeparam>
    /// <param name="key">The key instance the value was stored under.</param>
    /// <param name="value">The stored value, or <see langword="default"/> when absent.</param>
    /// <returns><see langword="true"/> when a value is stored under <paramref name="key"/>.</returns>
    public bool TryGetProperty<T>(PipelinePropertyKey<T> key, out T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _state.TryGet(key, out value);
    }

    /// <summary>
    /// Stores a value in the call-scoped property bag, shared by every copy of this context. Overwrites any value under
    /// the same key instance.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key instance.</param>
    /// <param name="value">The value to store.</param>
    public void SetProperty<T>(PipelinePropertyKey<T> key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _state.Set(key, value);
    }

    /// <summary>
    /// Returns a copy for a retry attempt, with <see cref="AttemptNumber"/> set to <paramref name="attempt"/>.
    /// </summary>
    /// <param name="attempt">The zero-based attempt number.</param>
    /// <returns>A new context sharing every call-scoped reference.</returns>
    public PipelineContext ForAttempt(int attempt) =>
        new(_state, Activity, attempt, HopNumber, CancellationToken);

    /// <summary>
    /// Returns a copy for a redirect hop, with <see cref="HopNumber"/> set to <paramref name="hop"/>.
    /// </summary>
    /// <param name="hop">The zero-based hop number.</param>
    /// <returns>A new context sharing every call-scoped reference.</returns>
    public PipelineContext ForHop(int hop) =>
        new(_state, Activity, AttemptNumber, hop, CancellationToken);

    /// <summary>
    /// Returns a copy whose <see cref="Activity"/> is <paramref name="activity"/>.
    /// </summary>
    /// <param name="activity">The span to expose downstream, or <see langword="null"/>.</param>
    /// <returns>A new context sharing every call-scoped reference.</returns>
    public PipelineContext WithActivity(Activity? activity) =>
        new(_state, activity, AttemptNumber, HopNumber, CancellationToken);

    /// <summary>
    /// Returns a copy whose <see cref="CancellationToken"/> is <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="cancellationToken">The token to expose downstream.</param>
    /// <returns>A new context sharing every call-scoped reference.</returns>
    public PipelineContext WithCancellationToken(CancellationToken cancellationToken) =>
        new(_state, Activity, AttemptNumber, HopNumber, cancellationToken);
}
