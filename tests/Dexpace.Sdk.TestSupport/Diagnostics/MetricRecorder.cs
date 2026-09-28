// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics.Metrics;

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>
/// A recording <see cref="MeterListener"/> for one <see cref="Meter"/>: it enables every instrument the meter
/// publishes (or only the named ones) and records each measurement of any numeric type. Listening begins at
/// construction and ends at <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// A <see cref="MeterListener"/> is process-wide: it sees measurements that any concurrently running test records
/// on the same meter. A test class that asserts on values runs in a non-parallel xUnit collection with every other
/// class that drives that meter.
/// </remarks>
public sealed class MetricRecorder : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<RecordedMeasurement> _measurements = [];
    private readonly Lock _gate = new();

    /// <summary>Starts recording the meter named <paramref name="meterName"/>.</summary>
    /// <param name="meterName">The <see cref="Meter.Name"/> to listen to.</param>
    /// <param name="instrumentNames">The instruments to record; none means every instrument of the meter.</param>
    public MetricRecorder(string meterName, params string[] instrumentNames)
    {
        ArgumentNullException.ThrowIfNull(meterName);
        ArgumentNullException.ThrowIfNull(instrumentNames);
        var wanted = new HashSet<string>(instrumentNames, StringComparer.Ordinal);
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == meterName && (wanted.Count == 0 || wanted.Contains(instrument.Name)))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<byte>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<short>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<float>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _listener.SetMeasurementEventCallback<decimal>(
            (instrument, value, tags, _) => Record(instrument, (double)value, tags));
        _listener.Start();
    }

    /// <summary>Every measurement recorded so far, in recording order.</summary>
    public IReadOnlyList<RecordedMeasurement> Measurements
    {
        get
        {
            lock (_gate)
            {
                return [.. _measurements];
            }
        }
    }

    /// <summary>The measurements recorded so far by the instrument named <paramref name="instrumentName"/>.</summary>
    /// <param name="instrumentName">The instrument name.</param>
    public IReadOnlyList<RecordedMeasurement> For(string instrumentName)
    {
        lock (_gate)
        {
            return _measurements.FindAll(measurement => measurement.Instrument == instrumentName);
        }
    }

    /// <summary>Polls the enabled observable instruments, recording what they report.</summary>
    public void RecordObservableInstruments() => _listener.RecordObservableInstruments();

    /// <summary>Stops listening. Measurements recorded so far stay readable.</summary>
    public void Dispose() => _listener.Dispose();

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var measurement = new RecordedMeasurement(instrument.Name, value, tags.ToArray());
        lock (_gate)
        {
            _measurements.Add(measurement);
        }
    }
}
