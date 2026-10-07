// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;

namespace Dexpace.Sdk.TestSupport.Recovery;

/// <summary>Exceptions whose <see cref="Exception.InnerException"/> chain loops, closed by reflection (design §5.2).</summary>
public static class CyclicExceptions
{
    private static readonly FieldInfo s_inner = typeof(Exception).GetField(
        "_innerException",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Exception._innerException was not found on this runtime.");

    /// <summary>An exception whose inner exception is itself.</summary>
    /// <returns>The exception.</returns>
    public static Exception SelfCycle()
    {
        var exception = new InvalidOperationException("self");
        s_inner.SetValue(exception, exception);
        return exception;
    }

    /// <summary>Two exceptions that are each other's inner exception.</summary>
    /// <returns>The first of the two; its inner exception is the second.</returns>
    public static Exception TwoNodeCycle()
    {
        var a = new InvalidOperationException("a");
        var b = new InvalidOperationException("b");
        s_inner.SetValue(a, b);
        s_inner.SetValue(b, a);
        return a;
    }
}
