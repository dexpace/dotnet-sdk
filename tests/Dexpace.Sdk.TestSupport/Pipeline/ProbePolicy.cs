// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;

namespace Dexpace.Sdk.TestSupport.Pipeline;

/// <summary>
/// Records <c>name:in</c> before and <c>name:out</c> after the downstream, sync and async, into a shared log, and which
/// entry point ran into <see cref="EntryPoints"/>.
/// </summary>
public sealed class ProbePolicy(string name, PipelineStage stage, List<string> log) : HttpPipelinePolicy
{
    private readonly List<string> _entryPoints = [];

    /// <summary>The entry points that ran, in order: <c>async</c> or <c>sync</c>.</summary>
    public IReadOnlyList<string> EntryPoints => _entryPoints;

    /// <summary>The number of times the probe was entered.</summary>
    public int Entered => _entryPoints.Count;

    /// <summary>The last request the probe received.</summary>
    public Request? LastRequest { get; private set; }

    /// <summary>The last context the probe received.</summary>
    public PipelineContext? LastContext { get; private set; }

    /// <summary>The last response the probe saw on the way out.</summary>
    public Response? LastResponse { get; private set; }

    /// <inheritdoc/>
    public override PipelineStage Stage => stage;

    /// <inheritdoc/>
    public override async ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation)
    {
        Enter("async", request, context);
        var response = await continuation.RunAsync(request, context).ConfigureAwait(false);
        Exit(response);
        return response;
    }

    /// <inheritdoc/>
    public override Response Process(Request request, PipelineContext context, PipelineRunner continuation)
    {
        Enter("sync", request, context);
        var response = continuation.Run(request, context);
        Exit(response);
        return response;
    }

    private void Enter(string entryPoint, Request request, PipelineContext context)
    {
        lock (log)
        {
            log.Add($"{name}:in");
        }

        lock (_entryPoints)
        {
            _entryPoints.Add(entryPoint);
        }

        LastRequest = request;
        LastContext = context;
    }

    private void Exit(Response response)
    {
        LastResponse = response;
        lock (log)
        {
            log.Add($"{name}:out");
        }
    }
}
