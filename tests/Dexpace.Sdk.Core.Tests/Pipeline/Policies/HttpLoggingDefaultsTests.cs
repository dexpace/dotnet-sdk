// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Transports;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

/// <summary>OBS-34, XCUT-19(c) and (e): logging is off by default and default-deny on headers.</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class HttpLoggingDefaultsTests
{
    private static Request Get() => Request.Get("https://api.example.com/v1/items?token=SECRET");

    [Fact]
    public async Task The_default_options_emit_no_http_event()
    {
        var logger = new RecordingLogger();
        using var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(logger)).Build(new RecordingTransport());

        using var ok = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new PipelineBuilder().Add(new InstrumentationPolicy(logger))
                .Build(new RecordingTransport(_ => throw new InvalidOperationException("x")))
                .SendAsync(Get(), TestContext.Current.CancellationToken).AsTask());

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task Spans_and_instruments_still_record_at_None()
    {
        using var activities = new ActivityRecorder("Dexpace.Sdk");
        using var metrics = new MetricRecorder("Dexpace.Sdk", "http.client.request.duration", "http.client.active_requests");
        using var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(new RecordingLogger())).Build(new RecordingTransport());

        using var response = await pipeline.SendAsync(Get(), TestContext.Current.CancellationToken);

        var activity = Assert.Single(activities.Started);
        Assert.Equal("https://api.example.com/v1/items?token=***", activity.GetTagItem("url.full"));
        Assert.Single(metrics.For("http.client.request.duration"));
        Assert.Equal([1d, -1d], metrics.For("http.client.active_requests").Select(m => Convert.ToDouble(m.Value, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Headers_level_logs_headers_only_and_never_a_body_preview()
    {
        var logger = new RecordingLogger();
        using var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(logger))
            .Build(new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: ResponseBody.FromBytes("payload"u8.ToArray()))),
                new DexpaceClientOptions { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers } });
        var request = Request.Post("https://api.example.com/v1/items", RequestBody.FromString("secret-body"));

        using var response = await pipeline.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEmpty(logger.Entries);
        Assert.DoesNotContain(logger.Entries, e => e.State.Any(p => p.Key.Contains(".preview", StringComparison.Ordinal)));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("payload", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_secret_header_value_never_appears_in_any_event_field_or_message()
    {
        var logger = new RecordingLogger();
        var secretResponse = new Headers.Builder()
            .Add("Set-Cookie", "session=SECRET")
            .Add("WWW-Authenticate", "Bearer realm=SECRET")
            .Add("Content-Type", "text/plain")
            .Build();
        using var pipeline = new PipelineBuilder().Add(new InstrumentationPolicy(logger))
            .Build(new RecordingTransport(_ => TestResponses.Create(Status.Ok, headers: secretResponse)),
                new DexpaceClientOptions { Logging = new HttpLoggingOptions { Level = HttpLogLevel.Headers } });
        var request = Get().WithHeaders(
            new Headers.Builder()
                .Add("Authorization", "Bearer SECRET")
                .Add("Cookie", "id=SECRET")
                .Add("X-Api-Key", "SECRET")
                .Add("Accept", "application/json")
                .Build());

        using var response = await pipeline.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.All(logger.Entries, e => Assert.DoesNotContain("SECRET", e.Message, StringComparison.Ordinal));
        Assert.All(
            logger.Entries.SelectMany(e => e.State),
            p => Assert.DoesNotContain("SECRET", p.Value as string ?? string.Empty, StringComparison.Ordinal));
        Assert.Equal("REDACTED", logger.Entries[0]["http.request.header.authorization"]);
        Assert.Equal("REDACTED", logger.Entries[1]["http.response.header.set-cookie"]);
        Assert.Equal("application/json", logger.Entries[0]["http.request.header.accept"]);
    }
}
