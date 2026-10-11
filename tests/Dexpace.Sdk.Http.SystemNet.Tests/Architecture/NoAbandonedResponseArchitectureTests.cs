// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Http.Response;
using Xunit;

namespace Dexpace.Sdk.Http.SystemNet.Tests.Architecture;

/// <summary>
/// TRANSPORT-9 and SEAM-30 (phase 8b, P8b-8): the reference transport never abandons a <c>Task&lt;Response&gt;</c>. It is met
/// structurally: <c>ExecuteAsync</c> is an <c>async</c> method that returns the response <c>SendAsync</c> produced, and the per-call
/// deadline is enforced by cancelling the token <c>SendAsync</c> observes, never by racing the call against a timer. The
/// primitives that build the settle race (<c>Task.WaitAsync</c>, <c>WhenAny</c>, <c>WhenAll</c>, <c>Task.Delay</c> and a
/// <c>TaskCompletionSource&lt;Response&gt;</c>) therefore appear nowhere in the library assembly, and with no abandonment there
/// is nothing for <c>LateResult</c> to dispose after. The `RS0030` bans are the compile-time layer; this IL scan is the second
/// layer and sees a call a pragma suppressed.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NoAbandonedResponseArchitectureTests
{
    [Fact]
    public void No_method_races_a_call_against_a_timer_or_settles_a_response_by_hand()
    {
        var offenders = IlCallScanner.FindCalls(typeof(SystemNetHttpClient).Assembly)
            .Where(call => IsAbandonmentPrimitive(call.Callee))
            .Select(call => call.ToString())
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void The_scanner_finds_each_planted_primitive()
    {
        var planted = IlCallScanner.FindCalls(typeof(PlantedRaces).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public).Append(typeof(PlantedRaces)))
            .Where(call => IsAbandonmentPrimitive(call.Callee))
            .Select(call => $"{call.CallerMethod.Name}|{call.Callee.DeclaringType?.Name}.{call.Callee.Name}")
            .ToList();

        Assert.Contains(planted, entry => entry.StartsWith("RacesWithWhenAny|Task.WhenAny", StringComparison.Ordinal));
        Assert.Contains(planted, entry => entry.StartsWith("RacesWithWhenAll|Task.WhenAll", StringComparison.Ordinal));
        Assert.Contains(planted, entry => entry.StartsWith("WaitsWithTimeout|Task`1.WaitAsync", StringComparison.Ordinal));
        Assert.Contains(planted, entry => entry.StartsWith("DelaysWithTask|Task.Delay", StringComparison.Ordinal));
        Assert.Contains(planted, entry => entry.StartsWith("SettlesByHand|TaskCompletionSource`1..ctor", StringComparison.Ordinal));
    }

    private static bool IsAbandonmentPrimitive(MethodBase callee) =>
        (callee.DeclaringType, callee.Name) switch
        {
            ({ FullName: "System.Threading.Tasks.Task" }, "WhenAny" or "WhenAll" or "Delay" or "WaitAsync") => true,
            ({ IsGenericType: true } type, "WaitAsync") when type.GetGenericTypeDefinition() == typeof(Task<>) => true,
            ({ IsGenericType: true } type, ".ctor") when type.GetGenericTypeDefinition() == typeof(TaskCompletionSource<>)
                && type.GetGenericArguments()[0] == typeof(Response) => true,
            _ => false,
        };

    // Callees the scanner must see; never called at runtime.
    internal static class PlantedRaces
    {
        internal static Task<Task> RacesWithWhenAny(Task a, Task b) => Task.WhenAny(a, b);

        internal static Task RacesWithWhenAll(Task a, Task b) => Task.WhenAll(a, b);

        internal static Task<Response> WaitsWithTimeout(Task<Response> pending) => pending.WaitAsync(TimeSpan.FromSeconds(1));

        internal static Task DelaysWithTask() => Task.Delay(TimeSpan.FromSeconds(1));

        internal static TaskCompletionSource<Response> SettlesByHand() => new();
    }
}
