// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>OBS-20, OBS-6, XCUT-20, RETRY-25: a logging failure never fails the request it describes.</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class EmissionGuardTests
{
    private static readonly DexpaceClientOptions s_headers = new() { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers } };

    private static HttpPipeline Pipeline(ILogger logger, Func<Request, Response>? respond = null) =>
        new PipelineBuilder().Add(new InstrumentationPolicy(logger)).Build(new RecordingTransport(respond), s_headers);

    private static Request Get() => Request.Get("https://api.example.com/v1/items");

    [Fact]
    public async Task A_logger_whose_Log_throws_does_not_fail_the_request()
    {
        var logger = new ThrowingLogger { ThrowOnLogFirst = 1 };

        using var response = await Pipeline(logger).SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Equal([100, 120, 101], logger.Attempts.Select(e => e.EventId.Id));
        var diagnostic = logger.Entries[0];
        Assert.Equal(LogLevel.Warning, diagnostic.Level);
        Assert.Equal("http.instrumentation.log_failed", diagnostic.EventId.Name);
        Assert.Equal("http.request", diagnostic[DexpaceLogKeys.FailedEvent]);
        Assert.Equal(typeof(InvalidOperationException).FullName, diagnostic[DexpaceLogKeys.ErrorType]);
        Assert.IsType<InvalidOperationException>(diagnostic.Exception);
    }

    [Fact]
    public async Task A_logger_whose_IsEnabled_throws_does_not_fail_the_request()
    {
        var logger = new ThrowingLogger { ThrowOnIsEnabled = true };

        using var response = await Pipeline(logger).SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
    }

    [Fact]
    public async Task A_second_failure_while_reporting_the_first_is_swallowed()
    {
        var logger = new ThrowingLogger { ThrowOnLog = true };

        using var response = await Pipeline(logger).SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);

        // One failed event plus at most one diagnostic attempt for each of http.request and http.response: no recursion.
        Assert.Equal([100, 120, 101, 120], logger.Attempts.Select(e => e.EventId.Id));
    }

    [Fact]
    public async Task A_fatal_exception_from_the_logger_propagates_unlogged()
    {
        var logger = new ThrowingLogger { ThrowOnLog = true, ExceptionFactory = static () => new InsufficientMemoryException() };

        await Assert.ThrowsAsync<InsufficientMemoryException>(
            () => Pipeline(logger).SendAsync(Get(), TestContext.Current.CancellationToken).AsTask());

        Assert.Equal([100], logger.Attempts.Select(e => e.EventId.Id));
    }

    [Fact]
    public async Task An_OperationCanceledException_from_the_calls_own_token_propagates()
    {
        using var cts = new CancellationTokenSource();
        var logger = new ThrowingLogger
        {
            ThrowOnLog = true,
            ExceptionFactory = () =>
            {
                cts.Cancel();
                return new OperationCanceledException(cts.Token);
            },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Pipeline(logger).SendAsync(Get(), cts.Token).AsTask());
    }

    [Fact]
    public async Task An_unrelated_OperationCanceledException_from_the_logger_is_swallowed()
    {
        var logger = new ThrowingLogger { ThrowOnLogFirst = 1, ExceptionFactory = static () => new OperationCanceledException() };

        using var response = await Pipeline(logger).SendAsync(Get(), TestContext.Current.CancellationToken);

        Assert.Equal(Status.Ok, response.Status);
        Assert.Contains(logger.Entries, e => e.EventId.Id == DexpaceLogEvents.LogFailedId);
    }

    [Fact]
    public async Task An_exception_whose_Message_throws_does_not_break_the_failure_path()
    {
        var logger = new ProviderLikeLogger();
        var failure = new MessageThrows();

        Exception? thrown = null;
        try
        {
            await Pipeline(logger, _ => throw failure).SendAsync(Get(), TestContext.Current.CancellationToken);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            thrown = ex;
        }

        // xUnit's own ThrowsAsync reads Message while matching, which is the very getter under test: match by hand.

        Assert.True(ReferenceEquals(failure, thrown), "the original exception must surface, not the logger's");
        var diagnostics = logger.Entries.Where(e => e.EventId.Id == DexpaceLogEvents.LogFailedId).ToList();
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("http.response", diagnostic[DexpaceLogKeys.FailedEvent]);
        Assert.Equal(LogLevel.Warning, diagnostic.Level);
    }

    [Fact]
    public async Task A_throwing_ActivityListener_is_not_swallowed_by_the_log_guard()
    {
        var logger = new RecordingLogger();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dexpace.Sdk",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = static _ => throw new InvalidOperationException("listener failed"),
        };
        ActivitySource.AddActivityListener(listener);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pipeline(logger).SendAsync(Get(), TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("listener failed", thrown.Message);
        Assert.DoesNotContain(logger.Entries, e => e.EventId.Id == DexpaceLogEvents.LogFailedId);
    }

    [Fact]
    public async Task A_throwing_MeterListener_is_not_swallowed_by_the_log_guard()
    {
        var logger = new RecordingLogger();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Dexpace.Sdk" && instrument.Name == "http.client.request.duration")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>(static (_, _, _, _) => throw new InvalidOperationException("meter failed"));
        listener.Start();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pipeline(logger).SendAsync(Get(), TestContext.Current.CancellationToken).AsTask());

        Assert.Equal("meter failed", thrown.Message);
        Assert.DoesNotContain(logger.Entries, e => e.EventId.Id == DexpaceLogEvents.LogFailedId);
    }

    private sealed class MessageThrows : Exception
    {
        public override string Message => throw new InvalidOperationException("Message getter threw.");
    }
}
