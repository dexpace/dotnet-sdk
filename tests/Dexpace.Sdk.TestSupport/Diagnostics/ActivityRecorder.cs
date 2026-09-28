// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>
/// A recording <see cref="ActivityListener"/> for one <see cref="ActivitySource"/>: every activity the source
/// creates is sampled with all data and recorded as it starts and as it stops. Listening begins at construction and
/// ends at <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// An <see cref="ActivityListener"/> is process-wide: it sees activities that any concurrently running test starts
/// on the same source. A test class that asserts on counts runs in a non-parallel xUnit collection with every other
/// class that drives that source.
/// </remarks>
public sealed class ActivityRecorder : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _started = [];
    private readonly List<Activity> _stopped = [];
    private readonly Lock _gate = new();

    /// <summary>Starts recording the activities of the source named <paramref name="sourceName"/>.</summary>
    /// <param name="sourceName">The <see cref="ActivitySource.Name"/> to listen to.</param>
    public ActivityRecorder(string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Record(_started, activity),
            ActivityStopped = activity => Record(_stopped, activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>The activities started so far, in start order.</summary>
    public IReadOnlyList<Activity> Started => Snapshot(_started);

    /// <summary>The activities stopped so far, in stop order.</summary>
    public IReadOnlyList<Activity> Stopped => Snapshot(_stopped);

    /// <summary>Stops listening. Activities recorded so far stay readable.</summary>
    public void Dispose() => _listener.Dispose();

    private void Record(List<Activity> list, Activity activity)
    {
        lock (_gate)
        {
            list.Add(activity);
        }
    }

    private List<Activity> Snapshot(List<Activity> list)
    {
        lock (_gate)
        {
            return [.. list];
        }
    }
}
