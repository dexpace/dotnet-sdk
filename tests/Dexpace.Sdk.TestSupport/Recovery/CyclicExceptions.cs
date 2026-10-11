// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;

namespace Dexpace.Sdk.TestSupport.Recovery;

/// <summary>Exceptions whose <see cref="Exception.InnerException"/> chain loops, closed by reflection (design §5.2).</summary>
/// <remarks>
/// <b>Walk one anywhere; never throw one on Windows.</b> On Windows the runtime walks the inner-exception chain of every
/// exception it throws to the innermost one, with no cycle check: the throw's dispatch records Watson bucketing details on
/// its first managed frame (dotnet/runtime <c>src/coreclr/vm/excep.cpp</c>: <c>StackTraceInfo::AppendElement</c>, then
/// <c>SetupWatsonBucket</c>, then <c>SetupInitialThrowBucketDetails</c>, all compiled only when not <c>TARGET_UNIX</c>),
/// which calls <c>ExceptionObject::GetBaseException</c> (<c>object.h</c>). On a loop that walk never ends, and it runs in
/// cooperative GC mode, so the next collection waits for the throwing thread forever and every managed thread waits with
/// it, test timeouts included: the test host freezes and no test fails (phase 8a's Windows CI hang). A rethrow is a throw,
/// so a faulted task holding one must not be awaited or unwrapped with <c>GetAwaiter().GetResult()</c> there either; gate
/// such a test with <see cref="WindowsThrowHangs"/>.
/// </remarks>
public static class CyclicExceptions
{
    /// <summary>
    /// The skip reason for a test that throws one of these exceptions:
    /// <c>Assert.SkipWhen(OperatingSystem.IsWindows(), CyclicExceptions.WindowsThrowHangs)</c>.
    /// </summary>
    public const string WindowsThrowHangs =
        "Windows: the runtime walks a thrown exception's inner-exception chain to its end with no cycle check "
        + "(Watson bucketing, excep.cpp SetupInitialThrowBucketDetails) in cooperative GC mode, so throwing a cyclic "
        + "chain freezes the test host; see CyclicExceptions";

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
