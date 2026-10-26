// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using Dexpace.Sdk.TestSupport.Diagnostics;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Support;

/// <summary>
/// The test harness's <see cref="ActivityRecorder"/>: its listener is process-wide, so a scoped recorder under construction
/// must tolerate another thread's activity stopping before its root exists (a race that once threw
/// <see cref="NullReferenceException"/> inside an unrelated test's <see cref="Activity.Stop"/>).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ActivityRecorderTests
{
    private const string SourceName = "Dexpace.Sdk.Tests.RecorderRace";

    [Fact]
    public async Task A_foreign_activity_stopping_while_a_scoped_recorder_is_built_does_not_throw()
    {
        using var source = new ActivitySource(SourceName);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stop.CancelAfter(TimeSpan.FromSeconds(3));

        // A recorder's listener throwing inside Activity.Stop surfaces here, on the foreign thread.
        var foreign = Task.Run(
            () =>
            {
                while (!stop.IsCancellationRequested)
                {
                    using var activity = source.StartActivity("foreign", ActivityKind.Internal, parentContext: default);
                }
            },
            TestContext.Current.CancellationToken);

        while (!stop.IsCancellationRequested && !foreign.IsCompleted)
        {
            using var recorder = ActivityRecorder.Scoped(SourceName);
            Assert.All(recorder.Stopped, a => Assert.Equal(recorder.Root!.TraceId, a.TraceId));
        }

        await stop.CancelAsync();
        await foreign;
    }
}
