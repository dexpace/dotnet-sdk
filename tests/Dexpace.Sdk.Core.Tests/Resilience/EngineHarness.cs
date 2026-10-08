// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Configuration;
using Dexpace.Sdk.Core.Http.Request;
using Dexpace.Sdk.Core.Recovery;
using Dexpace.Sdk.Core.Resilience;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Resilience;

/// <summary>A fake clock that records every timer it is asked to arm.</summary>
internal sealed class RecordingFakeTimeProvider : FakeTimeProvider
{
    private readonly List<TimeSpan> _timers = [];

    internal IReadOnlyList<TimeSpan> Timers
    {
        get
        {
            lock (_timers)
            {
                return [.. _timers];
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_timers)
        {
            _timers.Add(dueTime);
        }

        return base.CreateTimer(callback, state, dueTime, period);
    }
}

/// <summary>Helpers the engine, budget and recovery tests share.</summary>
internal static class EngineHarness
{
    internal static readonly Request s_get = Request.Get("https://api.example.test/items");

    internal static readonly Request s_barePost = new(Core.Http.Common.Method.Post, new Uri("https://api.example.test/items"));

    /// <summary>Options with no jitter and a zero base delay unless overridden.</summary>
    internal static RetryOptions Options(
        int retries = 2,
        double baseMs = 0,
        double jitter = 0,
        TimeSpan? fixedDelay = null,
        bool honor = true) =>
        new()
        {
            MaxRetryAttempts = retries,
            BaseDelay = TimeSpan.FromMilliseconds(baseMs),
            MaxDelay = TimeSpan.FromSeconds(8),
            Jitter = jitter,
            FixedDelay = fixedDelay,
            HonorRetryAfter = honor,
        };

    /// <summary>A send that returns <paramref name="script"/>(send number) and counts calls.</summary>
    internal static RetrySend Script(Func<int, Outcome> script, Action<int>? onSend = null) =>
        (_, send, _, _) =>
        {
            onSend?.Invoke(send);
            return ValueTask.FromResult(script(send));
        };

    /// <summary>Runs <paramref name="task"/> to completion while advancing <paramref name="clock"/> in steps.</summary>
    internal static async Task<T> DriveAsync<T>(Task<T> task, FakeTimeProvider clock, TimeSpan? step = null)
    {
        var advance = step ?? TimeSpan.FromSeconds(1);
        for (var i = 0; i < 200_000 && !task.IsCompleted; i++)
        {
            clock.Advance(advance);
            await Task.Yield();
        }

        Assert.True(task.IsCompleted, "The run did not complete while the clock advanced.");
        return await task;
    }

    internal static Exception Error(Outcome outcome) =>
        Assert.IsAssignableFrom<Outcome.Failure>(outcome).Error;
}
