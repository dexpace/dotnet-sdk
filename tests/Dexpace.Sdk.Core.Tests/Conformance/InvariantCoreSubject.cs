// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Conformance;
using Dexpace.Sdk.Core.Errors;
using Dexpace.Sdk.Core.Internal;
using Dexpace.Sdk.TestSupport.Recovery;

namespace Dexpace.Sdk.Core.Tests.Conformance;

/// <summary>
/// The composition the core driver runs the invariant suite over: core's own components, plus the three hooks only this project can
/// supply because they reach internals (phase 10, P10-4, P10-5). The fourth no-default hook, <c>SeamResolution</c>, lives in the
/// dependency-injection package and is supplied by that driver, so <c>xcut-23</c> stays not exercised here.
/// </summary>
internal static class InvariantCoreSubject
{
    /// <summary>The assertions this driver leaves not exercised, and why: core has no seam to resolve (P10-28, plan reading R13).</summary>
    internal static IReadOnlyList<string> ExpectedNotExercised { get; } = ["xcut-23.resolution-deterministic"];

    /// <summary>Bounds the whole suite sensibly on a slow runner.</summary>
    internal static SuiteOptions Options { get; } = new() { AssertionTimeout = TimeSpan.FromSeconds(30) };

    /// <summary>Core, with the strict mapping factory, a cyclic exception (built to be walked, never thrown) and a <see cref="BoundedMap{TKey,TValue}"/>-backed probe.</summary>
    internal static InvariantSubject Create() =>
        new()
        {
            Name = "Dexpace.Sdk.Core (internal hooks)",
            MapStrict = ErrorMapping.ToException,
            CreateCyclicException = CyclicExceptions.SelfCycle,
            CreateBoundedMap = capacity =>
            {
                var map = new BoundedMap<string, object>(capacity);
                var value = new object();
                return new BoundedMapProbe(capacity, key => map.Set(key, value), () => map.EntryCount);
            },
        };
}
