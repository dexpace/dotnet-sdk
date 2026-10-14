// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// What one run of one invariant assertion owns: the subject, the face, a recording clock and logger, and the transports it
/// builds. Created fresh for each (assertion, face) and disposed after it on every path, so a poisoned pipeline cannot fail a
/// later row (suite-contract clause 1). The faces are the two call surfaces of <see cref="HttpPipeline"/>; <see cref="TransportFace.None"/>
/// runs an assertion that drives neither.
/// </summary>
internal sealed class InvariantContext(InvariantSubject subject, TransportFace face, SuiteOptions options) : IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _owned = [];

    /// <summary>The composition under test.</summary>
    internal InvariantSubject Subject { get; } = subject;

    /// <summary>The face this run drives.</summary>
    internal TransportFace Face { get; } = face;

    /// <summary>The run's options.</summary>
    internal SuiteOptions Options { get; } = options;

    /// <summary>The clock every pipeline of this run waits on; it counts the timers it creates and disposes.</summary>
    internal RecordingTimeProvider Time { get; } = new();

    /// <summary>The logger every pipeline of this run logs to.</summary>
    internal RecordingLogger Logger { get; } = new();

    /// <summary>
    /// How long an assertion waits for something that should already have happened or been cut: ten seconds, or half the
    /// assertion's own bound when that is shorter, so a subject that hangs fails fast and named instead of running to the full bound.
    /// </summary>
    internal TimeSpan Bound => TimeSpan.FromSeconds(Math.Min(10, Options.AssertionTimeout.TotalSeconds / 2));

    /// <summary>Returns <paramref name="hook"/>, or ends the assertion as not exercised, naming the hook (P10-4).</summary>
    /// <typeparam name="T">The hook's type.</typeparam>
    /// <param name="hook">The hook as the subject supplied it.</param>
    /// <param name="name">The hook's member name on <see cref="InvariantSubject"/>.</param>
    /// <exception cref="NotExercisedException"><paramref name="hook"/> is <see langword="null"/>.</exception>
    internal static T RequireHook<T>(T? hook, string name)
        where T : class =>
        hook ?? throw new NotExercisedException($"the subject supplies no {name} hook, so nothing was exercised");

    /// <summary>Takes ownership of <paramref name="item"/>; it is disposed with the run.</summary>
    /// <typeparam name="T">The item's type.</typeparam>
    /// <param name="item">What to dispose after the run.</param>
    internal T Own<T>(T item)
        where T : IAsyncDisposable
    {
        _owned.Add(item);
        return item;
    }

#pragma warning disable CA2000 // Ownership passes to the context's list, which DisposeAsync drains on every path.
    /// <summary>Builds an in-memory transport with <paramref name="behavior"/>; disposed with the run.</summary>
    /// <param name="behavior">What the transport does with each send.</param>
    internal ProbeTransport Transport(ProbeTransport.Behavior behavior) => Own(new ProbeTransport(behavior));
#pragma warning restore CA2000

    /// <summary>
    /// Returns <paramref name="pipeline"/> with error mapping on: the pipeline itself when it already holds an
    /// <see cref="ErrorMappingPolicy"/>, otherwise a flattened copy with one added (PIPE-35), so an error status reaches the caller
    /// as the exception the SDK maps it to rather than as a response.
    /// </summary>
    /// <param name="pipeline">The subject's pipeline.</param>
    internal static HttpPipeline WithErrorMapping(HttpPipeline pipeline) =>
        pipeline.Policies.OfType<ErrorMappingPolicy>().Any() ? pipeline : PipelineBuilder.Flatten(pipeline).Add(new ErrorMappingPolicy()).Build();

    /// <summary>Builds the subject's pipeline over <paramref name="transport"/> on this run's clock and logger.</summary>
    /// <param name="transport">The terminal transport; the pipeline never owns it.</param>
    /// <param name="clientOptions">The call-level options, or the defaults.</param>
    internal HttpPipeline Build(IAsyncHttpClient transport, DexpaceClientOptions? clientOptions = null) =>
        Subject.CreatePipeline(transport, clientOptions ?? new DexpaceClientOptions(), Time, Logger);

    /// <summary>Sends one request through <paramref name="pipeline"/> on this run's face; the blocking face runs on a dedicated thread.</summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="request">The request.</param>
    /// <param name="token">The caller's token.</param>
    /// <param name="requestOptions">Per-call options, or none.</param>
    internal Task<Response> SendAsync(HttpPipeline pipeline, Request request, CancellationToken token, RequestOptions? requestOptions = null)
    {
        var perCall = requestOptions ?? RequestOptions.Empty;
        return Face == TransportFace.Blocking
            ? Task.Factory.StartNew(
                () => pipeline.Send(request, perCall, token),
                CancellationToken.None,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default)
            : pipeline.SendAsync(request, perCall, token).AsTask();
    }

    /// <summary>
    /// Sends and returns how the call ended: <see langword="null"/> when it succeeded (the response is disposed), otherwise the
    /// exception it failed with. Bounded by <see cref="Bound"/>, so a hung pipeline is a named failure.
    /// </summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="request">The request.</param>
    /// <param name="what">What the call is, named if it hangs.</param>
    /// <param name="token">The assertion's token, which bounds the wait for the call to settle.</param>
    /// <param name="callToken">The token handed to the pipeline as the caller's, or <see langword="null"/> for <paramref name="token"/>. A test that cancels the call cancels this one.</param>
    /// <param name="requestOptions">Per-call options, or none.</param>
    internal async Task<Exception?> FailureOfAsync(
        HttpPipeline pipeline,
        Request request,
        string what,
        CancellationToken token,
        CancellationToken? callToken = null,
        RequestOptions? requestOptions = null) =>
        await Outcome.OfAsync(SendAsync(pipeline, request, callToken ?? token, requestOptions), Bound, what, token).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            await _owned[i].DisposeAsync().ConfigureAwait(false);
        }
    }
}
