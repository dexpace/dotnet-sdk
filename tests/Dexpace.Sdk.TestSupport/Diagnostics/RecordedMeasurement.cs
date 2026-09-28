// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>One measurement a <see cref="MetricRecorder"/> observed.</summary>
/// <param name="Instrument">The name of the instrument that recorded it.</param>
/// <param name="Value">The measured value, widened to <see cref="double"/> whatever the instrument's type.</param>
/// <param name="Tags">The measurement's tags, copied out of the callback's span.</param>
public sealed record RecordedMeasurement(
    string Instrument,
    double Value,
    IReadOnlyList<KeyValuePair<string, object?>> Tags)
{
    /// <summary>The value of the tag named <paramref name="key"/>, or <see langword="null"/> when it is absent.</summary>
    /// <param name="key">The tag key.</param>
    public object? Tag(string key)
    {
        foreach (var (name, value) in Tags)
        {
            if (name == key)
            {
                return value;
            }
        }

        return null;
    }
}
