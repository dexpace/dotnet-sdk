// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

/// <summary>CTX-19, P6c-25: background work captures no ambient state.</summary>
[Trait("Category", "Unit")]
public sealed class BackgroundWorkTests
{
    private static readonly AsyncLocal<string?> s_ambient = new();

    [Fact]
    public async Task The_work_observes_no_Activity_current()
    {
        using var source = new ActivitySource("Dexpace.Tests.BackgroundWork");
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity("caller");
        Assert.NotNull(Activity.Current);
        Activity? seen = new Activity("sentinel");

        await BackgroundWork.Run(() =>
        {
            seen = Activity.Current;
            return Task.CompletedTask;
        });

        Assert.Null(seen);
    }

    [Fact]
    public async Task The_caller_flow_is_restored_after_Run_returns()
    {
        s_ambient.Value = "caller";
        string? inside = "unset";

        var task = BackgroundWork.Run(() =>
        {
            inside = s_ambient.Value;
            return Task.CompletedTask;
        });

        Assert.Equal("caller", s_ambient.Value);
        Assert.False(ExecutionContext.IsFlowSuppressed());
        await task;
        Assert.Null(inside);
    }

    [Fact]
    public async Task A_throwing_delegate_faults_the_returned_task_not_the_caller()
    {
        Task? task = null;
        var thrown = Record.Exception(() =>
        {
            task = BackgroundWork.Run(() => throw new InvalidOperationException("boom"));
        });

        Assert.Null(thrown);
        await Assert.ThrowsAsync<InvalidOperationException>(() => task!);
    }

    [Fact]
    public async Task Run_returns_a_task_the_caller_can_await()
    {
        var ran = false;

        await BackgroundWork.Run(async () =>
        {
            await Task.Yield();
            ran = true;
        });

        Assert.True(ran);
    }
}
