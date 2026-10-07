// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Client;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>OBS-24, OBS-10, ASYNC-9, P5b-20: the diagnostic context reaches the log events.</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class DiagnosticContextFlowTests
{
    private static readonly DexpaceClientOptions s_headers = new() { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers } };

    private static IAsyncHttpClient OtherThreadTransport() =>
        DelegateHttpClient.Create(async (_, _, _) =>
        {
            await Task.Yield();
            return await Task.Run(() => TestResponses.Create(Status.Ok));
        });

    [Fact]
    public async Task A_caller_activity_and_scope_are_visible_at_the_response_event_after_the_transport_completes_on_another_thread()
    {
        using var source = new ActivitySource("Dexpace.Sdk.Core.Tests.CallerFlow");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new RecordingLogger();
        using var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(logger)).Build(OtherThreadTransport(), s_headers);

        using var caller = source.StartActivity("caller");
        using (logger.BeginScope("order-42"))
        {
            using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/v1/items"), TestContext.Current.CancellationToken);
        }

        var entry = logger.Entries.Single(e => e.EventId.Id == 101);
        Assert.NotNull(caller);
        Assert.Equal(caller.TraceId, entry.Activity?.TraceId);
        Assert.Contains("order-42", entry.Scopes);
    }

    [Fact]
    public async Task The_attempt_span_is_current_when_http_request_is_emitted()
    {
        using var recorder = ActivityRecorder.Scoped("Dexpace.Sdk");
        var logger = new RecordingLogger();
        using var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(logger)).Build(new RecordingTransport(), s_headers);

        using var response = await pipeline.SendAsync(Request.Get("https://api.example.com/v1/items"), TestContext.Current.CancellationToken);

        var attempt = Assert.Single(recorder.StartedOfKind(System.Diagnostics.ActivityKind.Client));
        Assert.Same(attempt, logger.Entries.Single(e => e.EventId.Id == 100).Activity);
        Assert.Same(attempt, logger.Entries.Single(e => e.EventId.Id == 101).Activity);
    }

    [Fact]
    public void No_SDK_source_suppresses_execution_context_flow()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BannedSymbols.txt")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var banned = File.ReadAllLines(Path.Combine(directory.FullName, "BannedSymbols.txt"));

        Assert.Contains(banned, line => line.StartsWith("M:System.Threading.ExecutionContext.SuppressFlow;", StringComparison.Ordinal));
    }
}
