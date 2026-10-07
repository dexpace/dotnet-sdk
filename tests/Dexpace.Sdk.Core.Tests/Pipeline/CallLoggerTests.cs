// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

/// <summary>OBS-20 plumbing, P5b-6, plan reading R4: the logger on the call.</summary>
[Trait("Category", "Unit")]
public sealed class CallLoggerTests
{
    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/resource");

    private static DelegatePolicy Probe(Action<ILogger> seen) =>
        new(
            PipelineStage.Serde,
            (request, context, next) =>
            {
                seen(context.State.Logger);
                return next.RunAsync(request, context);
            });

    [Fact]
    public async Task The_calls_logger_is_the_diagnostics_policys_logger()
    {
        var recording = new RecordingLogger();
        ILogger? seen = null;
        using var pipeline = new PipelineBuilder()
            .Add(new InstrumentationPolicy(recording))
            .Add(Probe(l => seen = l))
            .Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Same(recording, seen);
    }

    [Fact]
    public async Task Without_an_InstrumentationPolicy_the_logger_is_NullLogger()
    {
        ILogger? seen = null;
        using var pipeline = new PipelineBuilder().Add(Probe(l => seen = l)).Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Same(NullLogger.Instance, seen);
    }

    [Fact]
    public async Task A_nested_pipeline_uses_its_own_logger()
    {
        var innerLogger = new RecordingLogger();
        var outerLogger = new RecordingLogger();
        ILogger? innerSeen = null;
        ILogger? outerSeen = null;
        using var inner = new PipelineBuilder()
            .Add(new InstrumentationPolicy(innerLogger))
            .Add(Probe(l => innerSeen = l))
            .Build(new RecordingTransport());
        using var outer = new PipelineBuilder()
            .Add(new InstrumentationPolicy(outerLogger))
            .Add(Probe(l => outerSeen = l))
            .Build(inner);

        using var response = await outer.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Same(outerLogger, outerSeen);
        Assert.Same(innerLogger, innerSeen);
    }

    [Fact]
    public async Task CreateDefault_forwards_its_logger()
    {
        var recording = new RecordingLogger();
        ILogger? seen = null;
        var builder = PipelineBuilder.Flatten(DexpacePipeline.CreateDefault(new RecordingTransport(), logger: recording)).Add(Probe(l => seen = l));
        using var pipeline = builder.Build();

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Same(recording, seen);
    }
}
