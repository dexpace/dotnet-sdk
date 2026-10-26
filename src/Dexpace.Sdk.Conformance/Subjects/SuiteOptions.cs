// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// How a run of the invariant, codec or packaging suite is bounded and which gaps it accepts (P10-7). The transport suite
/// keeps <see cref="TransportSuiteOptions"/>, which adds the release bound only a socket-driving suite needs.
/// </summary>
public sealed record SuiteOptions
{
    /// <summary>
    /// The waivers in force: each turns a failing assertion into <see cref="ConformanceStatus.Waived"/>, and each must stay
    /// needed, because a waived assertion that passes fails the run. Never <see langword="null"/>; empty by default.
    /// </summary>
    public IReadOnlyList<ConformanceWaiver> Waivers
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Waivers));
            field = [.. value];
        }
    } = [];

    /// <summary>
    /// The longest one assertion may run; a hang is a <see cref="ConformanceStatus.Failed"/> result naming the bound, never a
    /// hung run. Positive; 30 seconds by default.
    /// </summary>
    public TimeSpan AssertionTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(AssertionTimeout));
            field = value;
        }
    } = TimeSpan.FromSeconds(30);
}
