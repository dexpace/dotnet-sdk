// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Pipeline;

/// <summary>
/// The "continuation" continuation passed to each <see cref="HttpPipelinePolicy.ProcessAsync"/> and
/// <see cref="HttpPipelinePolicy.Process"/> call. Invokes the policy at the continuation index and, past the last, the transport.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PipelineRunner"/> is a <c>readonly struct</c> with no mutable field: it is the fork primitive. Each
/// <see cref="RunAsync"/> or <see cref="Run"/> call on the same value is an independent drive of the whole downstream
/// tail from the same position, so a retry or redirect policy re-drives by calling <c>continuation</c> again with the request it
/// holds (PIPE-13, PIPE-15, PIPE-16). There is no advanced handle to misuse and no single-use latch (P4c-12).
/// </para>
/// <para>
/// <b>Breaking:</b> was <c>RunAsync(PipelineContext) -&gt; ValueTask</c> over a context carrying the request and the
/// response. It is now <c>RunAsync(Request, PipelineContext) -&gt; ValueTask&lt;Response&gt;</c>, and <see cref="Run"/>
/// is the synchronous twin. A transport or policy returning <c>null</c> fails with
/// <see cref="PipelineAbortedException"/> (SEAM-16).
/// </para>
/// </remarks>
public readonly struct PipelineRunner
{
    private readonly PipelineEntry[]? _entries;
    private readonly int _index;
    private readonly PipelineTerminal? _terminal;

    internal PipelineRunner(PipelineEntry[] entries, int index, PipelineTerminal terminal)
    {
        _entries = entries;
        _index = index;
        _terminal = terminal;
    }

    /// <summary>
    /// Runs the remainder of the pipeline asynchronously with <paramref name="request"/>, then the transport, and
    /// returns the response.
    /// </summary>
    /// <param name="request">The request to pass downstream; every downstream policy and the transport receive it.</param>
    /// <param name="context">The context of the current drive.</param>
    /// <returns>The response produced downstream.</returns>
    /// <exception cref="PipelineAbortedException">A downstream policy or the transport returned no response.</exception>
    public async ValueTask<Response> RunAsync(Request request, PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var (entries, terminal) = Parts();
        if (_index >= entries.Length)
        {
            return await terminal.ExecuteAsync(request, context).ConfigureAwait(false);
        }

        var policy = entries[_index].Policy;
        var continuation = new PipelineRunner(entries, _index + 1, terminal);
        var response = await policy.ProcessAsync(request, context, continuation).ConfigureAwait(false);
        return response ?? throw NullFrom(policy);
    }

    /// <summary>
    /// Runs the remainder of the pipeline synchronously with <paramref name="request"/>, then the transport, and returns
    /// the response.
    /// </summary>
    /// <param name="request">The request to pass downstream; every downstream policy and the transport receive it.</param>
    /// <param name="context">The context of the current drive.</param>
    /// <returns>The response produced downstream.</returns>
    /// <exception cref="PipelineAbortedException">A downstream policy or the transport returned no response.</exception>
    public Response Run(Request request, PipelineContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        var (entries, terminal) = Parts();
        if (_index >= entries.Length)
        {
            return terminal.Execute(request, context);
        }

        var policy = entries[_index].Policy;
        var continuation = new PipelineRunner(entries, _index + 1, terminal);
        return policy.Process(request, context, continuation) ?? throw NullFrom(policy);
    }

    private (PipelineEntry[] Entries, PipelineTerminal Terminal) Parts() =>
        (_entries, _terminal) is ({ } entries, { } terminal)
            ? (entries, terminal)
            : throw new InvalidOperationException("A default PipelineRunner is not attached to a pipeline.");

    private static PipelineAbortedException NullFrom(HttpPipelinePolicy policy) =>
        new($"The policy {policy.GetType().FullName} returned no response (SEAM-16).");
}
