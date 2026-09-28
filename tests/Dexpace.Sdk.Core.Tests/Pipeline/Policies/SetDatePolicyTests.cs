// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Common;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Pipeline;
using Dexpace.Sdk.Core.Pipeline.Policies;
using Dexpace.Sdk.TestSupport.Transports;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Pipeline.Policies;

[Trait("Category", "Unit")]
public sealed class SetDatePolicyTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Request MakeRequest() => Request.Get("https://api.example.com/v1/items");

    private static DexpaceClientOptions MakeOptions() => new();

    // -------------------------------------------------------------------------
    // Stage
    // -------------------------------------------------------------------------

    [Fact]
    public void Stage_IsPerAttempt()
    {
        var policy = new SetDatePolicy();
        Assert.Equal(PipelineStage.PerAttempt, policy.Stage);
    }

    // -------------------------------------------------------------------------
    // Date header stamping
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ProcessAsync_SetsDateHeader_ReplacesPriorValue()
    {
        // Arrange: fixed time so we can assert the exact value
        var fixedUtc = new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);
        var provider = new FakeTimeProvider(fixedUtc);

        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new SetDatePolicy(provider))
            .Build(transport);

        // Act
        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);

        // Assert: RFC 1123 "r" format
        var dateHeader = transport.LastRequest!.Headers.Get("Date");
        Assert.Equal(fixedUtc.ToString("r"), dateHeader);
    }

    [Fact]
    public async Task ProcessAsync_ReplacesExistingDateHeader()
    {
        // Arrange: request already has a stale Date header
        var fixedUtc = new DateTimeOffset(2026, 6, 14, 9, 0, 0, TimeSpan.Zero);
        var provider = new FakeTimeProvider(fixedUtc);

        var staleRequest = MakeRequest() with
        {
            Headers = Headers.Empty.Set("Date", "Mon, 01 Jan 2024 00:00:00 GMT")
        };

        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new SetDatePolicy(provider))
            .Build(transport);

        await pipeline.SendAsync(staleRequest, MakeOptions(), TestContext.Current.CancellationToken);

        // The stale value must be replaced, not appended
        var values = transport.LastRequest!.Headers.GetAll("Date");
        Assert.Single(values);
        Assert.Equal(fixedUtc.ToString("r"), values[0]);
    }

    [Fact]
    public async Task ProcessAsync_UsesSystemTimeProvider_WhenNullPassed()
    {
        // Arrange: no explicit time provider → uses TimeProvider.System
        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new SetDatePolicy())   // null → TimeProvider.System
            .Build(transport);

        var before = DateTimeOffset.UtcNow;
        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);
        var after = DateTimeOffset.UtcNow;

        var raw = transport.LastRequest!.Headers.Get("Date");
        Assert.NotNull(raw);
        var parsed = DateTimeOffset.ParseExact(raw, "r", null);
        Assert.InRange(parsed, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public async Task ProcessAsync_StampsNewDatePerAttempt()
    {
        // Arrange: increment time between two calls
        var times = new[]
        {
            new DateTimeOffset(2026, 6, 14, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 14, 10, 0, 5, TimeSpan.Zero),
        };
        var provider = new SequentialTimeProvider(times);

        var transport = new RecordingTransport();
        var pipeline = new PipelineBuilder()
            .Add(new SetDatePolicy(provider))
            .Build(transport);

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);
        var first = transport.LastRequest!.Headers.Get("Date");

        await pipeline.SendAsync(MakeRequest(), MakeOptions(), TestContext.Current.CancellationToken);
        var second = transport.LastRequest!.Headers.Get("Date");

        Assert.NotEqual(first, second);
    }

    // -------------------------------------------------------------------------
    // Fake TimeProvider helpers
    // -------------------------------------------------------------------------

    private sealed class SequentialTimeProvider(DateTimeOffset[] times) : TimeProvider
    {
        private int _index;

        public override DateTimeOffset GetUtcNow() => times[_index++ % times.Length];
    }
}
