// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.TestSupport.Diagnostics;

/// <summary>Unique host names, so a metric recorder can pick out one test's measurements from a process-wide meter.</summary>
public static class TestHosts
{
    /// <summary>A lower-case host name that no other call returns, under the reserved <c>.test</c> domain.</summary>
    /// <returns>For example <c>3f2a…c1.example.test</c>.</returns>
    public static string Unique() => $"{Guid.NewGuid():N}.example.test";
}
