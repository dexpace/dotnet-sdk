// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Diagnostics;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Http.Response;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Dexpace.Sdk.TestSupport.Time;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Diagnostics;

/// <summary>OBS-2, OBS-3, OBS-4, OBS-39: everything the SDK logs belongs to the published catalogue.</summary>
[Collection("Instrumentation")]
[Trait("Category", "Unit")]
public sealed class LogCatalogueTests
{
    private static readonly HashSet<string> s_eventNames = Constants(typeof(DexpaceLogEvents));

    private static readonly HashSet<string> s_keys = [.. Constants(typeof(DexpaceLogKeys)), InternalLogKeys.DisposeResourceType, "{OriginalFormat}"];

    private static readonly LogLevel[] s_allowedLevels = [LogLevel.Error, LogLevel.Warning, LogLevel.Information, LogLevel.Debug];

    private static HashSet<string> Constants(Type type) =>
        [.. type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!)];

    private static DexpaceClientOptions Options(HttpLogLevel level) => new() { Logging = new HttpLoggingOptions { Level = level } };

    private static Request Get() => Request.Get("https://api.example.com/v1/items?token=SECRET");

    private static async Task<IReadOnlyList<RecordedLog>> ScenariosAsync(HttpLogLevel level)
    {
        var recorded = new List<RecordedLog>();

        var success = new RecordingLogger();
        using var a = await new PipelineBuilder().Add(new InstrumentationPolicy(success))
            .Build(new RecordingTransport(), Options(level)).SendAsync(Get(), TestContext.Current.CancellationToken);
        recorded.AddRange(success.Entries);

        var failure = new RecordingLogger();
        await Assert.ThrowsAsync<ServiceRequestException>(
            () => new PipelineBuilder().Add(new InstrumentationPolicy(failure))
                .Build(new RecordingTransport(_ => throw new ServiceRequestException("never sent")), Options(level))
                .SendAsync(Get(), TestContext.Current.CancellationToken).AsTask());
        recorded.AddRange(failure.Entries);

        var broken = new ThrowingLogger { ThrowOnLogFirst = 1 };
        using var b = await new PipelineBuilder().Add(new InstrumentationPolicy(broken))
            .Build(new RecordingTransport(), Options(level)).SendAsync(Get(), TestContext.Current.CancellationToken);
        recorded.AddRange(broken.Entries);

        var dispose = new RecordingLogger();
        using var superseded = new TrackingResponseBody([], "retried", new IOException("dispose failed"));
        using var c = await new PipelineBuilder().Add(new InstrumentationPolicy(dispose)).Add(new RetryPolicy(new InstantTimeProvider()))
            .Build(
                new ScriptedTransport(TestResponses.Create(Status.ServiceUnavailable, body: superseded), TestResponses.Create(Status.Ok)),
                new DexpaceClientOptions
                {
                    Logging = new HttpLoggingOptions { Level = level },
                    Retry = new RetryOptions { BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) },
                })
            .SendAsync(Get(), TestContext.Current.CancellationToken);
        recorded.AddRange(dispose.Entries);

        if (level == HttpLogLevel.Body)
        {
            var capture = new RecordingLogger();
            var failing = ResponseBody.FromStream(new FailingStream(), MediaType.Parse("text/plain"), 10);
            using var d = await new PipelineBuilder().Add(new InstrumentationPolicy(capture))
                .Build(new RecordingTransport(_ => TestResponses.Create(Status.Ok, body: failing)), Options(level))
                .SendAsync(Get(), TestContext.Current.CancellationToken);
            recorded.AddRange(capture.Entries);
        }

        return recorded;
    }

    private sealed class FailingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("capture failed");

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData(HttpLogLevel.Headers)]
    [InlineData(HttpLogLevel.Body)]
    public async Task Every_recorded_event_belongs_to_the_catalogue(HttpLogLevel level)
    {
        var entries = await ScenariosAsync(level);

        Assert.Contains(entries, e => e.EventId.Id == DexpaceLogEvents.LogFailedId);
        Assert.Contains(entries, e => e.EventId.Id == DexpaceLogEvents.DisposeSuppressedId);
        Assert.Contains(entries, e => e.EventId.Id == DexpaceLogEvents.HttpFailureId);
        Assert.Equal(level == HttpLogLevel.Body, entries.Any(e => e.EventId.Id == DexpaceLogEvents.BodyCaptureFailedId));
        Assert.Equal(level == HttpLogLevel.Body, entries.Any(e => e.Has(DexpaceLogKeys.HttpResponseBodyPreview)));
        Assert.All(entries, e =>
        {
            Assert.True(s_eventNames.Contains(e.EventId.Name!), $"{e.EventId.Name} is not in the catalogue");
            Assert.Contains(e.Level, s_allowedLevels);
            foreach (var (key, _) in e.State)
            {
                Assert.False(string.IsNullOrEmpty(key));
                Assert.NotEqual("event", key);
                Assert.True(
                    s_keys.Contains(key)
                        || key.StartsWith(DexpaceLogKeys.HttpRequestHeaderPrefix, StringComparison.Ordinal)
                        || key.StartsWith(DexpaceLogKeys.HttpResponseHeaderPrefix, StringComparison.Ordinal),
                    $"'{key}' is not in the catalogue");
            }
        });
    }
}
