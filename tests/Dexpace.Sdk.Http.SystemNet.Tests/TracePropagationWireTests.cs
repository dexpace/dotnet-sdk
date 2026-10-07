// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.Http.SystemNet.Tests.Loopback;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;
using SystemHttpClient = System.Net.Http.HttpClient;

namespace Dexpace.Sdk.Http.SystemNet.Tests;

/// <summary>
/// Design §8.1, phase 5c P5c-11, P5c-12: at the wire, the SDK's <c>traceparent</c> stamp must not hide the runtime's own
/// child span. Listeners are process-wide, so the class is its own collection and each test uses a scoped recorder.
/// </summary>
[Collection("TracePropagation")]
[Trait("Category", "Integration")]
public sealed class TracePropagationWireTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SystemNetHttpClient OwnedTransport() => new(handler => handler.UseProxy = false);

    private static SystemHttpClient BorrowedClient(DistributedContextPropagator? propagator = null)
    {
        var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false };
        if (propagator is not null)
        {
            handler.ActivityHeadersPropagator = propagator;
        }

        return new SystemHttpClient(handler);
    }

    private static HttpPipeline PipelineOver(SystemNetHttpClient transport) =>
        new PipelineBuilder().Add(new InstrumentationPolicy()).Build(transport);

    private static (string TraceId, string ParentId) Parse(string traceparent)
    {
        var parts = traceparent.Split('-');
        return (parts[1], parts[2]);
    }

    [Fact]
    public async Task The_runtime_child_span_is_the_wire_parent()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk", "System.Net.Http");
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        await using var transport = OwnedTransport();
        using var pipeline = PipelineOver(transport);

        using var response = await pipeline.SendAsync(Request.Get(server.Url("/traced").AbsoluteUri), Ct);

        var attempt = Assert.Single(recorder.StartedOfKind(ActivityKind.Client), a => a.Source.Name == "Dexpace.Sdk");
        var runtime = Assert.Single(recorder.Started, a => a.Source.Name == "System.Net.Http");
        var wire = Parse(Assert.Single(server.Requests).Header("traceparent")!);
        Assert.Equal(runtime.SpanId.ToHexString(), wire.ParentId);
        Assert.NotEqual(attempt.SpanId.ToHexString(), wire.ParentId);
        Assert.Equal(attempt.TraceId.ToHexString(), wire.TraceId);
        Assert.Equal(attempt.Id, runtime.ParentId);
    }

    [Fact]
    public async Task The_attempt_span_is_the_wire_parent_without_a_runtime_listener()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        await using var transport = OwnedTransport();
        using var pipeline = PipelineOver(transport);

        using var response = await pipeline.SendAsync(Request.Get(server.Url("/traced").AbsoluteUri), Ct);

        var attempt = Assert.Single(recorder.StartedOfKind(ActivityKind.Client));
        var wire = Parse(Assert.Single(server.Requests).Header("traceparent")!);
        Assert.Equal(attempt.SpanId.ToHexString(), wire.ParentId);
        Assert.Equal(attempt.TraceId.ToHexString(), wire.TraceId);
    }

    [Fact]
    public async Task The_tracestate_the_sdk_stamped_is_not_duplicated_on_the_wire()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk", "System.Net.Http");
        recorder.Root!.TraceStateString = "vendor=value";
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        await using var transport = OwnedTransport();
        using var pipeline = PipelineOver(transport);

        using var response = await pipeline.SendAsync(Request.Get(server.Url("/traced").AbsoluteUri), Ct);

        var sent = Assert.Single(server.Requests);
        Assert.Single(sent.HeaderValues("traceparent"));
        Assert.Equal(["vendor=value"], sent.HeaderValues("tracestate"));
    }

    [Fact]
    public async Task A_caller_traceparent_is_not_stripped()
    {
        const string Caller = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        await using var transport = OwnedTransport();
        var request = Request.Create(
            Dexpace.Sdk.Core.Http.Common.Method.Get,
            server.Url("/caller").AbsoluteUri,
            new Dexpace.Sdk.Core.Http.Common.Headers.Builder().Add("traceparent", Caller).Build());

        await using var response = await transport.ExecuteAsync(request, Ct);

        Assert.Equal([Caller], Assert.Single(server.Requests).HeaderValues("traceparent"));
    }

    [Fact]
    public async Task A_borrowed_client_gets_the_same_treatment_as_an_owned_one()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk", "System.Net.Http");
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        using var client = BorrowedClient();
        await using var transport = new SystemNetHttpClient(client);
        using var pipeline = PipelineOver(transport);

        using var response = await pipeline.SendAsync(Request.Get(server.Url("/borrowed").AbsoluteUri), Ct);

        var runtime = Assert.Single(recorder.Started, a => a.Source.Name == "System.Net.Http");
        Assert.Equal(runtime.SpanId.ToHexString(), Parse(Assert.Single(server.Requests).Header("traceparent")!).ParentId);
    }

    [Fact]
    public async Task A_borrowed_client_whose_handler_has_a_no_output_propagator_sends_no_traceparent_for_a_traced_call()
    {
        // The documented residual (P5c-11): the adapter cannot see the handler's propagator, so while a System.Net.Http
        // listener exists a traced call through a borrowed client that injects nothing carries no traceparent.
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk", "System.Net.Http");
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        using var client = BorrowedClient(DistributedContextPropagator.CreateNoOutputPropagator());
        await using var transport = new SystemNetHttpClient(client);
        using var pipeline = PipelineOver(transport);

        using var response = await pipeline.SendAsync(Request.Get(server.Url("/residual").AbsoluteUri), Ct);

        Assert.Empty(Assert.Single(server.Requests).HeaderValues("traceparent"));
    }

    [Fact]
    public async Task Without_a_runtime_listener_the_sdk_stamp_survives_even_a_handler_that_injects_nothing()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        using var client = BorrowedClient(DistributedContextPropagator.CreateNoOutputPropagator());
        await using var transport = new SystemNetHttpClient(client);
        using var pipeline = PipelineOver(transport);

        using var response = await pipeline.SendAsync(Request.Get(server.Url("/stamp").AbsoluteUri), Ct);

        var attempt = Assert.Single(recorder.StartedOfKind(ActivityKind.Client));
        Assert.Equal(attempt.SpanId.ToHexString(), Parse(Assert.Single(server.Requests).Header("traceparent")!).ParentId);
    }

    [Fact]
    public async Task An_untraced_call_sends_no_sdk_traceparent()
    {
        await using var server = LoopbackServer.Start(LoopbackResponse.Ok("ok"));
        await using var transport = OwnedTransport();
        using var pipeline = PipelineOver(transport);

        using var response = await pipeline.SendAsync(Request.Get(server.Url("/untraced").AbsoluteUri), Ct);

        Assert.Empty(Assert.Single(server.Requests).HeaderValues("traceparent"));
    }
}
