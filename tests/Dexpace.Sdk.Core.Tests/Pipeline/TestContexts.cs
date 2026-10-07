// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Execution;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>
/// Builds pipeline contexts and runners for policy unit tests. The constructors are internal (P4c-4), so the tests reach
/// them through the <c>InternalsVisibleTo</c> Core grants this project (R2).
/// </summary>
internal static class TestContexts
{
    internal static PipelineContext For(
        Request? seed = null,
        DexpaceClientOptions? options = null,
        RequestOptions? requestOptions = null,
        DispatchContext? dispatch = null,
        ILogger? logger = null) =>
        PipelineContext.Create(
            seed ?? Request.Get("https://api.example.com/v1/resource"),
            options ?? new DexpaceClientOptions(),
            requestOptions ?? RequestOptions.Empty,
            dispatch ?? new DispatchContext(),
            logger ?? NullLogger.Instance,
            CancellationToken.None);

    /// <summary>A runner over <paramref name="downstream"/> policies (in the order given) and <paramref name="transport"/>.</summary>
    internal static PipelineRunner RunnerOver(IAsyncHttpClient transport, params HttpPipelinePolicy[] downstream) =>
        new([.. downstream.Select(p => new PipelineEntry(p, p.Stage))], 0, new PipelineTerminal(transport));

    /// <summary>Runs one policy over a recording transport and returns the request the transport received.</summary>
    internal static async Task<Request> SentAsync(HttpPipelinePolicy policy, Request request, PipelineContext context)
    {
        var transport = new RecordingTransport();
        using var response = await policy.ProcessAsync(request, context, RunnerOver(transport)).ConfigureAwait(false);
        return transport.LastRequest!;
    }

    /// <summary>The synchronous twin of <see cref="SentAsync"/>.</summary>
    internal static Request Sent(HttpPipelinePolicy policy, Request request, PipelineContext context)
    {
        var transport = new RecordingTransport();
        using var response = policy.Process(request, context, RunnerOver(transport));
        return transport.LastRequest!;
    }
}
