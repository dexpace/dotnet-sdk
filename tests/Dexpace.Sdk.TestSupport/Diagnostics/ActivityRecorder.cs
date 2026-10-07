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
/// on the same source. Prefer <see cref="Scoped"/>, which reports only the activities of its own trace. Scoping filters
/// what a recorder reports, not what the process creates: a test that asserts "no listener" or a non-recording sampler
/// belongs in a collection that excludes every other listening class. Create a scoped recorder inside the test body, not
/// in a constructor or field initialiser, because <see cref="Activity.Current"/> is an <c>AsyncLocal</c> and a value set
/// in the class constructor is not guaranteed to flow into the test method.
/// </remarks>
public sealed class ActivityRecorder : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _started = [];
    private readonly List<Activity> _stopped = [];
    private readonly Lock _gate = new();
    private readonly ActivitySource? _rootSource;
    private readonly Activity? _previous;

    /// <summary>Starts recording the activities of the source named <paramref name="sourceName"/>.</summary>
    /// <param name="sourceName">The <see cref="ActivitySource.Name"/> to listen to.</param>
    public ActivityRecorder(string sourceName)
        : this([sourceName ?? throw new ArgumentNullException(nameof(sourceName))], scoped: false)
    {
    }

    private ActivityRecorder(string[] sourceNames, bool scoped)
    {
        var names = new HashSet<string>(sourceNames, StringComparer.Ordinal);
        if (scoped)
        {
            names.Add(RootSourceName);
            _previous = Activity.Current;
        }

        _listener = new ActivityListener
        {
            ShouldListenTo = source => names.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => Record(_started, activity),
            ActivityStopped = activity => Record(_stopped, activity),
        };
        ActivitySource.AddActivityListener(_listener);

        if (scoped)
        {
            _rootSource = new ActivitySource(RootSourceName);

            // A default parent context falls back to Activity.Current, so clear it: the root starts a fresh trace.
            Activity.Current = null;
            Root = _rootSource.StartActivity("test-root", ActivityKind.Internal, parentContext: default)
                ?? throw new InvalidOperationException("The scoped root activity was not sampled.");
        }
    }

    private const string RootSourceName = "Dexpace.Sdk.Tests.Root";

    /// <summary>
    /// Starts recording the named sources, reporting only the activities in the trace of a fresh <see cref="Root"/>
    /// activity that becomes <see cref="Activity.Current"/> in the creating context.
    /// </summary>
    /// <param name="sourceNames">The <see cref="ActivitySource.Name"/> values to listen to.</param>
    /// <returns>The recorder; dispose it to stop the root and the listener.</returns>
    public static ActivityRecorder Scoped(params string[] sourceNames)
    {
        ArgumentNullException.ThrowIfNull(sourceNames);
        return new ActivityRecorder(sourceNames, scoped: true);
    }

    /// <summary>The scoped root activity, or <see langword="null"/> for an unscoped recorder.</summary>
    public Activity? Root { get; }

    /// <summary>The activities started so far, in start order.</summary>
    public IReadOnlyList<Activity> Started => Snapshot(_started);

    /// <summary>The activities stopped so far, in stop order.</summary>
    public IReadOnlyList<Activity> Stopped => Snapshot(_stopped);

    /// <summary>The started activities of the given <paramref name="kind"/>, in start order.</summary>
    /// <param name="kind">The kind to keep.</param>
    public IReadOnlyList<Activity> StartedOfKind(ActivityKind kind) => [.. Snapshot(_started).Where(a => a.Kind == kind)];

    /// <summary>Stops the scoped root (restoring the previous current activity) and stops listening. Recorded activities stay readable.</summary>
    public void Dispose()
    {
        if (Root is not null)
        {
            Root.Stop();
            Root.Dispose();
            Activity.Current = _previous;
        }

        _listener.Dispose();
        _rootSource?.Dispose();
    }

    private void Record(List<Activity> list, Activity activity)
    {
        if (Root is null && _rootSource is not null)
        {
            // The root is being created: it is neither reported nor does it have a trace to compare with yet.
            return;
        }

        if (_rootSource is not null && (ReferenceEquals(activity, Root) || activity.TraceId != Root!.TraceId))
        {
            return;
        }

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
