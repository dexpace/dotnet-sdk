// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Collections.ObjectModel;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.TestSupport.Pipeline;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline;

[Trait("Category", "Unit")]
public class HttpPipelineTests
{
    private static Request MakeRequest() =>
        Request.Get("https://api.example.com/v1/resource");

    private static DexpaceClientOptions MakeOptions() => new();

    [Fact]
    public async Task SendAsync_ReturnsTransportResponse()
    {
        var expected = TestResponses.Create(Status.Ok);
        var pipeline = new PipelineBuilder().Build(new RecordingTransport(_ => expected));

        var actual = await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Send_ReturnsTransportResponse()
    {
        var expected = TestResponses.Create(Status.Ok);
        var pipeline = new PipelineBuilder().Build(new RecordingTransport(_ => expected));

        var actual = pipeline.Send(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task SendAsync_WithPolicies_PoliciesInvokedAndResponseReturned()
    {
        var log = new List<string>();
        var expected = TestResponses.Create(Status.Ok);

        var pipeline = new PipelineBuilder()
            .Add(new ProbePolicy("a", PipelineStage.Operation, log))
            .Add(new ProbePolicy("b", PipelineStage.PerAttempt, log))
            .Build(new RecordingTransport(_ => expected));

        var actual = await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        Assert.Same(expected, actual);
        Assert.Equal(["a:in", "b:in", "b:out", "a:out"], log);
    }

    [Fact]
    public void Policies_is_an_ordered_read_only_view()
    {
        var log = new List<string>();
        var late = new ProbePolicy("late", PipelineStage.Diagnostics, log);
        var early = new ProbePolicy("early", PipelineStage.Operation, log);
        var builder = new PipelineBuilder().Add(late).Add(early);

        var pipeline = builder.Build(new RecordingTransport());
        builder.Add(new ProbePolicy("after", PipelineStage.PerAttempt, log));

        Assert.Equal(2, pipeline.Policies.Count);
        Assert.Same(early, pipeline.Policies[0]);
        Assert.Same(late, pipeline.Policies[1]);
        Assert.IsType<ReadOnlyCollection<HttpPipelinePolicy>>(pipeline.Policies);
        Assert.True(((IList<HttpPipelinePolicy>)pipeline.Policies).IsReadOnly);
    }

    [Fact]
    public void HttpPipeline_has_no_settable_member()
    {
        // PIPE-10: the pipeline is immutable after build.
        Assert.All(
            typeof(HttpPipeline).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
            property => Assert.Null(property.SetMethod));
        Assert.All(
            typeof(HttpPipeline).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance),
            field => Assert.True(field.IsInitOnly));
    }

    [Fact]
    public async Task SendAsync_with_a_default_literal_token_binds_to_the_token_overload()
    {
        // Fact 7: no overload of the family carries an optional token, and a default literal binds to the token one.
        var pipeline = new PipelineBuilder().Build(new RecordingTransport());

        // The default literal is the point of the test: it must pick the token overload (xUnit1051 does not apply).
#pragma warning disable xUnit1051
        using var response = await pipeline.SendAsync(MakeRequest(), default);
#pragma warning restore xUnit1051

        Assert.Equal(Status.Ok, response.Status);
    }

    [Fact]
    public async Task The_DexpaceClientOptions_overloads_override_the_build_time_options_for_one_call()
    {
        // P4c-15: the build-time options are captured, and the per-call overload wins for one call only.
        var seenUserAgents = new List<string>();
        var probe = new DelegatePolicy(
            PipelineStage.PerAttempt,
            (request, context, next) =>
            {
                seenUserAgents.Add(context.Options.UserAgent);
                return next.RunAsync(request, context);
            });
        var buildTime = new DexpaceClientOptions { UserAgent = "build-time/1" };
        var pipeline = new PipelineBuilder().Add(probe).Build(new RecordingTransport(), buildTime);

        using var first = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);
        using var second = await pipeline.SendAsync(MakeRequest(), new DexpaceClientOptions { UserAgent = "per-call/2" }, TestContext.Current.CancellationToken);
        using var third = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(["build-time/1", "per-call/2", "build-time/1"], seenUserAgents);
    }

    [Fact]
    public async Task Build_transport_captures_default_client_options()
    {
        var seen = new List<DexpaceClientOptions>();
        var probe = new DelegatePolicy(
            PipelineStage.PerAttempt,
            (request, context, next) =>
            {
                seen.Add(context.Options);
                return next.RunAsync(request, context);
            });
        var pipeline = new PipelineBuilder().Add(probe).Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Single(seen);
        Assert.Equal(new DexpaceClientOptions().UserAgent, seen[0].UserAgent);
    }

    [Fact]
    public async Task Build_transport_options_captures_the_callers()
    {
        var mine = new DexpaceClientOptions { UserAgent = "mine/1" };
        DexpaceClientOptions? seen = null;
        var probe = new DelegatePolicy(
            PipelineStage.PerAttempt,
            (request, context, next) =>
            {
                seen = context.Options;
                return next.RunAsync(request, context);
            });
        var pipeline = new PipelineBuilder().Add(probe).Build(new RecordingTransport(), mine);

        using var response = await pipeline.SendAsync(MakeRequest(), TestContext.Current.CancellationToken);

        Assert.Same(mine, seen);
    }

    [Fact]
    public async Task A_null_request_is_delivered_through_the_task()
    {
        var pipeline = new PipelineBuilder().Build(new RecordingTransport());

        var task = pipeline.SendAsync(null!, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ArgumentNullException>(() => task.AsTask());
    }
}
