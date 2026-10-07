// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Core.Tests.Pipeline;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>OBS-1, OBS-34, P5b-19: the disabled path allocates and emits nothing.</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class DisabledPathAllocationTests
{
    private const int WarmUp = 3;
    private const int Iterations = 1000;

    private static readonly Request s_request =
        Request.Get("https://api.example.com/v1/items?token=SECRET&other=" + new string('x', 200) + "&api-version=3");

    private static readonly InvalidOperationException s_failure = new("preallocated");

    private static long Allocated(Action action)
    {
        for (var i = 0; i < WarmUp; i++)
        {
            action();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    // The context and the entry are built by the caller: only the emitter's own allocations are measured.
    private static void EmitterRound(
        ILogger logger,
        PipelineContext context,
        HttpLoggingOptions options,
        RedactionCache.Entry entry,
        Response response)
    {
        var scope = new AttemptScope(s_request, context, entry.Redactor);
        var log = HttpLogEmitter.OnRequest(logger, ref scope, s_request, options, entry, CancellationToken.None);
        HttpLogEmitter.OnFailure(logger, ref scope, log, s_failure, CancellationToken.None);
        _ = HttpLogEmitter.Complete(logger, ref scope, log, response, CancellationToken.None);
    }

    [Fact]
    public void Every_emitter_method_allocates_nothing_at_None()
    {
        using var response = TestResponses.Create(Status.Ok);
        var options = new HttpLoggingOptions { Level = HttpLogLevel.None };
        var entry = new RedactionCache().Get(options);
        var context = TestContexts.For(s_request);

        foreach (var logger in (ILogger[])[new DisabledLogger(), NullLogger.Instance, new RecordingLogger()])
        {
            Assert.Equal(0, Allocated(() => EmitterRound(logger, context, options, entry, response)));
        }
    }

    [Fact]
    public void Every_emitter_method_allocates_nothing_when_the_logger_disables_the_events()
    {
        using var response = TestResponses.Create(Status.Ok);
        var context = TestContexts.For(s_request);
        var logger = new DisabledLogger();

        foreach (var level in (HttpLogLevel[])[HttpLogLevel.Headers, HttpLogLevel.Body])
        {
            var options = new HttpLoggingOptions { Level = level };
            var entry = new RedactionCache().Get(options);
            Assert.Equal(0, Allocated(() => EmitterRound(logger, context, options, entry, response)));
        }
    }

    [Fact]
    public void A_pipeline_over_a_synchronous_transport_adds_zero_bytes_per_call_over_a_pass_through_policy()
    {
        using var cached = TestResponses.Create(Status.Ok);
        var transport = new RecordingTransport(_ => cached);
        var instrumented = new PipelineBuilder().Add(new InstrumentationPolicy()).Build(transport);
        var passThrough = new PipelineBuilder().Add(new PassThroughPolicy()).Build(transport);

        var withPolicy = BestOfRounds(() => instrumented.Send(s_request, CancellationToken.None));
        var without = BestOfRounds(() => passThrough.Send(s_request, CancellationToken.None));

        Assert.True(withPolicy <= without, $"instrumented {withPolicy} bytes, pass-through {without} bytes over {Iterations} calls");
    }

    // The least allocation over several rounds, after a warm-up long enough for tiered JIT to have promoted the hot path:
    // one-off type-loading and JIT allocations belong to the early rounds, not to the steady state OBS-1 is about.
    private static long BestOfRounds(Action action)
    {
        for (var i = 0; i < 500; i++)
        {
            action();
        }

        var best = long.MaxValue;
        for (var round = 0; round < 6; round++)
        {
            best = Math.Min(best, Allocated(action));
        }

        return best;
    }

    [Fact]
    public async Task The_redacted_url_is_not_computed_on_the_disabled_path()
    {
        using var cached = TestResponses.Create(Status.Ok);
        var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(new DisabledLogger())).Build(new RecordingTransport(_ => cached));
        var shortUrl = Request.Get("https://api.example.com/p?a=1");
        var longUrl = Request.Get("https://api.example.com/p?a=" + new string('x', 20_000));

        var shortCost = await PerCallAsync(pipeline, shortUrl);
        var longCost = await PerCallAsync(pipeline, longUrl);

        // Redacting the 20 KB URL would cost tens of KB a call; a skipped redaction costs the same as the short one.
        Assert.True(longCost - shortCost < 200, $"short {shortCost} B, long {longCost} B per call");
    }

    private static async Task<long> PerCallAsync(HttpPipeline pipeline, Request request)
    {
        for (var i = 0; i < WarmUp; i++)
        {
            await pipeline.SendAsync(request, CancellationToken.None);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            await pipeline.SendAsync(request, CancellationToken.None);
        }

        return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
    }

    private sealed class PassThroughPolicy : HttpPipelinePolicy
    {
        public override PipelineStage Stage => PipelineStage.Diagnostics;

        public override ValueTask<Response> ProcessAsync(Request request, PipelineContext context, PipelineRunner continuation) =>
            continuation.RunAsync(request, context);

        public override Response Process(Request request, PipelineContext context, PipelineRunner continuation) =>
            continuation.Run(request, context);
    }

}
