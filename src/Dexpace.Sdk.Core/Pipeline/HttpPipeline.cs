// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.ObjectModel;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// The entry point for sending an HTTP request through the configured policy chain, and itself a transport.
/// </summary>
/// <remarks>
/// <para>
/// Instances are created exclusively by <see cref="PipelineBuilder"/>. The pipeline is immutable after construction: the
/// ordered policy array, the terminal transport and the client options are captured at build time, and every send
/// creates its own <see cref="PipelineContext"/>, so concurrent calls share no per-call state (PIPE-10).
/// </para>
/// <para>
/// <b>Two kinds of options.</b> The seam carries <see cref="RequestOptions"/> (per call); the policies read
/// <see cref="DexpaceClientOptions"/> (retry, redirect, user agent, deadline). A pipeline captures the client options at
/// build, so a call through <see cref="IAsyncHttpClient"/> or <see cref="IHttpClient"/> runs with them. The overloads taking
/// a <see cref="DexpaceClientOptions"/> run one call with that immutable value; derive it with <c>with</c>.
/// <see cref="RequestOptions"/> remains the seam's per-call carrier (timeout, retry cap, tags) (phase 5a, P5a-6).
/// </para>
/// <para>
/// <b>As a transport (PIPE-26).</b> The pipeline implements <see cref="IAsyncHttpClient"/> and <see cref="IHttpClient"/>
/// explicitly, threading the seam's <see cref="RequestOptions"/> and token through. <b>Disposal (PIPE-27)</b> is a no-op
/// toward the transport: the pipeline never owns it. There is no disposed latch: a disposed pipeline stays usable
/// (SEAM-15 is a MAY), so disposal never suggests a release that did not happen.
/// </para>
/// <para>
/// <b>Operation span (OBS-29).</b> When <c>Dexpace.Sdk</c>'s activity source has a listener, every call opens one
/// <see cref="System.Diagnostics.ActivityKind.Internal"/> operation span at entry, before the call context exists, and ends
/// it exactly once when the response is returned (at headers, not when its body is consumed) or the call throws. A listener
/// that throws while the span ends propagates, after the response, if any, has been disposed (P5c-13). The bundle in
/// <see cref="PipelineContext.Instrumentation"/> is that span's, or <see cref="Execution.InstrumentationContext.None"/> when
/// untraced.
/// </para>
/// <para>
/// <b>Sync path.</b> <see cref="Send(Request, CancellationToken)"/> drives <see cref="HttpPipelinePolicy.Process"/> and the
/// transport's synchronous entry point; it never blocks on the async chain (PIPE-28).
/// </para>
/// <para>
/// <b>Breaking (additive):</b> the pipeline is now <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>, so
/// <c>using</c> analyzers ask callers to dispose it; disposal never touches the transport. <b>Breaking (additive):</b>
/// the overloads taking <see cref="DexpaceClientOptions"/> lose their <c>= default</c> token: no overload of this family
/// carries an optional token.
/// </para>
/// </remarks>
public sealed class HttpPipeline : IAsyncHttpClient, IHttpClient
{
    private readonly PipelineEntry[] _entries;
    private readonly PipelineTerminal _terminal;
    private readonly ILogger _logger;

    internal HttpPipeline(PipelineEntry[] entries, PipelineTerminal terminal, DexpaceClientOptions options)
    {
        _entries = entries;
        _terminal = terminal;
        ClientOptions = options;

        // P5b-6: the pipeline reports to the logger of its Diagnostics pillar (design 5b position F, plan reading R4).
        _logger = entries.Select(e => e.Policy).OfType<InstrumentationPolicy>().FirstOrDefault()?.Logger ?? NullLogger.Instance;
        Policies = new ReadOnlyCollection<HttpPipelinePolicy>([.. entries.Select(e => e.Policy)]);
    }

