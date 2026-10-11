// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance.Tests.Support;

/// <summary>
/// A leaked socket is closed by its finalizer once it is garbage, which would let a leak "release" itself between garbage
/// collections and make the leak controls flaky. Whatever a control leaks is rooted here for the rest of the process, so a
/// leak stays a leak.
/// </summary>
internal static class Leaks
{
    private static readonly List<object> s_rooted = [];

    /// <summary>Keeps <paramref name="leaked"/> reachable, so its finalizer cannot close what it holds.</summary>
    internal static T Root<T>(T leaked)
        where T : class
    {
        lock (s_rooted)
        {
            s_rooted.Add(leaked);
        }

        return leaked;
    }
}
