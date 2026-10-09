// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>How a suite run is bounded and which gaps it accepts.</summary>
public sealed class TransportSuiteOptions
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
            ArgumentNullException.ThrowIfNull(value);
            field = [.. value];
        }
    } = [];

    /// <summary>
    /// The longest one assertion may run (suite-contract clause 6); a hang is a <see cref="ConformanceStatus.Failed"/>
    /// result naming the bound, never a hung run. Positive; 30 seconds by default.
    /// </summary>
    public TimeSpan AssertionTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long an assertion waits for the server to observe that the client released a connection, and the window of the
    /// one timed negative ("no second request arrives"). A transport that leaks a connection for longer and then frees it
    /// passes: this bound is the kit's definition of "promptly". Positive; 10 seconds by default.
    /// </summary>
    public TimeSpan ReleaseTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            field = value;
        }
    } = TimeSpan.FromSeconds(10);
}