    /// <summary>
    /// The policies in execution order, outermost first, as a read-only view over a private copy (PIPE-25).
    /// </summary>
    public IReadOnlyList<HttpPipelinePolicy> Policies { get; }

    internal DexpaceClientOptions ClientOptions { get; }

    internal IAsyncHttpClient Transport => _terminal.Transport;

    internal IReadOnlyList<PipelineEntry> Entries => _entries;

    /// <summary>
    /// Asynchronously sends <paramref name="request"/> with the client options captured at build.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The response produced by the pipeline.</returns>
    public ValueTask<Response> SendAsync(Request request, CancellationToken cancellationToken) =>
        SendCoreAsync(request, ClientOptions, RequestOptions.Empty, async: true, cancellationToken);

    /// <summary>
    /// Asynchronously sends <paramref name="request"/> with the client options captured at build and the caller's
    /// per-call <paramref name="options"/>, which reach every policy and the transport by reference (PIPE-17).
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="options">The per-call options.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The response produced by the pipeline.</returns>
    public ValueTask<Response> SendAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        SendCoreAsync(request, ClientOptions, options, async: true, cancellationToken);

    /// <summary>
    /// Asynchronously sends <paramref name="request"/>, overriding the captured client options for this one call.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="options">Client options that apply to this call instead of the captured ones.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The response produced by the pipeline.</returns>
    /// <remarks>
    /// <b>Breaking:</b> the token no longer defaults; pass <see cref="CancellationToken.None"/> or use
    /// <see cref="SendAsync(Request, CancellationToken)"/>.
    /// </remarks>
    public ValueTask<Response> SendAsync(Request request, DexpaceClientOptions options, CancellationToken cancellationToken) =>
        SendCoreAsync(request, options, RequestOptions.Empty, async: true, cancellationToken);

    /// <summary>
    /// Sends <paramref name="request"/>, applies <paramref name="handler"/> to the response, and disposes the response in
    /// every outcome (PIPE-31).
    /// </summary>
    /// <typeparam name="T">The handler's result type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="handler">Maps the response to a result; the response is disposed after it runs.</param>
    /// <param name="options">The per-call options.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The handler's result.</returns>
    /// <remarks>
    /// A handler failure surfaces as itself (never wrapped), and a failure disposing the response is attached to it
    /// through <see cref="ExceptionTrail"/>. A transport failure surfaces unwrapped.
    /// </remarks>
    public async ValueTask<T> SendAsync<T>(
        Request request,
        Func<Response, CancellationToken, ValueTask<T>> handler,
        RequestOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var response = await SendCoreAsync(request, ClientOptions, options, async: true, cancellationToken).ConfigureAwait(false);
        T result;
        try
        {
            result = await handler(response, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            await Disposal.DisposeQuietlyAsync(response, ex).ConfigureAwait(false);
            throw;
        }

        await response.DisposeAsync().ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Synchronously sends <paramref name="request"/> with the client options captured at build.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The response produced by the pipeline.</returns>
    public Response Send(Request request, CancellationToken cancellationToken) =>
        SendSync(request, ClientOptions, RequestOptions.Empty, cancellationToken);

    /// <summary>
    /// Synchronously sends <paramref name="request"/> with the caller's per-call <paramref name="options"/> (PIPE-17).
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="options">The per-call options.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The response produced by the pipeline.</returns>
    public Response Send(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        SendSync(request, ClientOptions, options, cancellationToken);

    /// <summary>
    /// Synchronously sends <paramref name="request"/>, overriding the captured client options for this one call.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="options">Client options that apply to this call instead of the captured ones.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The response produced by the pipeline.</returns>
    /// <remarks>
    /// <b>Breaking:</b> this drives the policies synchronously (it used to block on the async chain), and the token no
    /// longer defaults.
    /// </remarks>
    public Response Send(Request request, DexpaceClientOptions options, CancellationToken cancellationToken) =>
        SendSync(request, options, RequestOptions.Empty, cancellationToken);

    /// <summary>
    /// Synchronously sends <paramref name="request"/>, applies <paramref name="handler"/> and disposes the response in
    /// every outcome (PIPE-31).
    /// </summary>
    /// <typeparam name="T">The handler's result type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="handler">Maps the response to a result; the response is disposed after it runs.</param>
    /// <param name="options">The per-call options.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The handler's result.</returns>
    public T Send<T>(Request request, Func<Response, T> handler, RequestOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var response = SendSync(request, ClientOptions, options, cancellationToken);
        T result;
        try
        {
            result = handler(response);
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            Disposal.DisposeQuietly(response, ex);
            throw;
        }

        response.Dispose();
        return result;
    }

    /// <inheritdoc />
    Task<Response> IAsyncHttpClient.ExecuteAsync(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        SendAsync(request, options, cancellationToken).AsTask();

    /// <inheritdoc />
    Response IHttpClient.Execute(Request request, RequestOptions options, CancellationToken cancellationToken) =>
        Send(request, options, cancellationToken);

    /// <summary>
    /// A no-op toward the transport: the pipeline never owns it (PIPE-27). The pipeline stays usable afterwards.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// A no-op toward the transport: the pipeline never owns it (PIPE-27). The pipeline stays usable afterwards.
    /// </summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Response SendSync(Request request, DexpaceClientOptions options, RequestOptions requestOptions, CancellationToken cancellationToken) =>
        SyncPath.GetCompletedResult(
            SendCoreAsync(request, options, requestOptions, async: false, cancellationToken),
            nameof(HttpPipeline));

    // The shared body of every send. With async: false it never awaits, so the returned task is complete (fact 1) and
    // a failure is carried in it (fact 2) for SyncPath to rethrow unwrapped.
    private async ValueTask<Response> SendCoreAsync(
        Request request,
        DexpaceClientOptions options,
        RequestOptions requestOptions,
        bool async,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(requestOptions);

        // OBS-29, P5c-2: the operation span opens at call entry, before the dispatch context is built, so the bundle (and the
        // CallKey minted from it, CTX-17) carries the operation span's ids. An untraced call's bundle is None (P5c-3).
        var operation = OperationTelemetry.Start(request, options);
        PipelineContext? context = null;
        Response? response = null;
        var settled = false;
        try
        {
            // CTX-17: the dispatch registers nothing; the terminal's promotion does, once per transmission.
            var dispatch = new DispatchContext(InstrumentationContext.FromActivity(operation));
            context = PipelineContext.Create(request, options, requestOptions, dispatch, _logger, cancellationToken);
            var runner = new PipelineRunner(_entries, 0, _terminal);
            response = async
                ? await runner.RunAsync(request, context).ConfigureAwait(false)
                : runner.Run(request, context);
            OperationTelemetry.Complete(operation, response);
            settled = true;

            // The span ends when the response is returned, at headers (P5c-4). A listener that throws here is the
            // listener's contract violation and propagates (OBS-30), but not before the response is released (P5c-13).
            OperationTelemetry.Stop(operation, settled);
            return response;
        }
        catch (Exception ex) when (response is not null && !ExceptionFacts.IsFatal(ex))
        {
            await DisposeSupersededAsync(response, ex, async).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (!ExceptionFacts.IsFatal(ex))
        {
            // Close the furthest link before the exception surfaces; a fatal exception skips it, the store's bound is
            // the backstop (design §5.4, CTX-11).
            context?.State.CloseFurthest();
            OperationTelemetry.Fail(operation, ex, context?.State);
            settled = true;
            throw;
        }
        finally
        {
            OperationTelemetry.Stop(operation, settled);
        }
    }

    private static ValueTask DisposeSupersededAsync(Response response, Exception primary, bool async)
    {
        if (async)
        {
            return Disposal.DisposeQuietlyAsync(response, primary);
        }

        Disposal.DisposeQuietly(response, primary);
        return ValueTask.CompletedTask;
    }
}
